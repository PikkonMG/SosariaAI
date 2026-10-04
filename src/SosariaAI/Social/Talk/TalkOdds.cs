namespace SosariaAI.Social;

/// <summary>
/// How often each situation speaks. Percents apply where a person at a keyboard can hear; far
/// from every client the chance is divided so 1600 characters do not fill the log. Pure.
/// </summary>
public static class TalkOdds
{
    public const int PercentScale = 100;

    /// <summary>Unheard, a situation speaks this many times less often.</summary>
    public const int UnheardDivisor = 10;

    /// <summary>After a situational line a person keeps quiet this long before the next one.</summary>
    public const int SpeakerRestMs = 45_000;

    public const int EngagePercent = 35;
    public const int AssistPercent = 35;
    public const int PullPercent = 25;
    public const int FleePercent = 50;
    public const int TurnPercent = 30;
    public const int ClearPercent = 30;
    public const int VictoryPercent = 30;
    public const int LootHaulPercent = 30;
    public const int HuntEndPercent = 40;
    public const int DungeonEnterPercent = 20;
    public const int DungeonLeavePercent = 20;
    public const int CraftDonePercent = 15;
    public const int CraftStartPercent = 10;

    /// <summary>A crafter out of stock or without its tool asks the room for it.</summary>
    public const int CraftNeedPercent = 40;

    /// <summary>While it waits at the station for stock, each look round may ask again.</summary>
    public const int CraftNeedRepeatPercent = 10;
    public const int GatherHaulPercent = 15;
    public const int TameSuccessPercent = 40;
    public const int TameFailPercent = 25;
    public const int TreasureDecodedPercent = 30;
    public const int TreasureLootPercent = 35;

    /// <summary>
    /// A working or wandering person says a line about its work about every 25 minutes in
    /// earshot: 1 in 600 scans. Once an hour read as silence at a ten-person bank.
    /// </summary>
    public const int WorkTalkOdds = 600;

    /// <summary>
    /// A heard person mutters a line of its own persona about every 25 minutes: 1 in 600
    /// scans. Only rolls when a keyboard is near; an unheard mutter fills the log, not the room.
    /// </summary>
    public const int IdleTalkOdds = 600;

    /// <summary>A heard person makes a small gesture about every 40 minutes: 1 in 900 scans.</summary>
    public const int EmoteOdds = 900;

    /// <summary>Outdoors at the game's night a person remarks on it about every half hour: 1 in 7,200 scans.</summary>
    public const int NightTalkOdds = 7_200;

    /// <summary>The chance, out of <see cref="PercentScale"/>, for a situation heard or unheard.</summary>
    public static int Chance(int percent, bool heard) => heard ? percent : percent / UnheardDivisor;
}
