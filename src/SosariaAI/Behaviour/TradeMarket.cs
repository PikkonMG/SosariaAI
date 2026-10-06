using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Deliberation;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using SosariaAI.Spawning;

namespace SosariaAI.Behaviour;

/// <summary>A WTB a character shouted and can pay for: the goods and when it stops listening.</summary>
public readonly record struct TradeWant(GoodsClaim Claim, DateTime Until);

/// <summary>
/// Who trades with whom on a bank floor: the seller a person's question is aimed at, the buyer
/// that crosses the floor for a WTS, the WTB an "i have one" answers, the hawker a character
/// with coin walks up to, and the bystander idling on the floor who answers a character's own
/// WTS or WTB (<see cref="FloorDeal"/>). It also keeps the verdicts that stick: a room that passed on a seller's
/// goods, and a character that already said no to someone. World thread only.
/// </summary>
public static class TradeMarket
{
    public const int NamedWeight = 1000;
    public const int GoodsWeight = 100;

    /// <summary>Past this many remembered verdicts, the stale ones are let go.</summary>
    public const int PruneAbove = 256;

    private static readonly Dictionary<(Serial Seller, string Row), (bool Bites, DateTime Until)> RoomVerdicts = new();
    private static readonly Dictionary<(Serial Character, Serial Other), DateTime> Refusals = new();
    private static readonly Dictionary<Serial, TradeWant> Wants = new();

    /// <summary>The character said no to this person or character; it will not bite again for a while.</summary>
    public static void NoteRefusal(SosariaCharacter character, Mobile other, GoodsClaim claim)
    {
        if (character == null || other == null)
        {
            return;
        }

        var now = Core.Now;
        var until = now + TradeDemandRules.RefusalHold;
        Prune(now);
        Refusals[(character.Serial, other.Serial)] = until;

        if (claim.Row != null)
        {
            RoomVerdicts[(other.Serial, claim.Row.Key)] = (false, until);
        }
    }

    /// <summary>
    /// The seller a person's trade question is aimed at: a character in talking range holding goods
    /// up, the one named first, then one selling the goods named, then the nearest. A crafter quotes
    /// the piece of its shop stock the person named, not only the one held up.
    /// </summary>
    public static (SosariaCharacter Seller, Item Goods, HawkerOffer Offer)? SellerFor(Mobile speaker, string text, GoodsClaim? goods)
    {
        (SosariaCharacter, Item, HawkerOffer)? best = null;
        var bestScore = int.MinValue;

        foreach (var mobile in speaker.GetMobilesInRange(TradeRanges.TalkRange))
        {
            if (mobile is not SosariaCharacter seller || !Present(seller) || TradeSessions.IsBusy(seller) ||
                !BankCrowd.TryGetHawkerOffer(seller, out var offer) || World.FindItem(offer.Item) is not { } item ||
                !item.IsChildOf(seller.Backpack) || !speaker.CanSee(seller) || !People.Perceives(seller, speaker))
            {
                continue;
            }

            var (piece, asking) = goods is { Piece: not ArmorPiece.FullSet } named && ShopStock.Named(seller, named) is { } match
                ? (match, ShopStock.AskingOf(match))
                : (item, offer.Asking);

            var score = SellerScore(
                AttentionGate.MentionsName(text, seller.Name),
                goods is { } wanted && wanted.Row == Appraisal.RowOf(piece),
                (int)speaker.GetDistanceToSqrt(seller)
            );

            if (score > bestScore)
            {
                bestScore = score;
                best = (seller, piece, piece == item ? offer : new HawkerOffer(piece.Serial, asking, Appraisal.NounOf(piece)));
            }
        }

        return best;
    }

    /// <summary>
    /// The crafter within walking range of a person's WTB whose shop stock holds the goods wanted,
    /// and the piece; the nearest wins. Null when none does.
    /// </summary>
    public static (SosariaCharacter Crafter, Item Piece)? StockFor(Mobile speaker, GoodsClaim wanted)
    {
        // A whole suit is an order, not one piece off the shelf.
        if (wanted.Piece == ArmorPiece.FullSet)
        {
            return null;
        }

        (SosariaCharacter, Item)? pick = null;
        var best = double.MaxValue;

        foreach (var mobile in speaker.GetMobilesInRange(TradeRanges.WalkOverRange))
        {
            if (mobile is not SosariaCharacter crafter || !Present(crafter) || TradeSessions.IsBusy(crafter) ||
                !People.Perceives(crafter, speaker) || ShopStock.Named(crafter, wanted) is not { } piece)
            {
                continue;
            }

            var distance = speaker.GetDistanceToSqrt(crafter);

            if (distance < best)
            {
                best = distance;
                pick = (crafter, piece);
            }
        }

        return pick;
    }

