using System;
using SosariaAI.Common;

namespace SosariaAI.Behaviour;

/// <summary>What a scene is, in one word, for the activity log.</summary>
public enum SceneKind
{
    ShoutAnswer,
    GoingAsk,
    FriendChain,
    DeathJoke,
    LootedReply,
    DuelStart,
    DuelEnd,
    RedAlert,
    CraftCustomer,
    PartyDepart,
    PartyReturn,
    TavernNight
}

/// <summary>
/// Timing and limits for scenes. A scene near a person at a keyboard may start when the shard
/// has room for one more and, for an everyday scene, when that spot has rested; far from every
/// client one scene starts in a while, shard-wide. Pure.
/// </summary>
public static class SceneRules
{
    /// <summary>Scenes playing at once, shard-wide.</summary>
    public const int MaxActive = 24;

    /// <summary>An actor farther than this from where the scene began has walked off; the scene ends.</summary>
    public const int StageRange = 16;

    /// <summary>Everyday scenes rest per square of this many tiles.</summary>
    public const int CellTiles = 32;

    /// <summary>Most bystanders one scene draws in.</summary>
    public const int MaxBystanders = 2;

    /// <summary>Bystanders stand this close to the one a scene is about.</summary>
    public const int BystanderRange = 10;

    /// <summary>A shopper stands this close to the crafter.</summary>
    public const int ShopperRange = 6;

    public const int MinGapMs = 2_000;
    public const int GapSpreadMs = 3_000;

    /// <summary>Onlookers speak once the duelists have walked a few steps.</summary>
    public const int DuelWatchMs = 5_000;

    public const int ShoutAnswerPercent = 35;
    public const int MeetingScenePercent = 3;
    public const int DeathJokePercent = 50;
    public const int LootedReplyPercent = 60;
    public const int DuelStartPercent = 70;
    public const int DuelEndPercent = 60;
    public const int RedAlertPercent = 80;
    public const int CraftCustomerPercent = 25;
    public const int PartyDepartPercent = 60;
    public const int PartyReturnPercent = 50;
    public const int TavernNightPercent = 40;

    /// <summary>One try in this many world scans for an everyday scene started away from the meeting scan.</summary>
    public const int IdleSceneOdds = 6_000;

    /// <summary>UO's night runs from this game hour to <see cref="NightEndsHour"/>.</summary>
    public const int NightStartsHour = 22;

    public const int NightEndsHour = 5;

    /// <summary>An everyday scene does not start again in one square before this.</summary>
    public static readonly TimeSpan PlaceRest = TimeSpan.FromMinutes(2);

    /// <summary>Far from every client, one scene in this time, shard-wide.</summary>
    public static readonly TimeSpan UnheardGap = TimeSpan.FromMinutes(2);

    /// <summary>A typed answer's delay: two to five seconds by the roll.</summary>
    public static TimeSpan Gap(int roll) => TimeSpan.FromMilliseconds(MinGapMs + Math.Abs(roll % GapSpreadMs));

    public static bool MayStart(
        bool heard,
        bool everyday,
        int active,
        DateTime lastAtPlace,
        DateTime lastUnheard,
        DateTime now
    )
    {
        if (active >= MaxActive)
        {
            return false;
        }

        if (!heard)
        {
            return TimeRules.Rested(lastUnheard, now, UnheardGap);
        }

        return !everyday || TimeRules.Rested(lastAtPlace, now, PlaceRest);
    }

    public static bool IsNight(int gameHour) => gameHour >= NightStartsHour || gameHour < NightEndsHour;

    public static (int X, int Y) Cell(int x, int y) => (x / CellTiles, y / CellTiles);
}
