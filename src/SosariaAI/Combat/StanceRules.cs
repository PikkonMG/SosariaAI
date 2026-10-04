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
/// BlueOnRed: a character who is not red fights a red person.
/// </summary>
public readonly record struct StanceSituation(
    CombatStyle Style,
    bool Caster,
    bool TankMage,
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
    bool Sticky,
    FightOutlook Outlook,
    bool LightDamage,
    bool Cornered,
    bool BlueOnRed
);

/// <summary>
/// The stance a character takes on its own, every tick, when Jev has not set one: heal when
/// badly hurt, ward after the words keep breaking and kite when they break through the ward,
/// freeze a melee foe that the kite cannot shake, kite while the mana comes back, else press.
/// A tank mage presses and swings while its words keep breaking. Pressing still steps clear for a spell
/// or a shot that needs the room, and acts where it stands once the steps cannot buy it. A
/// cornered fighter does not kite into the wall: it presses where it stands. A blue on a red
/// presses while the trade goes its way and kites only when it is losing. Also the gate on
/// Jev's answer: a stance the character cannot carry out, or one these rules forbid, is never taken.
/// </summary>
public static class StanceRules
{
    /// <summary>Below this share of hits the fight waits for the wounds.</summary>
    public const double HealStanceHitsFraction = 0.45;

    /// <summary>
    /// This many broken casts inside the interrupt window call for the ward, then for more
    /// room, and stop a tank mage's casting.
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

    public static CombatStance Pick(StanceSituation s)
    {
        if (s.HitsFraction < HealStanceHitsFraction && Feasible(CombatStance.Heal, s))
        {
            return CombatStance.Heal;
        }

        if (!s.Caster || PressesRed(s))
        {
            return CombatStance.Press;
        }

        // A tank mage casts from the melee path, which has no ward: its answer to broken
        // words is the weapon (see KeepsBreaking), not a stance it cannot carry out.
        if (s.TankMage)
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
    /// The words broke this often inside the interrupt window: a mage wards, then backs off;
    /// a tank mage stops casting and swings until the window clears.
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
}
