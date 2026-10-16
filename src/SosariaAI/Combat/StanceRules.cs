namespace SosariaAI.Combat;

/// <summary>How a character fights for the next few seconds. The footwork rules carry it out.</summary>
public enum CombatStance
{
    /// <summary>Stay on the attack at the usual range; open a gap only for a spell that needs it.</summary>
    Press,

    /// <summary>Keep well clear and fight from range; the way to let mana come back.</summary>
    Kite,

    /// <summary>Stop attacking and treat the wounds, from distance when the heal needs it.</summary>
    Heal,

    /// <summary>Freeze the sticky attacker, then walk away from it while it stands frozen, then the big spells.</summary>
    ParalyzeThenKite,

    /// <summary>Put up the ward the era has before anything else.</summary>
    Protect,

    /// <summary>Break off and run, drinking and bandaging on the way.</summary>
    Flee,

    /// <summary>Stand and fight where it is: no footwork, only what lands from here.</summary>
    Hold
}

/// <summary>
/// The fight as the stance rules and Jev see it, all computed in code: fractions, counts and
/// flags, and the trade of blows (<see cref="FightTrendRules"/>). Jev is given these as
/// words; the rules read them as they are. Cornered: the last back-off ran in place.
/// BlueOnRed: a character who is not red fights a red person. FoeOnFriend: the foe's own
/// target is a friend of this character or a friend's pet, not this character.
/// </summary>
public readonly record struct StanceSituation(
    CombatStyle Style,
    bool Caster,
    double HitsFraction,
    double ManaFraction,
    bool Poisoned,
    bool NeedsCare,
    bool FoeIsPerson,
    CombatStyle FoeStyle,
    double FoeHitsFraction,
    int FoeDistance,
    bool FoeCasting,
    bool FoeHeld,
    int AdjacentFoes,
    int RecentInterrupts,
    bool Warded,
    bool CanWard,
    bool CanParalyze,
    bool HasHealPotion,
    bool HasBandage,
    bool CanHealSpell,
    int AlliesNear,
    bool FoeOnFriend,
    bool Sticky,
    FightOutlook Outlook,
    bool LightDamage,
    bool Cornered,
    bool BlueOnRed
);

/// <summary>
/// The stance a character takes on its own, every tick, when no model has set one: finish a
/// foe about to fall, heal when badly hurt, ward after the words keep breaking and kite when
/// they break through the ward, hold and cast or shoot while a friend takes the foe's blows,
/// freeze a melee foe that the kite cannot shake, kite while the mana comes back, an archer
/// kites a melee foe it cannot shake or is losing to, else press. Pressing still steps clear
/// for a spell or a shot that needs the room, and acts where it stands once the steps cannot
/// buy it. A cornered fighter does not kite into the wall: it presses where it stands. A blue
/// on a red presses while the trade goes its way and kites only when it is losing. Also the
/// gate on a model's answer: a stance the character cannot carry out, or one these rules
/// forbid, is never taken.
/// </summary>
public static class StanceRules
{
    /// <summary>Below this share of hits the fight waits for the wounds.</summary>
    public const double HealStanceHitsFraction = 0.45;

    /// <summary>
    /// This many broken casts inside the interrupt window call for the ward, then for more
    /// room.
    /// </summary>
    public const int WardAfterInterrupts = 2;

    /// <summary>A caster below this share of mana kites while the pool comes back.</summary>
    public const double KiteManaFraction = 0.15;

    /// <summary>A monster this much stronger than the character's dare is a fight worth a Jev call.</summary>
    public const double ClearlyStrongerMultiple = 1.25;

    /// <summary>Below this share of hits any fight is worth a Jev call.</summary>
    public const double AskBelowHitsFraction = 0.5;

    public const double PressYes = 0.5;
    public const double ProtectYes = 0.55;
    public const double ParalyzeYes = 0.6;
    public const double FleeYes = 0.65;

