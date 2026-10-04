using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Mobiles;

namespace SosariaAI.Behaviour;

/// <summary>
/// A real secure-trade window between a character and a person at a keyboard. The engine's own
/// drop-to-trade path (<see cref="Mobile.OpenTrade" />) wants a NetState on both sides and a
/// character has none, so the session builds the <see cref="SecureTrade" /> by hand instead:
/// packets aimed at the character's missing client no-op, the person's client gets the window,
/// their drops and accept clicks run the usual packet handler, and items put on the character's
/// side still reach the person's screen. The trade never lands in the person's NetState trade
/// list, so the engine's own range and death sweeps do not touch it; the owning session folds it.
/// World thread only.
/// </summary>
public sealed class TradeWindow
{
    private readonly SecureTrade _trade;

    private TradeWindow(Mobile partner, SosariaCharacter character) =>
        _trade = new SecureTrade(partner, character);

    /// <summary>The person's half of the window: they pay the coin or hand over the goods here.</summary>
    public SecureTradeContainer Theirs => _trade.From.Container;

    /// <summary>The character's half.</summary>
    public SecureTradeContainer Ours => _trade.To.Container;

    /// <summary>False once the swap ran or the window folded.</summary>
    public bool IsOpen => _trade.Valid;

    /// <summary>The person checked their accept box.</summary>
    public bool TheirAccepted => _trade.From.Accepted;

    /// <summary>The character checked its accept box.</summary>
    public bool WeAccepted => _trade.To.Accepted;

    /// <summary>
    /// No coin promised through the window's currency fields. Account gold moves only between
    /// two accounts and a character has none, so an amount written there can never settle — the
    /// price has to be coins on the person's side.
    /// </summary>
    public bool TheirLedgerEmpty => _trade.From.Gold == 0 && _trade.From.Plat == 0;

    /// <summary>
    /// True when a window may open on this person: trading is on, they are a live player who did
    /// not refuse trades, and they are not inside another trade. A person without a NetState — a
    /// test mobile or a just-logged-off player — still gets the window; nothing reaches a screen
    /// and the session's own checks fold it.
    /// </summary>
    public static bool CanOpen(Mobile partner) =>
        ServerFeatureFlags.PlayerTrading &&
        partner is PlayerMobile { Alive: true, Deleted: false, RefuseTrades: false }
            and not SosariaCharacter &&
        !partner.HasTrade;

    /// <summary>Opens the window on the person's client, or null when <see cref="CanOpen" /> says no.</summary>
    public static TradeWindow Open(Mobile partner, SosariaCharacter character) =>
        CanOpen(partner) ? new TradeWindow(partner, character) : null;

    /// <summary>
    /// A dropped item lands on the person's half, the way the engine seats the first item of a
    /// drop-to-trade. False when the window closed or the container refuses it; the engine
    /// bounces the item back.
    /// </summary>
    public bool Offer(Item dropped) => IsOpen && Theirs.TryDropItem(_trade.From.Mobile, dropped, true);

    /// <summary>Goods or coin on the character's half, where the person sees them.</summary>
    public void Stock(Item item)
    {
        if (IsOpen)
        {
            Ours.DropItem(item);
        }
    }

    /// <summary>Checks the character's box; the swap runs when the person has checked theirs.</summary>
    public void Accept()
    {
        if (!IsOpen)
        {
            return;
        }

        _trade.To.Accepted = true;
        _trade.Update();
    }

    /// <summary>Folds the window; the engine hands each side's contents back to their owner.</summary>
    public void Cancel() => _trade.Cancel();
}
