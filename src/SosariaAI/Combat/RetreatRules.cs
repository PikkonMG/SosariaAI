using System;

namespace SosariaAI.Combat;

/// <summary>
/// When a fighter leaves a fight, and when it turns back. The hit line comes from the role
/// and the tier, divided by nerve, and rises with every extra attacker: incoming damage
/// grows with the swarm while the run out does not. A player surrounded by more than it can
/// answer leaves at full health. A bold one that is losing to something losing harder
/// swings for the kill instead. Above all, a fight the trade of blows says it is winning is
/// not left, whatever the count of foes (<see cref="FightTrendRules"/>). A run is over when
/// the pack and the thing it ran from are both well off; the same thing chasing it off
/// twice in a minute is a hunt, and then only twice that gap is clear. The ground it ran
/// from is left alone for a while after.
/// </summary>
public static class RetreatRules
{
    public const double WorkerLine = 0.60;
    public const double FighterLine = 0.40;
    public const double VeteranLine = 0.30;
    public const double ExtraAttackerRaise = 0.10;

    /// <summary>
    /// Nerve moves the one-foe line no higher than this. A timid worker's 0.60 over its 0.6
    /// nerve made the line the whole bar: Tobiah left a skeleton at 70 of 74 hits.
    /// </summary>
    public const double MaxOneFoeLine = 0.60;
    public const int MaxCountedExtraAttackers = 4;

    /// <summary>Starting a fight on the retreat line means running at the first hit.</summary>
    public const double StartMargin = 0.10;
    public const double MinLine = 0.15;
    public const double MaxLine = 0.95;
    public const int OutnumberedAttackers = 2;
    public const double OutnumberedMultiple = 2.0;
    public const double GambleNerve = 1.3;
    public const double GambleEdge = 0.9;
    public const int SingleAttacker = 1;

    /// <summary>Turns on a straggler per fight before the runner admits the pack will not string out.</summary>
    public const int MaxTurns = 4;

    /// <summary>Thinks in a row a retreat leg may make no ground before it is struck off (a door, a crowd, a bad goal).</summary>
    public const int EscapeStallTicks = 4;

    /// <summary>A runner that found no place to run to looks again after this long.</summary>
    public const int EscapeRepickMs = 1000;

    /// <summary>A runner that turned at bay does not try to run again for this long.</summary>
    public const int AtBayMs = 10000;

    /// <summary>
    /// A hunted runner (<see cref="IsHunted"/>) with no ground left to run on cannot outrun
    /// the one hunting it: it stands and fights the rest of that fight out, as a player with
    /// nowhere to go did, rather than turning at bay for <see cref="AtBayMs"/> and running into
    /// the same corner again. Sir Thaddeus ran from Perrin, was cornered and turned on him,
    /// 51 times in twenty minutes.
    /// </summary>
    public static bool StandsFast(bool hunted, bool cornered) => hunted && cornered;

    public static double BaseLine(CharacterRole role, bool veteran) =>
        role == CharacterRole.Worker ? WorkerLine : veteran ? VeteranLine : FighterLine;

    public static double Line(double baseLine, double nerve, int attackers)
    {
        var extra = Math.Clamp(attackers - SingleAttacker, 0, MaxCountedExtraAttackers);
        var line = Math.Min(MaxOneFoeLine, baseLine / Math.Max(NerveRules.MinNerve, nerve)) + ExtraAttackerRaise * extra;
        return Math.Clamp(line, MinLine, MaxLine);
    }

    /// <summary>Fit to start a fight against <paramref name="foes"/> means some room above that fight's line.</summary>
    public static double StartLine(double baseLine, double nerve, int foes) =>
        Math.Min(MaxLine, Line(baseLine, nerve, foes) + StartMargin);

    /// <summary>Two or more already swinging and a room far beyond the dare: leave on the numbers.</summary>
    public static bool IsOutnumbered(int attackers, int roomThreat, int darePower, double threatMultiple) =>
        attackers >= OutnumberedAttackers && roomThreat > darePower * threatMultiple * OutnumberedMultiple;

