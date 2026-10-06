using System;
using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Deliberation;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>
/// Goods and gold changing hands without a trade window — the fallback for a person whose window
/// cannot open, and the road every drop on a character takes first. A person drops the gold or
/// the goods on the character and the character puts its side into that person's pack, or at
/// their feet when the pack is full. A character pays from its own pack, drawing the rest from
/// its bank aloud when a banker hears it. Anything dropped outside a deal goes back where it
/// came from: the engine bounces an item a mobile refuses. Nothing is ever kept or deleted.
/// World thread only.
/// </summary>
public static class TradeHandOff
{
    /// <summary>The largest pile of gold one item holds.</summary>
    public const int GoldPileMax = 60000;

    /// <summary>
    /// What a character can pay across the bank floor: its pack gold, plus what one "withdraw"
    /// brings when a banker is in earshot.
    /// </summary>
    public static int Purse(SosariaCharacter character) =>
        character == null || BankTeller.FindBanker(character) == null
            ? character?.Backpack?.GetAmount(typeof(Gold)) ?? 0
            : PurseAtBank(character);

    /// <summary>
    /// What a character will pay across a bank floor once it stands there: its pack gold plus
    /// what one "withdraw" brings. A crafter at its station judges a trip to the bank by this.
    /// </summary>
    public static int PurseAtBank(SosariaCharacter character)
    {
        var pack = character?.Backpack?.GetAmount(typeof(Gold)) ?? 0;
        return character == null ? pack : pack + Math.Min(Banker.GetBalance(character), BankTellerRules.WithdrawCeiling(Core.ML));
    }

    /// <summary>
    /// Takes <paramref name="price"/> out of the payer's pack; a short pack draws the rest at the
    /// banker first. False when the gold is not there; nothing moves then. The caller owns the
    /// coin now and says where it lands.
    /// </summary>
    public static bool DrawGold(SosariaCharacter payer, int price)
    {
        var pack = payer?.Backpack;

        if (pack == null || price <= 0)
        {
            return false;
        }

        var shortfall = price - pack.GetAmount(typeof(Gold));

        BankTeller.Withdraw(payer, shortfall);

        return pack.ConsumeTotal(typeof(Gold), price);
    }

    /// <summary>
    /// Pays <paramref name="price"/> from the payer's pack into the payee's pack. False when the
    /// gold is not there; nothing moves then.
    /// </summary>
    public static bool Pay(SosariaCharacter payer, Mobile payee, int price)
    {
        if (payee == null || !DrawGold(payer, price))
        {
            return false;
        }

        HandOutGold(price, pile => Give(payee, pile));
        return true;
    }

    /// <summary>
    /// Makes <paramref name="amount"/> gold as piles no bigger than <see cref="GoldPileMax"/>
    /// and hands each to <paramref name="place"/>. The caller drew the same sum first.
    /// </summary>
    public static void HandOutGold(int amount, Action<Item> place)
    {
        for (var left = amount; left > 0; left -= GoldPileMax)
        {
            place(new Gold(Math.Min(left, GoldPileMax)));
        }
    }

    /// <summary>Into the pack, or at the feet when the pack cannot take it.</summary>
    public static void Give(Mobile to, Item item)
    {
        if (to?.Backpack == null || !to.Backpack.TryDropItem(to, item, false))
        {
            item.MoveToWorld(to?.Location ?? Point3D.Zero, to?.Map);
        }
    }

    /// <summary>
    /// A person dropped <paramref name="dropped"/> on the character. True when a live deal took
    /// it; false hands it back. Goods from someone the character cannot see go back unremarked.
    /// </summary>
    public static bool Receive(SosariaCharacter character, Mobile from, Item dropped)
    {
        if (!People.Perceives(character, from))
        {
            return false;
        }

        var session = TradeSessions.Find(character, from);

        if (session != null)
        {
            return session.Receive(dropped);
        }

        TradeVoice.Say(character, from, TradeLines.For(TradeLineKind.NotMine, Utility.Random(int.MaxValue), null, 0, 0));
        return false;
    }
}
