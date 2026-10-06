using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>
/// Orders, said and kept. A person or a fighter asks a crafter for work. The crafter reads the
/// goods, checks it can make them (<see cref="CraftOrderRules.Refusal"/>), and quotes the price
/// and the deposit. A "yes" opens the trade window for the deposit; the order is saved only when
/// the coin crossed. The crafter makes the work first at its station and holds each finished
/// exceptional piece for the buyer (<see cref="TagPiece"/>). When the buyer comes by or asks, it
/// opens the window again with the work on its side, for the rest of the coin. An order nobody
/// picks up in time goes to its shop stock, and the deposit stays with the crafter. A crafter
/// cannot reach an offline person: a ready order waits for the buyer. World thread only.
/// </summary>
public static class OrderDesk
{
    private sealed record Quote(SosariaCharacter Crafter, IReadOnlyList<string> Types, string Noun, int Price, int Deposit, DateTime Until);

    private static readonly ILogger logger = SosariaLog.For(typeof(OrderDesk));
    private static readonly Dictionary<Serial, Quote> Quotes = new();

    public static bool MayTakeOrder(SosariaCharacter crafter) =>
        crafter is { Deleted: false, Alive: true } && CraftMarket.TradeOf(crafter) != null &&
        crafter.CraftOrders.Count < CraftOrderRules.MaxOpenOrders;

    public static bool HasQuote(Mobile buyer) =>
        buyer != null && Quotes.TryGetValue(buyer.Serial, out var quote) && Core.Now < quote.Until;

    /// <summary>A person asks for work: the crafter in talking range quotes, offers a piece from stock, or says why not.</summary>
    public static void Ask(Mobile buyer, string text, TradeIntent intent)
    {
        if (CrafterFor(buyer, text) is not { } crafter)
        {
            return;
        }

        var trade = CraftMarket.TradeOf(crafter);

        if (intent.Goods is not { } claim)
        {
            Say(crafter, buyer, TradeLineKind.OrderWhat, trade.GoodsNoun, 0, 0);
            return;
        }

        if (claim.Piece != ArmorPiece.FullSet && ShopStock.Named(crafter, claim) is { } piece)
        {
            var asking = ShopStock.AskingOf(piece);
            TradeSessions.Open(TradeSession.Selling(crafter, buyer, piece, asking));
            Say(crafter, buyer, TradeLineKind.HaveOne, Appraisal.NounOf(piece), asking, 0);
            return;
        }

        var types = OrderItems.Resolve(trade, TradeParser.Words(text, crafter.Name), claim);
        var noun = types.Count > 0 ? OrderItems.Noun(types) : (claim with { Exceptional = true }).Noun;
        var refusal = RefusalFor(crafter, trade, types);

        if (refusal != OrderRefusal.None)
        {
            Say(crafter, buyer, RefusalLine(refusal), noun, 0, 0);
            return;
        }

        var price = CraftOrderRules.Quote(OrderItems.MarketValue(types));
        var deposit = CraftOrderRules.DepositOf(price);
        Quotes[buyer.Serial] = new Quote(crafter, types, noun, price, deposit, Core.Now + CraftOrderRules.QuoteHold);
        Say(crafter, buyer, TradeLineKind.OrderQuote, noun, price, deposit);
    }

    /// <summary>"yes" to a quote: the trade window opens for the deposit.</summary>
    public static void Accept(Mobile buyer)
    {
        if (!Quotes.Remove(buyer.Serial, out var quote) || !quote.Crafter.InRange(buyer, TradeRanges.TalkRange) ||
            TradeSessions.IsBusy(quote.Crafter))
        {
            return;
        }

        // Another buyer may have filled the last order slot since the quote.
        if (!MayTakeOrder(quote.Crafter))
        {
            Say(quote.Crafter, buyer, TradeLineKind.OrderTooBusy, quote.Noun, 0, 0);
            return;
        }

        TradeSessions.Open(
            TradeSession.Fixed(
                quote.Crafter,
                buyer,
                null,
                quote.Deposit,
                quote.Noun,
                TradeLineKind.SellerAccept,
                paid =>
                {
                    if (paid)
                    {
                        Place(quote, buyer);
                    }
                }
            )
        );
    }

