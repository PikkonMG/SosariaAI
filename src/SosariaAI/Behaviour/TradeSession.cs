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
using SosariaAI.Spawning;

namespace SosariaAI.Behaviour;

/// <summary>Where a haggle with a person stands.</summary>
public enum TradePhase
{
    /// <summary>The buying character is crossing the floor to the seller.</summary>
    Approach,

    /// <summary>Numbers go back and forth.</summary>
    Talk,

    /// <summary>A price is shaken on; the trade window is up or the goods and gold wait for a drop.</summary>
    HandOff
}

/// <summary>
/// One haggle between a character and a person at a keyboard, face to face. The character holds
/// still and faces the person while it lasts (the routine waits), answers every trade line with
/// the haggle's next number, and after "deal" opens a real trade window when the person can see
/// one — the goods or the coin already on the character's side — or waits for a bare drop when
/// they cannot. A haggle nobody comes back to goes cold. World thread only.
/// </summary>
public sealed class TradeSession
{
    private static readonly ILogger logger = SosariaLog.For(typeof(TradeSession));

    /// <summary>Ticks in a row a walking buyer may fail to step before it gives up.</summary>
    public const int MaxStalls = 8;

    private readonly Haggle _haggle;
    private readonly Item _goods;
    private readonly GoodsClaim _claim;
    private readonly List<Item> _offered = [];
    private TradeWindow _window;
    private bool _windowDeclined;
    private bool _theirAccepted;
    private int _pendingAsk;
    private int _stalls;
    private DateTime _lastHeard;
    private DateTime _phaseSince;

    private TradeSession(SosariaCharacter character, Mobile partner, Haggle haggle, Item goods, GoodsClaim claim)
    {
        Character = character;
        Partner = partner;
        _haggle = haggle;
        _goods = goods;
        _claim = claim;
        Noun = goods != null ? Appraisal.NounOf(goods) : claim.Noun;
        _lastHeard = Core.Now;
        _phaseSince = Core.Now;
        Phase = haggle.Side == HaggleSide.Sells ? TradePhase.Talk : TradePhase.Approach;
    }

    public SosariaCharacter Character { get; }

    public Mobile Partner { get; }

    public TradePhase Phase { get; private set; }

    public string Noun { get; }

    public HaggleSide Side => _haggle.Side;

    /// <summary>The last number the character said.</summary>
    public int Standing => _haggle.Standing;

    public int Agreed => _haggle.Agreed;

    public bool Ended { get; private set; }

    /// <summary>A character holding <paramref name="goods"/> for sale at <paramref name="asking"/>.</summary>
    public static TradeSession Selling(SosariaCharacter character, Mobile buyer, Item goods, int asking) =>
        new(character, buyer, Haggle.Selling(asking, TemperOf(character)), goods, Appraisal.ClaimOf(goods));

    /// <summary>
    /// A character crossing the floor to buy what <paramref name="seller"/> described.
    /// <paramref name="theirAsk"/> is the price the seller already named, or 0.
    /// </summary>
    public static TradeSession Buying(SosariaCharacter character, Mobile seller, GoodsClaim claim, int theirAsk)
    {
        var fixedLot = claim with { Amount = claim.Lot };
        var haggle = Haggle.Buying(fixedLot.Value(Appraisal.MidRoll), TradeHandOff.Purse(character), TemperOf(character));
        return new TradeSession(character, seller, haggle, null, fixedLot) { _pendingAsk = theirAsk };
    }

    public static HaggleTemper TemperOf(SosariaCharacter character)
    {
        var profile = character?.PersonProfile ?? PersonProfile.Default;
        return Haggle.TemperOf(profile.Has(PersonTrait.Greedy), profile.Has(PersonTrait.Generous));
    }

