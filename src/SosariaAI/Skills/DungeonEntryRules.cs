using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>One leg of the way into a dungeon hall.</summary>
public enum DungeonLeg
{
    /// <summary>Walk the surface to the door's teleporter pad.</summary>
    WalkToDoor,

    /// <summary>Step onto the pad; the engine carries the person inside.</summary>
    StepOnPad,

    /// <summary>Walk the dungeon floor from the landing to the hall.</summary>
    WalkInside,

    /// <summary>One walk all the way, when no door pad is known.</summary>
    WalkToHall
}

/// <summary>
/// The way into a dungeon, as a player makes it: a walk on the surface to the door pad,
/// a step onto the pad, then a separate walk inside to the hall. One long walk across the
/// map to a hall inside failed 250 times in a run and reached the inside 53 times in 604.
/// </summary>
public static class DungeonEntryRules
{
    /// <summary>How long a person may spend getting onto the door pad before the step is refused.</summary>
    public const int PadStepSeconds = 20;

    public static readonly TimeSpan PadStepTimeout = TimeSpan.FromSeconds(PadStepSeconds);

    /// <summary>A door's pad lies this close to the catalog door spot.</summary>
    public const int PadSearchTiles = 8;

    /// <summary>
    /// The walk to the door ends beside the real pad. It once ended three tiles from a graph
    /// node, and the Orc Cave pad two tiles further on was out of the step's reach.
    /// </summary>
    public const int PadApproachTiles = 1;

    public const int NoPad = -1;

    /// <summary>
    /// The shortest trip to a dungeon door a person recalls for instead of walking. A run
    /// starts with the recall, as a player's did: Britain to Destard's door is about 150
    /// tiles, and at the town-errand bar of <see cref="RecallRules.MinTripTiles"/> it was walked.
    /// </summary>
    public const int RecallMinTripTiles = 80;

    /// <summary>
    /// Casts of the door recall before the person walks: the first and two more, as a
    /// player recast a fizzle. One fizzle sent 63 runs in twenty minutes on foot across the
    /// map, and a Moonglow warrior walked to the Den gate and was still walking seven
    /// minutes later.
    /// </summary>
    public const int DoorRecallCasts = 3;

    public const int DoorRecallRetrySeconds = 20;

    /// <summary>
    /// How long a fizzled door recall waits to be cast again: a runebook rests seven seconds
    /// after each use, and the caster recovers from the last cast.
    /// </summary>
    public static readonly TimeSpan DoorRecallRetryWait = TimeSpan.FromSeconds(DoorRecallRetrySeconds);

    public const int PadDirectStepSeconds = 5;

    /// <summary>
    /// A person beside the pad this long whose straight step has not carried it paths onto
    /// the pad instead: a wall corner blocks the diagonal step onto the Britain Sewer pad
    /// on the upper floor at z 24.
    /// </summary>
    public static readonly TimeSpan PadDirectStep = TimeSpan.FromSeconds(PadDirectStepSeconds);

    /// <summary>
    /// Times a trip goes back in by the door after a pad inside carried the person out. The
    /// Orc Cave and Deceit landings lie beside their exit pads, and a walk from the landing
    /// stepped onto them.
    /// </summary>
    public const int MaxReentries = 2;

    /// <summary>
    /// A walk to the door this long, the public moongates counted, sends a lone traveller to
    /// a nearer dungeon when no recall or gate took. A Moonglow warrior whose recall fizzled
    /// walked for Covetous by way of the Den gate and died among the reds there.
    /// </summary>
    public const int LongWalkTiles = 400;

    public static DungeonLeg FirstLeg(bool inside, bool knowsDoorPad) =>
        inside ? DungeonLeg.WalkInside
        : knowsDoorPad ? DungeonLeg.WalkToDoor
        : DungeonLeg.WalkToHall;

    /// <summary>
    /// A person outside recalls to the door mark before it walks. One in a group does so
    /// only when every member can recall after it; otherwise the group walks together.
    /// </summary>
    public static bool RecallsToDoor(bool inside, bool grouped, bool crewCanRecall) =>
        !inside && (!grouped || crewCanRecall);

    /// <summary>The line a recall to a dungeon door writes, easy to count.</summary>
    public static string RecalledToDoorLine(string name, string dungeon) => $"{name} recalled to the door of {dungeon}";

    /// <summary>The leg after <paramref name="leg"/>, or null when that leg ends at the hall.</summary>
    public static DungeonLeg? After(DungeonLeg leg) =>
        leg switch
        {
            DungeonLeg.WalkToDoor => DungeonLeg.StepOnPad,
            DungeonLeg.StepOnPad => DungeonLeg.WalkInside,
            _ => null
        };

