using Server;
using Server.Items;
using Server.Mobiles;

namespace SosariaAI.Economy;

/// <summary>
/// A vendor's OnBuyItems takes payment from the buyer's pack, and only an order of
/// <see cref="VendorBankPayMin"/> gold or more is drawn from the bank by the vendor itself.
/// Gold never jumps from the bank to the pack at a shop counter: a buyer short of pack gold
/// visits a banker and says "withdraw" first.
/// </summary>
public static class PackFunds
{
    /// <summary>BaseVendor draws an order this large from the buyer's bank box.</summary>
    public const int VendorBankPayMin = 2000;

    /// <summary>True when the vendor can take <paramref name="cost"/> from what the buyer holds.</summary>
    public static bool FundPack(Mobile buyer, int cost)
    {
        var pack = buyer?.Backpack;

        if (pack == null)
        {
            return false;
        }

        return MayPay(pack.GetAmount(typeof(Gold)), Banker.GetBalance(buyer), cost);
    }

    public static bool MayPay(int packGold, int bankGold, int cost) =>
        cost > 0 && (packGold >= cost || cost >= VendorBankPayMin && bankGold >= cost);
}
