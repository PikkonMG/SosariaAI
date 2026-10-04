using Server;
using Server.Items;
using SosariaAI.Economy;

namespace SosariaAI.Skills;

/// <summary>
/// What a worker carries from its work, and which of it a town shop will buy. A
/// carpenter buys plain logs only, not oak or ash, so those go to the bank instead of
/// being offered for sale again and again.
/// </summary>
public static class HarvestPack
{
    public static bool IsHarvest(Item item) => item is Log or BaseOre or BaseIngot or Fish;

    public static bool IsSellable(Item item) =>
        IsHarvest(item) && VendorSellRules.IsSellableHarvest(item.GetType().Name);
}