    public static void Decline(Mobile buyer) => Quotes.Remove(buyer.Serial);

    /// <summary>"is my order ready": a ready order is handed over; else the crafter says it is not done.</summary>
    public static void Status(Mobile buyer)
    {
        foreach (var mobile in buyer.GetMobilesInRange(TradeRanges.TalkRange))
        {
            if (mobile is not SosariaCharacter crafter || crafter.OrderFor(buyer.Serial.Value) is not { } order)
            {
                continue;
            }

            var busy = TradeSessions.IsBusy(crafter);

            if (CraftOrderRules.StatusLine(order.Ready, busy) is { } line)
            {
                Say(crafter, buyer, line, OrderItems.Noun(order.ItemTypes), 0, 0);
            }
            else if (!HandOver(crafter, buyer, order))
            {
                Say(crafter, buyer, TradeLineKind.OrderBusy, OrderItems.Noun(order.ItemTypes), 0, 0);
            }

            return;
        }
    }

    /// <summary>The price an order of <paramref name="itemType"/> costs at this crafter, or 0 when it would turn it down.</summary>
    public static int PriceFor(SosariaCharacter crafter, string itemType)
    {
        IReadOnlyList<string> types = [itemType];
        return MayTakeOrder(crafter) && RefusalFor(crafter, CraftMarket.TradeOf(crafter), types) == OrderRefusal.None
            ? CraftOrderRules.Quote(OrderItems.MarketValue(types))
            : 0;
    }

    /// <summary>A fighter orders at the crafter's side: it pays the deposit from its pack. The order, or null.</summary>
    public static CraftOrder PlaceForBot(SosariaCharacter crafter, SosariaCharacter buyer, string itemType)
    {
        var price = PriceFor(crafter, itemType);
        var deposit = CraftOrderRules.DepositOf(price);

        if (price <= 0 || TradeSessions.IsBusy(crafter) || !TradeHandOff.Pay(buyer, crafter, deposit))
        {
            return null;
        }

        IReadOnlyList<string> types = [itemType];
        return Place(new Quote(crafter, types, OrderItems.Noun(types), price, deposit, Core.Now), buyer);
    }

    /// <summary>The type of the next piece the oldest open order needs, or null.</summary>
    public static Type NextToMake(SosariaCharacter crafter)
    {
        foreach (var order in crafter.CraftOrders)
        {
            if (!order.Ready)
            {
                return AssemblyHandler.FindTypeByName(order.NextType);
            }
        }

        return null;
    }

