using System;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>What the corpse run does next, looked at again after every walk.</summary>
public enum CorpseRunStep
{
    Reclaim,
    CloseWalk,
    LongWalk,
    GiveUp,

    /// <summary>A red leaves the body and recalls home to the Den, where its spare kit waits in the bank.</summary>
    RecallHome
}

/// <summary>
/// The corpse run after any raise: the ghost's own way back, a player's spell, a healer or
/// another character. The person walks back to its own body (never a crime, a red too), takes
/// its belongings, dresses again within its class armor limit and goes on. A body that rotted
/// or was emptied, or one it cannot reach, sends it to its bank and the shops for a new kit.
/// 217 of 272 raises in one run never looted the body: the walk stopped one tile past the
/// loot reach, and a red raised beside its body in town would not walk into the guards.
/// </summary>
public static class CorpseRunRules
{
    /// <summary>A body is looted from this close, as a player's hands reach it.</summary>
    public const int LootRange = 2;

    /// <summary>A walk aims one tile inside the loot reach: its last leg may stop a tile short.</summary>
    public const int WalkRange = 1;

    /// <summary>From this close the rest is walked tile by tile, straight at the body.</summary>
    public const int CloseWalkTiles = 30;

    /// <summary>Walks begun toward the body before the run gives it up.</summary>
    public const int MaxWalks = 4;

    /// <summary>Shop trips for lost pieces, one piece each.</summary>
    public const int MaxTownBuys = 4;

    public const string OtherFacetReason = "it lies on another facet";
    public const string NoWayReason = "no walk reached it";
    public const string TooLongReason = "the run took too long";
    public const string UnderGuardsReason = "it lies under the guards";
    public const string NoGuardFreeWalkReason = "no walk clear of the guards reaches it";

    /// <summary>The whole walk back is given up after this long.</summary>
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(10);

    /// <summary>
    /// A missing body is looked at where it was (and found gone); one in reach is looted; one
    /// on another facet, or past the walks or the time, is given up; else the next walk. A
    /// red (<paramref name="murderer"/>) goes for the body on its own facet clear of the guards,
    /// close by or on the long walk (its every step keeps off guarded ground); anything else, and
    /// a walk that runs out of tries or time, sends it home to the Den by recall. 54 reds gave up
    /// a body under the guards in one morning: each walk toward it was refused, four refusals
    /// burned the run in six seconds, and the red went out again naked. Sent home whenever the
    /// body lay past the close walk, as one raised at a far ankh always is, 411 of 412 reds left
    /// their gear on the body and 3 in 156 got it back, against 2 in 3 blues: the spare kit
    /// emptied, and the Den filled with naked reds.
    /// </summary>
    public static CorpseRunStep Next(
        bool corpseFound,
        bool sameMap,
        int distance,
        int walks,
        TimeSpan elapsed,
        bool murderer = false,
        bool bodyUnderGuards = false
    )
    {
        if (!corpseFound || sameMap && distance <= LootRange)
        {
            return CorpseRunStep.Reclaim;
        }

        if (murderer)
        {
            return !sameMap || bodyUnderGuards || walks >= MaxWalks || elapsed >= GiveUpAfter
                ? CorpseRunStep.RecallHome
                : distance <= CloseWalkTiles ? CorpseRunStep.CloseWalk : CorpseRunStep.LongWalk;
        }

        if (!sameMap || walks >= MaxWalks || elapsed >= GiveUpAfter)
        {
            return CorpseRunStep.GiveUp;
        }

        return distance <= CloseWalkTiles ? CorpseRunStep.CloseWalk : CorpseRunStep.LongWalk;
    }

    /// <summary>A close walk found no tile route: a red goes home by recall, anyone else travels to the body.</summary>
    public static CorpseRunStep AfterNoCloseRoute(bool murderer) =>
        murderer ? CorpseRunStep.RecallHome : CorpseRunStep.LongWalk;

    /// <summary>Why the run gave the body up, for the log.</summary>
    public static string WhyGiveUp(bool sameMap, int walks) =>
        !sameMap ? OtherFacetReason : walks >= MaxWalks ? NoWayReason : TooLongReason;

    /// <summary>Why a red left its body for the Den, for the log.</summary>
    public static string WhyRecallHome(bool sameMap, bool bodyUnderGuards, int walks) =>
        !sameMap ? OtherFacetReason
        : bodyUnderGuards ? UnderGuardsReason
        : walks >= MaxWalks ? NoWayReason
        : TooLongReason;

    /// <summary>
    /// A red's close walk to its body: <paramref name="walker"/>, but never a step onto a tile
    /// <paramref name="guarded"/> names, so the tile route keeps to guard-free ground or finds
    /// none. Pure.
    /// </summary>
    public static TileWalker OffGuards(TileWalker walker, Func<int, int, int, bool> guarded) =>
        walker with
        {
            Step = (int fromX, int fromY, int fromZ, int toX, int toY, out int toZ) =>
                walker.Step(fromX, fromY, fromZ, toX, toY, out toZ) && !guarded(toX, toY, toZ)
        };

    /// <summary>
    /// A red heading home recalls only from farther than a rune's landing walk
    /// (<paramref name="nearTiles"/>) from the Den's bank, and only when the Den's bank is
    /// known; closer, it walks the rest to the bank.
    /// </summary>
    public static bool RecallsHome(bool denKnown, int tilesFromDen, int nearTiles) =>
        denKnown && tilesFromDen > nearTiles;

    /// <summary>A body that rotted or was emptied is lost: the owner shouts and buys its kit again.</summary>
    public static bool BodyLost(CorpseReclaimResult result) =>
        result is CorpseReclaimResult.Decayed or CorpseReclaimResult.Stripped;

    /// <summary>
    /// The bank and the shops follow the body: a lost body, or one that gave back no arms. A
    /// killer that looted the weapon left the clothes, the body counted as found, and the blue
    /// went on with its fists: 243 bodies of one run gave back 3 items.
    /// </summary>
    public static bool ShopsAfterReclaim(bool bodyLost, bool armed) => bodyLost || !armed;

    /// <summary>
    /// After the bank a person with its body lost buys its kit back piece by piece: another
    /// trip while the last one bought something and the count allows it.
    /// </summary>
    public static bool BuysAgain(bool bought, int buys) => bought && buys < MaxTownBuys;

    /// <summary>
    /// A raise outside the ghost's own way back still runs to the body. Not the stand-up at
    /// home, which took the gear back already; not while the ghost step runs, which walks there
    /// itself; and not with no body left at death.
    /// </summary>
    public static bool RunsAfterRaise(bool standUpAtHome, bool ghostStepRunning, bool diedWithBody) =>
        !standUpAtHome && !ghostStepRunning && diedWithBody;
}
