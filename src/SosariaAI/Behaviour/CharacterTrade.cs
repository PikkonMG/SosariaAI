using Server;
using Server.Items;
using SosariaAI.Economy;
using SosariaAI.Mobiles;

namespace SosariaAI.Behaviour;

/// <summary>
/// Two characters close a deal the way two players did: the engine's secure-trade window with
/// the goods on the seller's side and the coin on the buyer's, both boxes checked, and the
/// engine's own swap puts each side into the other's pack. Neither side has a client, so the
/// window is built by hand as <see cref="TradeWindow"/> builds one for a person, and the packets
/// aimed at the missing clients no-op. A swap the engine refuses folds the window, and each side
/// takes its own back. With trading switched off on the shard, the goods and coin change hands
/// bare (<see cref="TradeHandOff"/>). No coin is made or lost: the buyer's coin leaves its pack
/// (its bank drawn aloud first when short) and the same sum reaches the seller. World thread only.
/// </summary>
public static class CharacterTrade
{
    /// <summary>
    /// Moves <paramref name="goods"/> from the seller's pack to the buyer's and
    /// <paramref name="price"/> gold the other way. False when either side is missing, the goods
    /// left the seller's pack, or the buyer cannot pay; nothing moves then.
    /// </summary>
    public static bool Swap(SosariaCharacter seller, Item goods, SosariaCharacter buyer, int price)
    {
        if (seller?.Backpack == null || buyer?.Backpack == null || seller == buyer || price <= 0 ||
            goods is not { Deleted: false } || !goods.IsChildOf(seller.Backpack))
        {
            return false;
        }

        return ServerFeatureFlags.PlayerTrading ? SwapInWindow(seller, goods, buyer, price) : SwapBare(seller, goods, buyer, price);
    }

    private static bool SwapInWindow(SosariaCharacter seller, Item goods, SosariaCharacter buyer, int price)
    {
        if (!TradeHandOff.DrawGold(buyer, price))
        {
            return false;
        }

        var trade = new SecureTrade(seller, buyer);
        trade.From.Container.DropItem(goods);
        TradeHandOff.HandOutGold(price, trade.To.Container.DropItem);
        trade.From.Accepted = true;
        trade.To.Accepted = true;
        trade.Update();

        if (trade.Valid)
        {
            trade.Cancel();
            return false;
        }

        TradeTally.NoteDeal(price, throughWindow: true);
        return true;
    }

    private static bool SwapBare(SosariaCharacter seller, Item goods, SosariaCharacter buyer, int price)
    {
        if (!TradeHandOff.Pay(buyer, seller, price))
        {
            return false;
        }

        TradeHandOff.Give(buyer, goods);
        TradeTally.NoteDeal(price, throughWindow: false);
        return true;
    }
}