    /// <summary>Holds a new exceptional piece for the first order that needs its type. True when held.</summary>
    public static bool TagPiece(SosariaCharacter crafter, Item piece)
    {
        if (piece == null || !Appraisal.IsExceptional(piece))
        {
            return false;
        }

        foreach (var order in crafter.CraftOrders)
        {
            if (order.Ready || order.NextType != piece.GetType().Name)
            {
                continue;
            }

            var held = order.WithPiece(piece.Serial.Value);
            crafter.ReplaceCraftOrder(held);

            if (held.Ready && SosariaSettings.LogActivity)
            {
                logger.Information(
                    "{Crafter} finished the order of {Buyer}: {Piece}",
                    crafter.Name,
                    held.BuyerName,
                    OrderItems.Noun(held.ItemTypes)
                );
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Lets an order go once its pickup window passed: its pieces are shop stock again and the
    /// deposit stays. A held piece that left the pack (a death, a theft) is made again: the
    /// pieces from the first lost one on go back to plain stock.
    /// </summary>
    public static void Sweep(SosariaCharacter crafter, DateTime now)
    {
        foreach (var order in crafter.CraftOrders)
        {
            if (CraftOrderRules.Expired(order.PlacedAt, now))
            {
                crafter.RemoveCraftOrder(order.Id);

                if (SosariaSettings.LogActivity)
                {
                    logger.Information(
                        "{Crafter} let the order of {Buyer} go after {Days} days; the {Piece} goes to its shop",
                        crafter.Name,
                        order.BuyerName,
                        (int)CraftOrderRules.PickupWindow.TotalDays,
                        OrderItems.Noun(order.ItemTypes)
                    );
                }

                continue;
            }

            var held = new List<uint>();

            foreach (var serial in order.PieceSerials)
            {
                if (World.FindItem((Serial)serial) is not { Deleted: false } piece || !piece.IsChildOf(crafter.Backpack))
                {
                    break;
                }

                held.Add(serial);
            }

            if (held.Count != order.PieceSerials.Count)
            {
                crafter.ReplaceCraftOrder(order with { PieceSerials = held });
            }
        }
    }

    /// <summary>
    /// The person a ready order waits for came into talking range: the hand-over begins, once per
    /// arrival (<paramref name="offered"/>, see <see cref="CraftOrderRules.PickupDue"/>). A "no"
    /// or a timeout waits until they walk off and come back, or ask. True when it began.
    /// </summary>
    public static bool OfferPickup(SosariaCharacter crafter, ISet<uint> offered)
    {
        foreach (var order in crafter.CraftOrders)
        {
            if (!order.Ready || World.FindMobile((Serial)order.BuyerSerial) is not { } buyer || !People.IsHuman(buyer))
            {
                continue;
            }

            var inRange = crafter.InRange(buyer, TradeRanges.TalkRange) && People.Perceives(crafter, buyer);

            if (CraftOrderRules.PickupDue(offered, order.BuyerSerial, inRange) && TradeSessions.FindFor(buyer) == null)
            {
                return HandOver(crafter, buyer, order);
            }
        }

        return false;
    }

    /// <summary>A fighter at the crafter for its ready order pays the rest and takes the work. The pieces, or none.</summary>
    public static List<Item> HandOverToBot(SosariaCharacter crafter, SosariaCharacter buyer)
    {
        // Every piece must still be in the crafter's pack before any coin moves.
        if (crafter.OrderFor(buyer.Serial.Value) is not { Ready: true } order || TradeSessions.IsBusy(crafter) ||
            HeldPieces(crafter, order) is not { } pieces || !TradeHandOff.Pay(buyer, crafter, order.Rest))
        {
            return [];
        }

        foreach (var piece in pieces)
        {
            TradeHandOff.Give(buyer, piece);
        }

        crafter.RemoveCraftOrder(order.Id);
        LogHanded(crafter, buyer, order);
        return pieces;
    }

    private static bool HandOver(SosariaCharacter crafter, Mobile buyer, CraftOrder order)
    {
        if (TradeSessions.IsBusy(crafter) || Goods(crafter, order) is not { } goods)
        {
            return false;
        }

        TradeSessions.Open(
            TradeSession.Fixed(
                crafter,
                buyer,
                goods,
                order.Rest,
                OrderItems.Noun(order.ItemTypes),
                TradeLineKind.OrderReady,
                paid => Settled(crafter, buyer, order, goods, paid)
            )
        );
        return true;
    }

    // The order's pieces, every one still in the crafter's pack, or null.
    private static List<Item> HeldPieces(SosariaCharacter crafter, CraftOrder order)
    {
        var pieces = new List<Item>();

        foreach (var serial in order.PieceSerials)
        {
            if (World.FindItem((Serial)serial) is not { Deleted: false } piece || !piece.IsChildOf(crafter.Backpack))
            {
                return null;
            }

            pieces.Add(piece);
        }

        return pieces;
    }

    // One piece goes as it is; a suit goes in a bag, which is unpacked again when the deal folds.
    private static Item Goods(SosariaCharacter crafter, CraftOrder order)
    {
        if (HeldPieces(crafter, order) is not { } pieces)
        {
            return null;
        }

        if (pieces.Count == 1)
        {
            return pieces[0];
        }

        var bag = new Bag();
        crafter.Backpack.DropItem(bag);

        foreach (var piece in pieces)
        {
            bag.DropItem(piece);
        }

        return bag;
    }

    private static void Settled(SosariaCharacter crafter, Mobile buyer, CraftOrder order, Item goods, bool paid)
    {
        if (paid)
        {
            crafter.RemoveCraftOrder(order.Id);
            LogHanded(crafter, buyer, order);
            return;
        }

        if (goods is Bag bag && order.PieceSerials.Count > 1)
        {
            foreach (var piece in new List<Item>(bag.Items))
            {
                crafter.Backpack.DropItem(piece);
            }

            bag.Delete();
        }
    }

    private static CraftOrder Place(Quote quote, Mobile buyer)
    {
        var crafter = quote.Crafter;
        var order = new CraftOrder(
            CraftOrderRules.NewId(crafter.Serial.Value, Core.Now, crafter.CraftOrders.Count),
            buyer.Serial.Value,
            buyer.Name,
            quote.Types,
            quote.Price,
            quote.Deposit,
            Core.Now,
            []
        );

        crafter.AddCraftOrder(order);
        Say(crafter, buyer, TradeLineKind.OrderTaken, quote.Noun, 0, 0);

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Crafter} took an order from {Buyer}: {Piece} for {Gold} gold, {Deposit} deposit",
                crafter.Name,
                buyer.Name,
                quote.Noun,
                quote.Price,
                quote.Deposit
            );
        }

        return order;
    }