    /// <summary>The best stance's yes must beat the next yes by this much, or the rules pick.</summary>
    public const double ClearWinGap = 0.05;

    /// <summary>A foe at or below this share of its hits falls to one more attack (<see cref="MageTacticsRules.BurstFoeHitsFraction"/>).</summary>
    public const double FinishFoeHitsFraction = MageTacticsRules.BurstFoeHitsFraction;

    /// <summary>Below this share of its own hits a fighter heals first: the next blows may kill it before the foe falls.</summary>
    public const double FinishOwnHitsFloor = 0.25;

    /// <summary>A friend takes the blows only while nothing stands next to this character.</summary>
    public const int NoAdjacentFoes = 0;

    public static CombatStance Pick(StanceSituation s)
    {
        if (Finishes(s))
        {
            return CombatStance.Press;
        }

        if (s.HitsFraction < HealStanceHitsFraction && Feasible(CombatStance.Heal, s))
        {
            return CombatStance.Heal;
        }

        if (PressesRed(s))
        {
            return CombatStance.Press;
        }

        if (s.Style == CombatStyle.Archer)
        {
            return ArcherStance(s);
        }

        if (!s.Caster)
        {
            return CombatStance.Press;
        }

        if (KeepsBreaking(s.RecentInterrupts) && Feasible(CombatStance.Protect, s))
        {
            return CombatStance.Protect;
        }

        // The ward is up, or out of reach, and the words still break: the mage backs off for
        // the room to finish them, where pressing cast into the same blows again and again.
        if (KeepsBreaking(s.RecentInterrupts) && Feasible(CombatStance.Kite, s))
        {
            return CombatStance.Kite;
        }

        // The foe beats on a friend: every step away from it is a spell not cast.
        if (HoldsBehindFriend(s))
        {
            return CombatStance.Hold;
        }

        // A frozen melee foe is walked away from before the big spell breaks it free; a free one
        // that the kite could not shake is frozen first.
        if (s.FoeStyle == CombatStyle.Melee &&
            (s.FoeHeld ? s.FoeDistance < FootworkRules.KiteBandTiles : s.Sticky && s.CanParalyze))
        {
            return CombatStance.ParalyzeThenKite;
        }

        return s.ManaFraction < KiteManaFraction && Feasible(CombatStance.Kite, s) ? CombatStance.Kite : CombatStance.Press;
    }

    /// <summary>
    /// An archer holds while a friend takes the foe's blows, kites a melee foe that it cannot
    /// shake or that is winning the trade, else presses: its own footwork steps clear for each
    /// shot (<see cref="FootworkRules.Next"/>).
    /// </summary>
    private static CombatStance ArcherStance(StanceSituation s)
    {
        if (HoldsBehindFriend(s))
        {
            return CombatStance.Hold;
        }

        return s.FoeStyle == CombatStyle.Melee && (s.Sticky || s.Outlook == FightOutlook.Losing) &&
               Feasible(CombatStance.Kite, s)
            ? CombatStance.Kite
            : CombatStance.Press;
    }

    /// <summary>
    /// The foe is one attack from falling and this character is neither losing nor close to
    /// death: it stays on the foe. A mage short of mana kited away from a foe one spell from
    /// falling, and a hurt one stepped out to heal while the foe got its own hits back.
    /// </summary>
    public static bool Finishes(StanceSituation s) =>
        s.FoeHitsFraction <= FinishFoeHitsFraction && s.HitsFraction >= FinishOwnHitsFloor &&
        s.Outlook != FightOutlook.Losing;

    /// <summary>
    /// A caster or an archer whose foe beats on a friend, with nothing next to itself, holds
    /// its ground and casts or shoots from where it stands: a party mage behind its tank.
    /// </summary>
    public static bool HoldsBehindFriend(StanceSituation s) =>
        (s.Caster || s.Style == CombatStyle.Archer) && s.FoeOnFriend && s.AdjacentFoes == NoAdjacentFoes &&
        Feasible(CombatStance.Hold, s);