    /// <summary>A named seller beats one with the goods asked for, which beats a nearer one.</summary>
    public static int SellerScore(bool named, bool sellsTheGoods, int distance) =>
        (named ? NamedWeight : 0) + (sellsTheGoods ? GoodsWeight : 0) - distance;

    /// <summary>
    /// The character that crosses the floor for a person's WTS, or null. Somebody who could use
    /// the goods, can pay the bottom of their band, has not said no to this seller lately and
    /// would not laugh at the asking price; the nearest one answers. The room's interest is rolled
    /// once per seller and kind of goods and then kept.
    /// </summary>
    public static SosariaCharacter BuyerFor(Mobile seller, GoodsClaim claim, int asking) =>
        BuyerFor(seller, claim, asking, TradeRanges.WalkOverRange, MayShop);

    /// <summary>
    /// The character idling on the bank floor within <paramref name="reach"/> that answers a
    /// character's WTS (<see cref="FloorDeal"/>), by the same test a person's WTS gets, or null.
    /// </summary>
    public static SosariaCharacter FloorBuyerFor(SosariaCharacter seller, Item goods, int asking, int reach) =>
        seller == null || goods == null ? null : BuyerFor(seller, Appraisal.ClaimOf(goods), asking, reach, MayAnswer);

    /// <summary>
    /// The character idling on the bank floor within <paramref name="reach"/> of a WTB shouter that
    /// carries goods of the wanted kind for sale at a price the shouter could meet: it answers
    /// "i have one". The nearest wins; null when nobody fits.
    /// </summary>
    public static (SosariaCharacter Seller, Item Goods, int Asking)? FloorSellerFor(
        SosariaCharacter buyer, GoodsClaim claim, int reach, int roll
    )
    {
        if (claim.Row == null || !MayShop(buyer))
        {
            return null;
        }

        var purse = TradeHandOff.Purse(buyer);
        (SosariaCharacter, Item, int)? pick = null;
        var best = double.MaxValue;

        foreach (var mobile in buyer.GetMobilesInRange(reach))
        {
            if (mobile is not SosariaCharacter seller || seller == buyer || !MayAnswer(seller) ||
                !People.Perceives(seller, buyer) || Refused(buyer, seller) || Refused(seller, buyer) ||
                PieceIn(seller, item => Appraisal.RowOf(item) == claim.Row) is not { } goods)
            {
                continue;
            }

            var asking = Appraisal.Value(goods, Math.Abs(roll) % Appraisal.PercentScale);
            var distance = buyer.GetDistanceToSqrt(seller);

            if (MayMeet(buyer, goods, asking, purse) && distance < best)
            {
                best = distance;
                pick = (seller, goods, asking);
            }
        }

        return pick;
    }

    /// <summary>
    /// A lawful character idling on a bank floor (<see cref="FloorTradeRules.IsIdleOnFloor"/>),
    /// free to answer a shout.
    /// </summary>
    public static bool MayAnswer(SosariaCharacter character) =>
        MayShop(character) && FloorTradeRules.IsIdleOnFloor(character.Routine?.CurrentSkill?.Name);

    private static SosariaCharacter BuyerFor(
        Mobile seller, GoodsClaim claim, int asking, int reach, Func<SosariaCharacter, bool> free
    )
    {
        if (claim.Row == null || !RoomWants(seller, claim))
        {
            return null;
        }

        SosariaCharacter nearest = null;
        var best = double.MaxValue;
        var bottom = claim.Value(0);

        foreach (var mobile in seller.GetMobilesInRange(reach))
        {
            if (mobile is not SosariaCharacter buyer || buyer == seller || !free(buyer) || !People.Perceives(buyer, seller) ||
                Refused(buyer, seller) || BankCrowd.TryGetHawkerOffer(buyer, out _) ||
                !TradeDemandRules.Wants(AppetiteOf(buyer), claim.Row))
            {
                continue;
            }

            var purse = TradeHandOff.Purse(buyer);
            var ceiling = Haggle.Buying(claim.Value(Appraisal.MidRoll), purse, TradeSession.TemperOf(buyer)).Limit;

            if (purse < bottom || asking > 0 && Haggle.IsSilly(asking, ceiling))
            {
                continue;
            }

            var distance = buyer.GetDistanceToSqrt(seller);

            if (distance < best)
            {
                best = distance;
                nearest = buyer;
            }
        }

        return nearest;
    }