    /// <summary>The person said something the parser placed.</summary>
    public void Hear(TradeIntent intent)
    {
        if (Ended || !intent.IsTrade)
        {
            return;
        }

        _lastHeard = Core.Now;

        if (Phase == TradePhase.Approach)
        {
            if (intent.Kind == TradeIntentKind.Decline)
            {
                End(TradeLineKind.Released);
            }
            else if (intent.Price > 0)
            {
                _pendingAsk = intent.Price;
            }

            return;
        }

        switch (intent.Kind)
        {
            case TradeIntentKind.AskStock:
                Say(Side == HaggleSide.Sells ? TradeLineKind.Stock : TradeLineKind.BuyerOpen, Standing, 0);
                break;
            case TradeIntentKind.AskPrice:
                Say(Side == HaggleSide.Sells ? TradeLineKind.Price : TradeLineKind.BuyerOpen, Standing, 0);
                break;
            case TradeIntentKind.Offer:
            case TradeIntentKind.HaveOne when intent.Price > 0:
            case TradeIntentKind.Sell when intent.Price > 0:
                Answer(intent.Price);
                break;
            case TradeIntentKind.Accept when Phase == TradePhase.HandOff:
                RepeatTerms();
                OpenWindow(reopen: true);
                break;
            case TradeIntentKind.Accept:
                Settle(_haggle.Agree());
                break;
            case TradeIntentKind.Decline:
                End(TradeLineKind.Released);
                break;
        }
    }

    /// <summary>
    /// The person dropped an item on the character. A live deal seats it on the person's side of
    /// the trade window when they can see one, and takes it bare when they cannot. False hands
    /// it back.
    /// </summary>
    public bool Receive(Item dropped)
    {
        _lastHeard = Core.Now;

        if (Phase != TradePhase.HandOff)
        {
            Say(TradeLineKind.NotMine, 0, 0);
            return false;
        }

        if (_window != null)
        {
            return _window.IsOpen && _window.Offer(dropped);
        }

        if (TradeWindow.CanOpen(Partner) && Partner.CheckTrade(Character, dropped, null, true, true, 0, 0))
        {
            OpenWindow(reopen: true);

            return _window?.Offer(dropped) == true;
        }

        return Side == HaggleSide.Sells ? TakeGold(dropped) : TakeGoods(dropped);
    }

    /// <summary>Keeps the character facing its customer, walks a buyer over, and cools a dead haggle. False once ended.</summary>
    public bool Tick(DateTime now)
    {
        if (Ended)
        {
            return false;
        }

        if (!StillHere())
        {
            End(null);
            return false;
        }

        if (Phase == TradePhase.HandOff)
        {
            if (now - _phaseSince > TradeRanges.HandOff)
            {
                End(TradeLineKind.Timeout);
                return false;
            }

            WatchWindow();

            if (Ended)
            {
                return false;
            }
        }

        if (Side == HaggleSide.Sells && !HoldsGoods())
        {
            End(TradeLineKind.Released);
            return false;
        }

        Character.Conversation.Hold(Partner.Serial, now, TradeRanges.Idle);

        switch (Phase)
        {
            case TradePhase.Approach:
                Approach(now);
                break;
            case TradePhase.Talk when now - _lastHeard > TradeRanges.Idle:
                End(TradeLineKind.Timeout);
                break;
        }

        return !Ended;
    }

    /// <summary>Closes the haggle, says a parting line when one is given, and lets the routine go on.</summary>
    public void End(TradeLineKind? parting)
    {
        if (Ended)
        {
            return;
        }

        Ended = true;

        _window?.Cancel();
        _window = null;

        _haggle.End();

        if (parting is { } line)
        {
            Say(line, Standing, 0);
        }

        if (Character.Conversation.Partner == Partner.Serial)
        {
            Character.Conversation.Clear();
        }

        TradeSessions.Close(this);
    }

    private void Approach(DateTime now)
    {
        if (Character.InRange(Partner, TradeRanges.DealRange))
        {
            Character.Motor.ClearMoveIntent();
            Character.Direction = Character.GetDirectionTo(Partner);
            Phase = TradePhase.Talk;
            _phaseSince = now;
            _lastHeard = now;

            if (_pendingAsk > 0)
            {
                Answer(_pendingAsk);
            }
            else
            {
                Say(TradeLineKind.BuyerOpen, Standing, 0);
            }

            return;
        }

        _stalls = Character.Motor.MoveTo(Partner, TradeRanges.DealRange) ? 0 : _stalls + 1;

        if (now - _phaseSince > TradeRanges.Approach || _stalls > MaxStalls)
        {
            End(TradeLineKind.Timeout);
        }
    }