    /// <summary>A person carried inside early, by a pad on the way, skips to the inside walk.</summary>
    public static bool SkipsToInside(DungeonLeg leg, bool inside) =>
        inside && leg is DungeonLeg.WalkToDoor or DungeonLeg.StepOnPad;

    public static bool PadStepExpired(DateTime now, DateTime started) => now - started >= PadStepTimeout;

    /// <summary>True once the straight step onto the pad has had <see cref="PadDirectStep"/>: the walk paths onto it.</summary>
    public static bool PathsOntoPad(DateTime now, DateTime started) => now - started >= PadDirectStep;

    /// <summary>
    /// A fizzled door recall is cast again while casts are left and the wait for the next
    /// cast has not run out.
    /// </summary>
    public static bool RecallsAgain(int casts, DateTime now, DateTime retryEnds) =>
        casts < DoorRecallCasts && now < retryEnds;

    /// <summary>
    /// A walk inside that failed while the person stands in the dungeon starts the crawl
    /// where it stands. The catalog hall of Trinsic Passage and of Deceit lies on a floor
    /// no walk from the door reaches, and ten runs a quarter hour ended at the landing.
    /// </summary>
    public static bool CrawlsWhereItStands(DungeonLeg leg, bool inside, SkillStatus status) =>
        inside && status == SkillStatus.Failed && leg is DungeonLeg.WalkInside or DungeonLeg.WalkToHall;

    /// <summary>A person carried out of the dungeon by a pad inside goes back in by the door while re-entries are left.</summary>
    public static bool Reenters(bool reachedInside, bool inside, int reentries) =>
        reachedInside && !inside && reentries < MaxReentries;

    /// <summary>True when the walk to the door is long enough that a lone traveller looks for a nearer dungeon.</summary>
    public static bool WalkIsLong(int walkTiles) => walkTiles >= LongWalkTiles;

    /// <summary>Why a trip walks although its person carries a door rune: every cast of the recall fizzled.</summary>
    public const string FizzledWhy = "the door recall fizzled";

    /// <summary>The line a trip that a pad carried out writes as it goes back in.</summary>
    public static string ReenterLine(string name, string dungeon) =>
        $"{name} was carried out of {dungeon} by a pad and goes back in";

    /// <summary>The line a trip that walks a long way to its door writes, with why no recall took it.</summary>
    public static string WalkLine(string name, string dungeon, int tiles, string why) =>
        $"{name} walks {tiles} tiles to the door of {dungeon}: {why}";

    /// <summary>The line a lone traveller that turns to a nearer dungeon writes.</summary>
    public static string NearerLine(string name, string from, string to) =>
        $"{name} turns from the long walk to {from} to the nearer {to}";

    /// <summary>
    /// True while the person stands too far from the pad for the step to find it: it walks
    /// up to the pad first, inside the step's time.
    /// </summary>
    public static bool NeedsApproach(Point3D at, Point3D pad) =>
        !GatePad.IsInReach(at, pad);

    /// <summary>
    /// The real teleporter pad nearest <paramref name="from"/>, within
    /// <paramref name="searchTiles"/>, that lands where the walker wants: inside the dungeon
    /// for the way in from its door, outside every dungeon for the way out. Else
    /// <see cref="NoPad"/>. The pads come from the world, not the graph: the graph joins a
    /// pad and its landing both ways, and the Orc Cave exit landing (1014,1434) was once
    /// taken for the way in, so the step found no pad under it twenty times in a run.
    /// </summary>
    public static int PickPad(
        Point3D from,
        IReadOnlyList<(Point3D Pad, bool LandsWanted)> links,
        int searchTiles = PadSearchTiles
    )
    {
        var best = NoPad;
        var bestDistance = int.MaxValue;

        for (var i = 0; i < (links?.Count ?? 0); i++)
        {
            var distance = NavMetric.Chebyshev(from, links[i].Pad);

            if (!links[i].LandsWanted || distance > searchTiles || distance >= bestDistance)
            {
                continue;
            }

            best = i;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>Why a trip failed on <paramref name="leg"/>, for the activity log.</summary>
    public static string FailureReason(DungeonLeg leg) =>
        leg switch
        {
            DungeonLeg.WalkToDoor => DelveFailure.NoWayToDoor,
            DungeonLeg.StepOnPad => DelveFailure.DoorPadRefused,
            DungeonLeg.WalkInside => DelveFailure.NoWayInside,
            _ => DelveFailure.NoWayToHall
        };
}
