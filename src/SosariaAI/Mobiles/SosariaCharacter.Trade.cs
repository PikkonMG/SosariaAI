using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Behaviour;

namespace SosariaAI.Mobiles;

/// <summary>
/// A deal with a person settles through a real trade window when they can see one: the session
/// builds the SecureTrade by hand because <see cref="Mobile.OpenTrade" /> wants a NetState on
/// both sides and a character has none. A drop on the character during a deal lands on the
/// person's side of that window; a person whose window cannot open still pays by dropping the
/// coin or the goods on the character — the bare hand-off. Anything dropped outside a deal is
/// refused and the engine bounces it back.
/// </summary>
public partial class SosariaCharacter : PlayerMobile
{
    public override bool OnDragDrop(Mobile from, Item dropped) =>
        from == this ? base.OnDragDrop(from, dropped) : TradeHandOff.Receive(this, from, dropped);

    /// <summary>
    /// A save that caught a live trade window leaves its container on the character with a dead
    /// trade behind it; the goods or coin inside go back to the pack.
    /// </summary>
    private void FoldStaleTrades()
    {
        for (var i = Items.Count - 1; i >= 0; --i)
        {
            if (Items[i] is not SecureTradeContainer { Trade: null or { Valid: false } } container)
            {
                continue;
            }

            for (var j = container.Items.Count - 1; j >= 0; --j)
            {
                if (container.Items[j] is not VirtualCheck)
                {
                    AddToBackpack(container.Items[j]);
                }
            }

            container.Delete();
        }
    }
}