    private void Answer(int theirs)
    {
        if (Phase == TradePhase.HandOff)
        {
            RepeatTerms();
            return;
        }

        var step = _haggle.Hear(theirs, Utility.Random(Haggle.PercentScale));

        switch (step.Move)
        {
            case HaggleMove.Accept:
                Settle(step);
                break;
            case HaggleMove.Counter:
                Say(Side == HaggleSide.Sells ? TradeLineKind.SellerCounter : TradeLineKind.BuyerCounter, step.Price, theirs);
                break;
            case HaggleMove.Firm:
                Say(Side == HaggleSide.Sells ? TradeLineKind.SellerFirm : TradeLineKind.BuyerFirm, step.Price, theirs);
                break;
            case HaggleMove.WalkAway:
                TradeMarket.NoteRefusal(Character, Partner, _claim);
                End(Side == HaggleSide.Sells ? TradeLineKind.SellerWalk : TradeLineKind.BuyerWalk);
                break;
            default:
                TradeMarket.NoteRefusal(Character, Partner, _claim);
                End(TradeLineKind.Insulted);
                break;
        }
    }

    // A price is already shaken on: say again what to drop.
    private void RepeatTerms() =>
        Say(Side == HaggleSide.Sells ? TradeLineKind.SellerAccept : TradeLineKind.BuyerAccept, Agreed, 0);

    private void Settle(HaggleStep step)
    {
        if (step.Move != HaggleMove.Accept)
        {
            return;
        }

        Phase = TradePhase.HandOff;
        _phaseSince = Core.Now;
        OpenWindow();
        RepeatTerms();
        Log($"{Character.Name} agreed {step.Price} gold with {Partner.Name} for {Noun}");
    }

    /// <summary>
    /// The agreed deal goes up as a real trade window, the way a person opens one: the
    /// character's side already shows its goods or its coin. A person who will not or cannot see
    /// a window still settles by dropping the coin or the goods on the character.
    /// </summary>
    private void OpenWindow(bool reopen = false)
    {
        if (_window != null || (_windowDeclined && !reopen) || !TradeWindow.CanOpen(Partner) ||
            !Character.InRange(Partner, TradeRanges.DealRange))
        {
            return;
        }

        _windowDeclined = false;
        var window = TradeWindow.Open(Partner, Character);

        if (window == null)
        {
            return;
        }

        if (Side == HaggleSide.Sells)
        {
            if (!HoldsGoods())
            {
                window.Cancel();
                return;
            }

            window.Stock(_goods);
        }
        else if (!StockGold(window))
        {
            window.Cancel();
            End(TradeLineKind.ShortOfGold);
            return;
        }

        _window = window;
        _theirAccepted = false;
    }

    /// <summary>
    /// The character's side of the window holds the agreed coin; the purse draws the rest at the
    /// banker first, the way <see cref="TradeHandOff.Pay" /> does.
    /// </summary>
    private bool StockGold(TradeWindow window)
    {
        if (!TradeHandOff.DrawGold(Character, Agreed))
        {
            return false;
        }

        TradeHandOff.HandOutGold(Agreed, window.Stock);
        return true;
    }

    /// <summary>
    /// Keeps a live window inside the engine's arm's-length rule (the trade never joins the
    /// person's NetState list, so the engine cannot sweep it), checks the character's own box
    /// once the person's side holds the deal, and names what is wrong when they check theirs
    /// against the wrong contents.
    /// </summary>
    private void WatchWindow()
    {
        var window = _window;

        if (window == null)
        {
            OpenWindow();
            return;
        }

        if (!window.IsOpen)
        {
            FoldWindow();
            return;
        }

        if (Partner is not { Deleted: false, Alive: true } || Partner.Map != Character.Map ||
            !Character.InRange(Partner, TradeRanges.DealRange))
        {
            window.Cancel();
            _window = null;
            _theirAccepted = false;
            return;
        }

        var paid = TheirGold(window, out var onlyGold);
        var met = Side == HaggleSide.Sells
            ? onlyGold && paid == Agreed && window.TheirLedgerEmpty
            : GoodsThere(window);

        if (met && !window.WeAccepted)
        {
            window.Accept();

            if (!window.IsOpen)
            {
                FoldWindow();
            }

            return;
        }

        var pressed = window.TheirAccepted;

        if (pressed && !_theirAccepted && !met)
        {
            if (Side == HaggleSide.Sells)
            {
                Say(TradeLineKind.WrongGold, Agreed, paid);
            }
            else
            {
                Say(TradeLineKind.WrongGoods, 0, 0);
            }
        }

        _theirAccepted = pressed;
    }

