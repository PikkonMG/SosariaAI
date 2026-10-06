using System.Collections.Generic;
using Server;
using SosariaAI.Economy;

namespace SosariaAI.Mobiles;

/// <summary>
/// The orders a crafter took, read from and written to its save lines. A line that does not
/// decode is dropped on the next write. World thread only.
/// </summary>
public partial class SosariaCharacter
{
    public IReadOnlyList<CraftOrder> CraftOrders
    {
        get
        {
            var orders = new List<CraftOrder>();

            foreach (var line in _orderLines)
            {
                if (CraftOrderCodec.TryDecode(line, out var order))
                {
                    orders.Add(order);
                }
            }

            return orders;
        }
    }

    public void AddCraftOrder(CraftOrder order) => WriteOrders([.. CraftOrders, order]);

    public void ReplaceCraftOrder(CraftOrder order)
    {
        var orders = new List<CraftOrder>(CraftOrders);
        var at = orders.FindIndex(o => o.Id == order.Id);

        if (at >= 0)
        {
            orders[at] = order;
            WriteOrders(orders);
        }
    }

    public void RemoveCraftOrder(string id)
    {
        var orders = new List<CraftOrder>(CraftOrders);

        if (orders.RemoveAll(o => o.Id == id) > 0)
        {
            WriteOrders(orders);
        }
    }

    /// <summary>The open order of <paramref name="buyerSerial"/>, or null.</summary>
    public CraftOrder OrderFor(uint buyerSerial)
    {
        foreach (var order in CraftOrders)
        {
            if (order.BuyerSerial == buyerSerial)
            {
                return order;
            }
        }

        return null;
    }

    /// <summary>True when <paramref name="item"/> is a finished piece held for a buyer.</summary>
    public bool IsOrderPiece(Item item)
    {
        if (item == null)
        {
            return false;
        }

        foreach (var order in CraftOrders)
        {
            foreach (var serial in order.PieceSerials)
            {
                if (serial == item.Serial.Value)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void WriteOrders(IEnumerable<CraftOrder> orders)
    {
        var lines = new List<string>();

        foreach (var order in orders)
        {
            lines.Add(CraftOrderCodec.Encode(order));
        }

        OrderLines = lines;
    }
}
