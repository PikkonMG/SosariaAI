namespace SosariaAI.Combat;

/// <summary>
/// How a warrior or a fencer fights with its feet, as a 1999 dexxer did: with a wall or a rock
/// at its back when several foes come at it, so fewer of them can reach it; a step out of
/// reach while the bandage works when the fight turns, then back in on the same foe; and a
/// return, healed, to the lone foe it ran from. Live, every fighter ran one loop (press, step
/// back, run), and the 11 runners of a 9.5 hour log that turned back healed all turned on a
/// person: a runner a monster chased ran to the end of its run and picked a fresh fight. Pure.
/// </summary>
public static class MeleeTacticsRules
{
    /// <summary>A stand is weighed once this many foes are close (<see cref="FootworkRules.CrowdCount"/>).</summary>
    public const int WallFoes = FootworkRules.CrowdCount;

    /// <summary>
    /// A step to a new stand must shut at least this many open neighbours: one fewer only
    /// shuffles the fighter from tile to tile, and each step is a moment it does not press.
    /// </summary>
    public const int WallGainSlots = 2;

    /// <summary>
    /// A stand is weighed when <see cref="WallFoes"/> or more foes are close, the fighter is
    /// not drawing one off a group, and it is not holding ground (<see cref="CombatStance.Hold"/>:
    /// no footwork at all).
    /// </summary>
    public static bool WeighsWall(int closeFoes, bool drawing, CombatStance stance) =>
        closeFoes >= WallFoes && !drawing && stance != CombatStance.Hold;

    /// <summary>
    /// A neighbouring tile is a better stand when the foe stays in weapon reach from it, it has
    /// at least <see cref="WallGainSlots"/> fewer open neighbours than here (fewer tiles a foe
    /// can swing from), it is no dead end (<see cref="EscapeRules.DeadEndExits"/>: a corner
    /// the fighter cannot run out of when the fight turns), and no more idle hostiles stand by
    /// it than by here (the steps keep off hostiles not on the fighter yet).
    /// </summary>
    public static bool BetterStand(int hereSlots, int standSlots, bool foeInReach, int idleFoesHere, int idleFoesThere) =>
        foeInReach &&
        standSlots > EscapeRules.DeadEndExits &&
        hereSlots - standSlots >= WallGainSlots &&
        idleFoesThere <= idleFoesHere;

    /// <summary>A fighter healing out steps back while a foe stands this close: in reach, or one step from it.</summary>
    public const int HealOutTiles = CombatBrain.MeleeRange + FootworkRules.SingleStep;

    /// <summary>
    /// A melee fighter keeps out of reach while its own bandage works, then goes back in on the
    /// same foe. Only with a bandage on itself (a heal potion heals at once and needs no room),
    /// a foe that fights up close (a step does not take it out of a bow's or a spell's reach),
    /// and ground to step on. The fight must be turning: lost on the trade of blows in any era,
    /// or below the heal stance line (<see cref="StanceRules.HealStanceHitsFraction"/>) where
    /// every blow slips the bandage. Pre-AOS any damage slips it, four hit points off the heal
    /// and two points off its chance each time (PlayerMobile.OnDamage, BandageContext.Slip);
    /// from AOS on only a blow above 18 from a person or 25 from a monster does. It holds until
    /// the bandage is done, out of reach too: ended by the first step that left the foe's reach,
    /// it walked straight back in and the next blow slipped the bandage.
    /// </summary>
    public static bool StepsOutToHeal(
        bool bandageOn,
        bool everyBlowSlips,
        double hitsFraction,
        FightOutlook outlook,
        bool foeHitsFromRange,
        bool pinned
    ) =>
        bandageOn && !foeHitsFromRange && !pinned &&
        (outlook == FightOutlook.Losing || everyBlowSlips && hitsFraction < StanceRules.HealStanceHitsFraction);

    /// <summary>
    /// A fighter keeping out of reach to heal steps back while the nearest foe on it stands
    /// within <see cref="HealOutTiles"/>, and stands still past it: it does not walk back in.
    /// </summary>
    public static bool StepsBack(int nearestFoeDistance) => nearestFoeDistance <= HealOutTiles;

    /// <summary>
    /// A melee fighter that ran from a monster goes back for that same monster once it healed
    /// to <see cref="RetreatRules.ComeBackFraction"/> and is now the fresher of the two, while
    /// the ground it left is still avoided (<see cref="RetreatRules.AvoidMs"/>), the monster stands
    /// alone (no packmate near it: the pack it ran from is what the avoided ground keeps it off),
    /// and no bard's song holds it calm. A person is never come back for here: the run's own
    /// rules turn on a person (<see cref="RetreatRules.ComesBackHealed"/>), and a person run from
    /// is left alone for its grace.
    /// </summary>
    public static bool ComesBackFor(
        bool isPerson,
        bool harmless,
        bool calmed,
        int packmates,
        double hitsFraction,
        double foeHitsFraction,
        long sinceRunMs
    ) =>
        !isPerson && !harmless && !calmed && packmates == 0 &&
        hitsFraction >= RetreatRules.ComeBackFraction && foeHitsFraction < hitsFraction &&
        sinceRunMs >= 0 && sinceRunMs < RetreatRules.AvoidMs;
}