    /// <summary>
    /// The words broke this often inside the interrupt window: a mage wards, then backs off.
    /// </summary>
    public static bool KeepsBreaking(int recentInterrupts) => recentInterrupts >= WardAfterInterrupts;

    /// <summary>True when the character has what the stance needs.</summary>
    public static bool Feasible(CombatStance stance, StanceSituation s) =>
        stance switch
        {
            CombatStance.Kite => (s.Caster || s.Style == CombatStyle.Archer) && !s.Cornered &&
                                 (!s.BlueOnRed || s.Outlook == FightOutlook.Losing),
            CombatStance.Flee or CombatStance.Hold => !PressesRed(s),
            CombatStance.Heal => s.NeedsCare && (s.HasHealPotion || s.HasBandage || s.CanHealSpell),
            CombatStance.ParalyzeThenKite => s.Caster && (s.FoeHeld || s.CanParalyze),
            CombatStance.Protect => s.CanWard && !s.Warded,
            _ => true
        };

    /// <summary>
    /// Jev is offered a stance the character can carry out (<see cref="Feasible"/>), and the
    /// Kite stance only where <see cref="OffersKite"/> says it is worth weighing.
    /// </summary>
    public static bool Askable(CombatStance stance, StanceSituation s) =>
        Feasible(stance, s) && (stance != CombatStance.Kite || OffersKite(s));

    /// <summary>
    /// The Kite stance is put to Jev against a person, or for a caster whose words keep breaking
    /// or whose mana runs low: the tests <see cref="Pick"/> kites on. Its rubric says yes to any
    /// archer or mage with a foe that fights up close, which is nearly every monster fight, so
    /// it was asked where the rules never kite and the yes only sent the fighter running.
    /// </summary>
    public static bool OffersKite(StanceSituation s) =>
        s.FoeIsPerson || s.Caster && (KeepsBreaking(s.RecentInterrupts) || s.ManaFraction < KiteManaFraction);

    /// <summary>
    /// A blue winning the trade against a red presses: no kite, no hold, no run. One anti-PK
    /// backed off from reds 173 times in an hour while its blows were landing.
    /// </summary>
    public static bool PressesRed(StanceSituation s) => s.BlueOnRed && s.Outlook == FightOutlook.Winning;

    /// <summary>How clear a yes must be to set this stance: the costlier a wrong call, the surer.</summary>
    public static double MinYes(CombatStance stance) =>
        stance switch
        {
            CombatStance.Flee => FleeYes,
            CombatStance.ParalyzeThenKite => ParalyzeYes,
            CombatStance.Protect => ProtectYes,
            _ => PressYes
        };

    /// <summary>
    /// Jev is asked only about a fight that matters: one against a person, one against a foe
    /// clearly stronger than what the character dares alone, or one that has the character
    /// below half its hits. Every other monster fight stays with the rules.
    /// </summary>
    public static bool WorthAsking(bool foeIsPerson, int foeThreat, int dare, double hitsFraction) =>
        foeIsPerson || foeThreat > dare * ClearlyStrongerMultiple || hitsFraction < AskBelowHitsFraction;

    /// <summary>
    /// Jev is asked only where a real player is part of the fight: the foe is one, one hits or
    /// was hit by either side, one is in a party with either side, or one can see the fight.
    /// Every other fight keeps the rules' stance with no call. Live, 9.4 hours with 800
    /// characters made 22,772 stance calls, 44% of them unsure, and 1,990 of 2,387 deaths were
    /// blues killing reds with no player near: those calls changed nothing a player could see.
    /// </summary>
    public static bool PlayerInvolved(bool foeIsHuman, bool humanTradesBlows, bool humanInParty, bool humanSees) =>
        foeIsHuman || humanTradesBlows || humanInParty || humanSees;
}