    /// <summary>A character shouted WTB and will listen for an answer until <paramref name="until"/>.</summary>
    public static void PostWant(SosariaCharacter buyer, GoodsClaim claim, DateTime until) =>
        Wants[buyer.Serial] = new TradeWant(claim with { Amount = claim.Lot }, until);

    public static void DropWant(SosariaCharacter buyer)
    {
        if (buyer != null)
        {
            Wants.Remove(buyer.Serial);
        }
    }

    /// <summary>
    /// The WTB a person's "i have one" answers: the nearest character in shouting range still
    /// listening, whose want matches the goods when the person named any.
    /// </summary>
    public static (SosariaCharacter Buyer, TradeWant Want)? WantAnswered(Mobile seller, GoodsClaim? goods)
    {
        (SosariaCharacter, TradeWant)? found = null;
        var best = double.MaxValue;
        var now = Core.Now;

        foreach (var mobile in seller.GetMobilesInRange(TradeRanges.ShoutRange))
        {
            if (mobile is not SosariaCharacter buyer || !Wants.TryGetValue(buyer.Serial, out var want) ||
                now > want.Until || !MayShop(buyer) || !People.Perceives(buyer, seller) ||
                goods is { } named && named.Row != want.Claim.Row)
            {
                continue;
            }

            var distance = buyer.GetDistanceToSqrt(seller);

            if (distance < best)
            {
                best = distance;
                found = (buyer, want);
            }
        }

        return found;
    }

    /// <summary>
    /// A hawker in reach whose goods this character could use and could meet the price of, or
    /// null. A buyer that already walked away from a hawker does not come back soon.
    /// </summary>
    public static (SosariaCharacter Hawker, Item Goods, HawkerOffer Offer)? HawkerFor(SosariaCharacter buyer)
    {
        if (!MayShop(buyer))
        {
            return null;
        }

        var appetite = AppetiteOf(buyer);
        var purse = TradeHandOff.Purse(buyer);

        return NearestOffer(
            buyer,
            TradeRanges.WalkOverRange,
            (hawker, item, offer) => Present(hawker) && !Refused(buyer, hawker) &&
                                     TradeDemandRules.Wants(appetite, Appraisal.RowOf(item)) &&
                                     MayMeet(buyer, item, offer.Asking, purse)
        );
    }

    /// <summary>
    /// The nearest offer held up within <paramref name="range"/> of a buyer free to shop
    /// (<see cref="MayShop"/>, the caller's test): a seller not in another deal, its goods still
    /// in its pack, and <paramref name="fits"/> true for them. Null when none is. A hawker's goods
    /// (<see cref="HawkerFor"/>) and a treasure map (<see cref="TreasureMarket.FindFor"/>) are both
    /// found this way.
    /// </summary>
    public static (SosariaCharacter Hawker, Item Goods, HawkerOffer Offer)? NearestOffer(
        SosariaCharacter buyer, int range, Func<SosariaCharacter, Item, HawkerOffer, bool> fits
    )
    {
        (SosariaCharacter, Item, HawkerOffer)? pick = null;
        var best = double.MaxValue;

        foreach (var (hawker, offer) in BankCrowd.HawkersNear(buyer.Map, buyer.Location, range))
        {
            if (hawker == buyer || TradeSessions.IsBusy(hawker) || World.FindItem(offer.Item) is not { } item ||
                !item.IsChildOf(hawker.Backpack) || !fits(hawker, item, offer))
            {
                continue;
            }

            var distance = buyer.GetDistanceToSqrt(hawker);

            if (distance < best)
            {
                best = distance;
                pick = (hawker, item, offer);
            }
        }

        return pick;
    }

    /// <summary>
    /// A crafter holding goods up within <paramref name="reach"/> of the buyer whose pack holds a
    /// finished piece that <paramref name="fits"/>, for sale and within reach of
    /// <paramref name="purse"/>, or null. A buyer asks the smith at the bank for the plate legs it
    /// needs, not only the piece held up: the smith digs it out of its pack. The nearest wins.
    /// </summary>
    public static (SosariaCharacter Crafter, Item Piece, int Asking)? CrafterPieceFor(
        SosariaCharacter buyer, Func<Item, bool> fits, int reach, int purse
    )
    {
        if (!MayShop(buyer) || fits == null)
        {
            return null;
        }

        (SosariaCharacter, Item, int)? pick = null;
        var best = double.MaxValue;

        foreach (var (crafter, offer) in BankCrowd.HawkersNear(buyer.Map, buyer.Location, reach))
        {
            if (crafter == buyer || !Present(crafter) || TradeSessions.IsBusy(crafter) || Refused(buyer, crafter) ||
                CraftMarket.TradeOf(crafter) == null || PieceIn(crafter, fits) is not { } piece)
            {
                continue;
            }

            var asking = piece.Serial == offer.Item ? offer.Asking : Appraisal.Value(piece, Appraisal.MidRoll);
            var distance = buyer.GetDistanceToSqrt(crafter);

            if (MayMeet(buyer, piece, asking, purse) && distance < best)
            {
                best = distance;
                pick = (crafter, piece, asking);
            }
        }

        return pick;
    }

