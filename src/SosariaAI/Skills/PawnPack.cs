using Server;
using Server.Items;

namespace SosariaAI.Skills;

/// <summary>
/// What a hunter carries home to pawn. Gold rides to the bank and fighting supplies stay in
/// the pack, so they are never pawn goods. Which shop takes a piece is the shops' own buy
/// lists' answer (<see cref="SaleGoods"/>), not the piece's kind.
/// </summary>
public static class PawnPack
{
    /// <summary>
    /// Anything a corpse run brings home that a shop might buy: gear, jewels, gems,
    /// scrolls and potions. Supplies the hunter burns and gold are not pawn goods.
    /// </summary>
    public static bool IsPawnable(Item item) =>
        item is { Deleted: false } &&
        StealRules.KindOf(item) != LootKind.Other &&
        item is not Gold &&
        !HawkerGoods.IsSupply(item);

    /// <summary>
    /// Loot nobody will buy and nobody banks: plain gear. Worthless drops like bones
    /// and patches never enter the pack in the first place.
    /// </summary>
    public static bool IsJunkable(Item item) =>
        item is { Deleted: false } && StealRules.KindOf(item) == LootKind.Gear;
}