    // Work the market table cannot price as exceptional (potions, scrolls, tools) is no order: it
    // could never be finished, and it would quote a coin.
    private static OrderRefusal RefusalFor(SosariaCharacter crafter, CraftTrade trade, IReadOnlyList<string> types)
    {
        var (success, exceptional) = OrderItems.Chances(crafter, trade, types);
        return CraftOrderRules.Refusal(types.Count > 0 && OrderItems.MarketValue(types) > 0, success, exceptional, crafter.CraftOrders.Count);
    }

    private static TradeLineKind RefusalLine(OrderRefusal refusal) =>
        refusal switch
        {
            OrderRefusal.CannotMake => TradeLineKind.OrderCannotMake,
            OrderRefusal.TooHard => TradeLineKind.OrderTooHard,
            _ => TradeLineKind.OrderTooBusy
        };

    // The crafter in talking range a request is aimed at: the one named first, else the nearest. Free, seen, with a trade.
    private static SosariaCharacter CrafterFor(Mobile buyer, string text)
    {
        SosariaCharacter pick = null;
        var best = int.MinValue;

        foreach (var mobile in buyer.GetMobilesInRange(TradeRanges.TalkRange))
        {
            if (mobile is not SosariaCharacter crafter || !crafter.Alive || TradeSessions.IsBusy(crafter) ||
                CraftMarket.TradeOf(crafter) == null || !People.Perceives(crafter, buyer))
            {
                continue;
            }

            var score = TradeMarket.SellerScore(
                AttentionGate.MentionsName(text, crafter.Name),
                false,
                (int)buyer.GetDistanceToSqrt(crafter)
            );

            if (score > best)
            {
                best = score;
                pick = crafter;
            }
        }

        return pick;
    }

    private static void LogHanded(SosariaCharacter crafter, Mobile buyer, CraftOrder order)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Crafter} handed {Piece} to {Buyer} for {Gold} gold (order)",
                crafter.Name,
                OrderItems.Noun(order.ItemTypes),
                buyer.Name,
                order.Price
            );
        }
    }

    private static void Say(SosariaCharacter crafter, Mobile buyer, TradeLineKind kind, string noun, int price, int theirs) =>
        TradeVoice.Say(crafter, buyer, TradeLines.For(kind, Utility.Random(int.MaxValue), noun, price, theirs));
}
