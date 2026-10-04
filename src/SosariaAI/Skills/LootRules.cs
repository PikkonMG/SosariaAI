using System;

namespace SosariaAI.Skills;

/// <summary>
/// What a hunter takes off a monster it killed, the way a 1999 player cleared a corpse:
/// gold, reagents, gems, scrolls, potions, jewels and magic gear, plus bandages and arrows
/// to keep fighting. Plain hides, bones and junk stay. The pack stops short of overload, since
/// an overloaded player cannot walk. Pure.
/// </summary>
public static class LootRules
{
    /// <summary>A corpse opens from this many tiles (the engine's corpse range).</summary>
    public const int CorpseReachTiles = 2;

    /// <summary>A corpse lies within this many tiles of where its creature last stood.</summary>
    public const int CorpseSearchTiles = 4;

    /// <summary>A hunter does not walk further than this for a corpse.</summary>
    public const int CorpseWalkTiles = 16;

    /// <summary>A hunter sweeps the ground this far out for other kills while it is not fighting.</summary>
    public const int GroundCorpseTiles = 10;

    /// <summary>How often the ground sweep runs.</summary>
    public static readonly TimeSpan GroundScanPause = TimeSpan.FromSeconds(3);

    /// <summary>Loot stops when the carried weight would pass this share of the most the body carries.</summary>
    public const double CarryFraction = 0.85;

    /// <summary>A corpse that cannot be reached in this long is left.</summary>
    public static readonly TimeSpan WalkLimit = TimeSpan.FromSeconds(30);

    public static bool Wants(LootKind kind, bool isSupply) => isSupply || kind != LootKind.Other;

    public static bool HasRoom(int carried, int maxWeight, int itemWeight) =>
        maxWeight > 0 && carried + Math.Max(0, itemWeight) <= maxWeight * CarryFraction;

    public static bool WorthTheWalk(int distance) => distance <= CorpseWalkTiles;

    public static bool TooLong(DateTime now, DateTime started) =>
        started != default && now - started >= WalkLimit;
}
