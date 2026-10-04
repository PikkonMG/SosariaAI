using System;
using System.Collections.Generic;

namespace SosariaAI.Economy;

/// <summary>What a trader has to say.</summary>
public enum TradeLineKind
{
    Stock,
    Price,
    SellerCounter,
    SellerFirm,
    SellerAccept,
    SellerWalk,
    BuyerOpen,
    BuyerCounter,
    BuyerFirm,
    BuyerAccept,
    BuyerWalk,
    Insulted,
    Thanks,
    WrongGold,
    WrongGoods,
    Timeout,
    Interested,
    AnswerWant,
    HaveOne,
    NotMine,
    ShortOfGold,
    Released
}

/// <summary>
/// Short bank-floor lines, typed the way 1999 typed them. {noun} is the goods, {price} the
/// speaker's number and {theirs} the other side's, both said as "4k" or "750". Pure.
/// </summary>
public static class TradeLines
{
    private const string NounSlot = "{noun}";
    private const string PriceSlot = "{price}";
    private const string TheirsSlot = "{theirs}";

    private static readonly Dictionary<TradeLineKind, string[]> Lines = new()
    {
        [TradeLineKind.Stock] = ["got a {noun}, {price}", "{noun}. {price}", "just the {noun}, {price} gold", "{noun} for {price}"],
        [TradeLineKind.Price] = ["{price}", "{price} for the {noun}", "{price} gold", "{price}, its a good one"],
        [TradeLineKind.SellerCounter] = ["cant do {theirs}. {price}", "{price} and its yours", "meet me at {price}", "lowest {price}"],
        [TradeLineKind.SellerFirm] = ["{price} is firm", "cant go lower than {price}", "{price}, no less"],
        [TradeLineKind.SellerAccept] = ["deal. drop {price} on me", "k, {price}. drop the gold on me", "sold, {price}. drop it on me"],
        [TradeLineKind.SellerWalk] = ["nah, ill keep it", "no thanks, ill find another buyer", "cant do it, sorry"],
        [TradeLineKind.BuyerOpen] = ["{price}?", "id give {price} for it", "{price} for the {noun}?"],
        [TradeLineKind.BuyerCounter] = ["{theirs}? {price}", "ill go {price}", "how about {price}"],
        [TradeLineKind.BuyerFirm] = ["{price} is my max", "{price}, thats all i got", "cant do more than {price}"],
        [TradeLineKind.BuyerAccept] = ["deal, drop it on me", "k {price}, hand it over", "done, drop the {noun} on me"],
        [TradeLineKind.BuyerWalk] = ["too rich for me", "nah im good", "ill pass"],
        [TradeLineKind.Insulted] = ["lol no", "lol", "not even close"],
        [TradeLineKind.Thanks] = ["ty", "ty, pleasure", "thx", "enjoy"],
        [TradeLineKind.WrongGold] = ["thats {theirs}, we said {price}", "{price} not {theirs}", "count again, {price}"],
        [TradeLineKind.WrongGoods] = ["thats not a {noun}", "we said {noun}", "not what u said"],
        [TradeLineKind.Timeout] = ["guess not", "nvm then", "k, forget it"],
        [TradeLineKind.Interested] = ["whats the {noun}? ill have a look", "{noun}? 1 sec", "hold on, coming"],
        [TradeLineKind.AnswerWant] = ["yea im buying, 1 sec", "k one sec", "coming"],
        [TradeLineKind.HaveOne] = ["i got a {noun}, {price}", "have a {noun} here. {price}", "{noun}? got one, {price}"],
        [TradeLineKind.NotMine] = ["not mine", "whats this for?", "here, keep it"],
        [TradeLineKind.ShortOfGold] = ["hold on, short on gold", "dont have it on me, sorry"],
        [TradeLineKind.Released] = ["np", "k", "no worries"]
    };

    public static string For(TradeLineKind kind, int roll, string noun, int price, int theirs)
    {
        var options = Lines[kind];
        var template = options[Math.Abs(roll) % options.Length];

        return template
            .Replace(NounSlot, noun ?? string.Empty, StringComparison.Ordinal)
            .Replace(PriceSlot, GoldWords.Spoken(price), StringComparison.Ordinal)
            .Replace(TheirsSlot, GoldWords.Spoken(theirs), StringComparison.Ordinal);
    }

    /// <summary>
    /// True when a reworded line names exactly the numbers the plain one did. A model may say a
    /// line its own way, never at another price.
    /// </summary>
    public static bool SameNumbers(string plain, string phrased)
    {
        if (string.IsNullOrWhiteSpace(phrased))
        {
            return false;
        }

        var want = Values(plain);
        var got = Values(phrased);
        return want.SetEquals(got);
    }

    private static HashSet<int> Values(string line)
    {
        var values = new HashSet<int>();

        foreach (var number in GoldWords.NumbersIn(TradeParser.Words(line)))
        {
            values.Add(number.Value);
        }

        return values;
    }
}
