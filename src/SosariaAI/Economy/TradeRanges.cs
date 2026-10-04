using System;

namespace SosariaAI.Economy;

/// <summary>
/// How far trade talk reaches. A haggle is face to face; a WTS or WTB shout carries across a
/// bank floor; goods and gold change hands only at arm's length, the engine's drop reach.
/// </summary>
public static class TradeRanges
{
    /// <summary>Close enough to talk money.</summary>
    public const int TalkRange = 6;

    /// <summary>The engine lets a person drop an item on someone this close.</summary>
    public const int DealRange = 2;

    /// <summary>A WTS or WTB shout reaches this far.</summary>
    public const int ShoutRange = 16;

    /// <summary>A buyer crosses at most this far for a shout.</summary>
    public const int WalkOverRange = 12;

    /// <summary>A haggle nobody comes back to goes cold after this many seconds.</summary>
    public const int IdleSeconds = 60;

    /// <summary>A settled price waits this long for the goods or the gold.</summary>
    public const int HandOffSeconds = 120;

    /// <summary>A buyer that cannot reach the seller in this long gives up the walk.</summary>
    public const int ApproachSeconds = 45;

    public static readonly TimeSpan Idle = TimeSpan.FromSeconds(IdleSeconds);
    public static readonly TimeSpan HandOff = TimeSpan.FromSeconds(HandOffSeconds);
    public static readonly TimeSpan Approach = TimeSpan.FromSeconds(ApproachSeconds);
}