    /// <summary>
    /// The window closed. The swap ran when the goods or the coin crossed hands; a closed window
    /// without a swap means the person folded it, and a fresh drop or another "deal" opens a new
    /// one.
    /// </summary>
    private void FoldWindow()
    {
        _window = null;
        _theirAccepted = false;

        var done = Side == HaggleSide.Sells
            ? _goods is { Deleted: false } && !_goods.IsChildOf(Character.Backpack)
            : _offered is { Count: > 0 } && _offered.TrueForAll(item => item.IsChildOf(Character.Backpack));

        _offered.Clear();

        if (!done)
        {
            _windowDeclined = true;
            return;
        }

        Closed(throughWindow: true);
    }

    // The deal with the person is done: the log line, the shard's trade count and the thanks.
    private void Closed(bool throughWindow)
    {
        Log(
            Side == HaggleSide.Sells
                ? $"{Character.Name} sold {Noun} to {Partner.Name} for {Agreed} gold"
                : $"{Character.Name} bought {Noun} from {Partner.Name} for {Agreed} gold"
        );
        TradeTally.NoteDeal(Agreed, throughWindow);
        End(TradeLineKind.Thanks);
    }

    /// <summary>
    /// Coin on the person's side of the window, and whether nothing else sits there. A currency
    /// check cannot reach a character — account gold never settles without an account on both
    /// sides — so a check does not count toward the price.
    /// </summary>
    private static int TheirGold(TradeWindow window, out bool onlyGold)
    {
        var gold = 0;
        onlyGold = true;

        foreach (var item in window.Theirs.Items)
        {
            if (item is VirtualCheck)
            {
                continue;
            }

            if (item is Gold pile)
            {
                gold += pile.Amount;
            }
            else
            {
                onlyGold = false;
            }
        }

        return gold;
    }

    /// <summary>
    /// The person's side of the window holds exactly what they said they would sell, remembered
    /// so the close can tell a done deal from a folded one.
    /// </summary>
    private bool GoodsThere(TradeWindow window)
    {
        _offered.Clear();

        foreach (var item in window.Theirs.Items)
        {
            if (item is VirtualCheck)
            {
                continue;
            }

            if (!_claim.Matches(item))
            {
                _offered.Clear();
                return false;
            }

            _offered.Add(item);
        }

        return _offered.Count > 0;
    }

    private bool TakeGold(Item dropped)
    {
        if (dropped is not Gold gold)
        {
            Say(TradeLineKind.NotMine, 0, 0);
            return false;
        }

        if (gold.Amount != Agreed)
        {
            Say(TradeLineKind.WrongGold, Agreed, gold.Amount);
            return false;
        }

        if (!HoldsGoods())
        {
            End(TradeLineKind.Released);
            return false;
        }

        Character.Backpack.DropItem(gold);
        TradeHandOff.Give(Partner, _goods);
        Closed(throughWindow: false);
        return true;
    }

    private bool TakeGoods(Item dropped)
    {
        if (!_claim.Matches(dropped))
        {
            Say(TradeLineKind.WrongGoods, 0, 0);
            return false;
        }

        if (!TradeHandOff.Pay(Character, Partner, Agreed))
        {
            End(TradeLineKind.ShortOfGold);
            return false;
        }

        Character.Backpack.DropItem(dropped);
        Closed(throughWindow: false);
        return true;
    }

    private bool HoldsGoods() =>
        _goods is { Deleted: false } &&
        (_goods.IsChildOf(Character.Backpack) ||
         _window is { IsOpen: true } && _goods.IsChildOf(_window.Ours));

    private bool StillHere() =>
        Character is { Deleted: false, Alive: true } && Character.Backpack != null &&
        Character.Motor.Action == CharacterAction.Wander &&
        Partner is { Deleted: false, Alive: true } && Partner.Map == Character.Map && People.Perceives(Character, Partner) &&
        Character.InRange(Partner, Phase == TradePhase.Approach ? TradeRanges.ShoutRange : TradeRanges.WalkOverRange);

    private void Say(TradeLineKind kind, int price, int theirs) =>
        TradeVoice.Say(Character, Partner, TradeLines.For(kind, Utility.Random(int.MaxValue), Noun, price, theirs));

    private static void Log(string line)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Line}", line);
        }
    }
}