    /// <summary>
    /// True when a buyer with <paramref name="purse"/> could meet the seller of
    /// <paramref name="goods"/> at <paramref name="asking"/>: its ceiling reaches the lowest floor
    /// any seller's temper would come down to.
    /// </summary>
    public static bool MayMeet(SosariaCharacter buyer, Item goods, int asking, int purse) =>
        MayMeet(buyer, Appraisal.ClaimOf(goods), asking, purse);

    /// <summary>The same test for goods described by <paramref name="claim"/> before they are cut from a stack.</summary>
    public static bool MayMeet(SosariaCharacter buyer, GoodsClaim claim, int asking, int purse)
    {
        var ceiling = Haggle.Buying(claim.Value(Appraisal.MidRoll), purse, TradeSession.TemperOf(buyer)).Limit;
        return ceiling >= LowestFloor(asking);
    }

    /// <summary>The lowest price any seller's temper comes down to from <paramref name="asking"/>: the soft floor.</summary>
    public static int LowestFloor(int asking) => (int)(asking * Haggle.FloorShare(HaggleTemper.Soft));

    /// <summary>What the person buys by its class, and a runebook while it recalls without one.</summary>
    public static TradeAppetite AppetiteOf(SosariaCharacter character)
    {
        var appetite = TradeDemandRules.AppetiteOf(
            (character?.PersonProfile ?? PersonProfile.Default).Class,
            character?.Build?.Style ?? Combat.CombatStyle.Melee
        );

        return character != null &&
               RunebookRules.WantsBook(
                   RuneKit.TravelsByMagic(character.Skills.Magery.Value, RuneKit.RecallStock(character)),
                   RuneShelf.Book(character) != null
               )
            ? appetite | TradeAppetite.Travel
            : appetite;
    }

    /// <summary>A lawful character standing in the world, not fighting and not in another deal.</summary>
    public static bool MayShop(SosariaCharacter character) =>
        Present(character) && !character.IsPk && !character.Criminal && !TradeSessions.IsBusy(character) &&
        character.Motor.Action == CharacterAction.Wander;

    // The first piece in the person's pack that fits and that it would sell.
    private static Item PieceIn(SosariaCharacter seller, Func<Item, bool> fits)
    {
        foreach (var item in seller.Backpack?.Items ?? [])
        {
            if (fits(item) && HawkerGoods.IsForSale(seller, item))
            {
                return item;
            }
        }

        return null;
    }

    private static bool Present(SosariaCharacter character) =>
        character is { Deleted: false, Alive: true, Hidden: false } && character.Map != null && character.Map != Map.Internal;

    /// <summary>True while the character's "no" to this person or character still stands.</summary>
    public static bool Refused(SosariaCharacter character, Mobile other) =>
        Refusals.TryGetValue((character.Serial, other.Serial), out var until) && Core.Now < until;

    // The sticky verdict for this seller and this kind of goods.
    private static bool RoomWants(Mobile seller, GoodsClaim claim)
    {
        var key = (seller.Serial, claim.Row.Key);
        var now = Core.Now;

        if (RoomVerdicts.TryGetValue(key, out var verdict) && now < verdict.Until)
        {
            return verdict.Bites;
        }

        var bites = TradeDemandRules.RoomBites(claim, Utility.Random(TradeDemandRules.PercentScale));
        Prune(now);
        RoomVerdicts[key] = (bites, now + TradeDemandRules.RefusalHold);
        return bites;
    }

    private static void Prune(DateTime now)
    {
        if (RoomVerdicts.Count + Refusals.Count <= PruneAbove)
        {
            return;
        }

        foreach (var (key, verdict) in RoomVerdicts)
        {
            if (now >= verdict.Until)
            {
                RoomVerdicts.Remove(key);
            }
        }

        foreach (var (key, until) in Refusals)
        {
            if (now >= until)
            {
                Refusals.Remove(key);
            }
        }
    }
}
