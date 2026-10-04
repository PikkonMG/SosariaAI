using System;
using Server;

namespace SosariaAI.Economy;

/// <summary>Whether goods are priced as a piece of gear (quality and magic count) or as a stack (the count counts).</summary>
public enum GoodsKind
{
    Weapon,
    Armor,
    Stack
}

/// <summary>Who at a bank has a use for goods. A merchant buys anything to sell on.</summary>
[Flags]
public enum TradeAppetite
{
    None = 0,
    Caster = 1 << 0,
    Melee = 1 << 1,
    Fencing = 1 << 2,
    Archery = 1 << 3,
    Healing = 1 << 4,
    Smithing = 1 << 5,
    Tailoring = 1 << 6,
    Carpentry = 1 << 7,

    /// <summary>Goods anybody might pick up: potions, gems, clothes.</summary>
    Everyone = 1 << 8,

    /// <summary>A person who travels by recall and has no runebook yet: a scribe's runebook.</summary>
    Travel = 1 << 9,

    Merchant = Caster | Melee | Fencing | Archery | Healing | Smithing | Tailoring | Carpentry | Everyone | Travel
}

/// <summary>
/// One line of the market table: what the goods are called and typed, the unit band in gold,
/// the usual lot, who wants them and how often a bank crowd bites. <see cref="Holds"/> tells a
/// real item of this kind.
/// </summary>
public sealed record GoodsRow(
    string Key,
    string Noun,
    GoodsKind Kind,
    int UnitLow,
    int UnitHigh,
    int Lot,
    TradeAppetite Appetite,
    int DemandPercent,
    string[] Words,
    Func<Item, bool> Holds
)
{
    public bool IsGear => Kind != GoodsKind.Stack;
}