    /// <summary>During a pick-off the whole pack chases; only the ones that caught up count.</summary>
    public static bool PackCaughtUp(int closeAttackers, int closeThreat, int darePower, double threatMultiple) =>
        closeAttackers >= OutnumberedAttackers && closeThreat > darePower * threatMultiple;

    /// <summary>Bold, one on one, and the foe clearly worse off: no retreat.</summary>
    public static bool IsGambling(double nerve, int attackers, double selfFraction, double foeFraction) =>
        nerve >= GambleNerve && attackers <= SingleAttacker && foeFraction < selfFraction * GambleEdge;

    /// <summary>
    /// A winning trade holds down to <see cref="FightTrendRules.WinningFloor"/>, through the
    /// numbers and the raised line. Otherwise the numbers or the hit line send it off, unless
    /// it gambles.
    /// </summary>
    public static bool ShouldRetreat(double hitsFraction, double line, bool outnumbered, bool gambling, FightOutlook outlook) =>
        outlook == FightOutlook.Winning
            ? !gambling && hitsFraction < FightTrendRules.WinningFloor
            : outnumbered || !gambling && hitsFraction < line;

    /// <summary>
    /// A fight this character would leave the moment it began: the run test above put before
    /// the first blow, at the start line and with no trade of blows yet. Bevis drew on a guild
    /// enemy, said "too many" and ran in the same second, got clear and drew again.
    /// </summary>
    public static bool WouldLeaveAtOnce(double hitsFraction, double startLine, bool outnumbered, bool overwhelmed) =>
        ShouldRetreat(hitsFraction, startLine, outnumbered || overwhelmed, gambling: false, FightOutlook.Unknown);

    /// <summary>A run is clear once the pack and the thing it ran from stand this far off.</summary>
    public const int ClearTiles = 14;

    /// <summary>
    /// A hunted runner is clear only this far off. A lich walked one mage between two
    /// waypoints ten tiles apart seven times, each run ending "clear" a few tiles out.
    /// </summary>
    public const int HuntedClearTiles = 24;

    /// <summary>A second run from the same thing inside this long means it is hunting the runner.</summary>
    public const int HuntedWindowMs = 60000;

    /// <summary>Fresh fights near the ground a runner left are declined this long.</summary>
    public const int AvoidMs = 45000;

    /// <summary>A hunted runner stays off that ground this long.</summary>
    public const int HuntedAvoidMs = 120000;

    /// <summary>A foe this close to the ground the runner left counts as still standing on it.</summary>
    public const int AvoidRadiusTiles = FightPullRules.IsolateRange + 1;

    public static int ClearAt(bool hunted) => hunted ? HuntedClearTiles : ClearTiles;

    /// <summary>
    /// Clear when the nearest of the pack and the thing the run started from are both at
    /// <see cref="ClearAt"/> or beyond (<see cref="RoomSurvey.NoDistance"/> for none or gone).
    /// Testing the pack alone let a runner from a red go clear on its first step and be hit again.
    /// </summary>
    public static bool IsClear(int packNearest, int sourceDistance, bool hunted) =>
        packNearest >= ClearAt(hunted) && sourceDistance >= ClearAt(hunted);

    /// <summary>A run from the same thing as the last one, started inside <see cref="HuntedWindowMs"/> of it.</summary>
    public static bool IsHunted(bool sameSource, long lastRunAt, long now) =>
        sameSource && lastRunAt != 0 && now - lastRunAt < HuntedWindowMs;

    public static int AvoidForMs(bool hunted) => hunted ? HuntedAvoidMs : AvoidMs;

    /// <summary>
    /// A foe standing on ground the runner left is not a fresh fight while the window is
    /// open. One on this character or on a friend still is: that fight is already on.
    /// </summary>
    public static bool Avoids(long avoidUntil, long now, int tilesFromGround, bool alreadyInFight) =>
        now - avoidUntil < 0 && tilesFromGround <= AvoidRadiusTiles && !alreadyInFight;

    public static bool ShouldTurnOnChaser(
        int turnsTaken,
        bool chaserIsPerson,
        bool fitToStart,
        bool straggler,
        bool chaserFitsDare
    ) =>
        turnsTaken < MaxTurns && !chaserIsPerson && fitToStart && straggler && chaserFitsDare;
}
