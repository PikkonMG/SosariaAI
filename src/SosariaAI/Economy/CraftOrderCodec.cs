using System;
using System.Collections.Generic;
using System.Globalization;

namespace SosariaAI.Economy;

/// <summary>
/// An order as one line of the save: id, buyer serial, buyer name, item types, price, deposit,
/// placed time in UTC ticks, and the held piece serials. Fields part at '|', lists at ','. A
/// name loses those two marks. Pure.
/// </summary>
public static class CraftOrderCodec
{
    private const char FieldMark = '|';
    private const char ListMark = ',';
    private const char NameStandIn = ' ';
    private const string HexFormat = "x";
    private const int FieldCount = 8;
    private const int IdField = 0;
    private const int BuyerField = 1;
    private const int NameField = 2;
    private const int TypesField = 3;
    private const int PriceField = 4;
    private const int DepositField = 5;
    private const int PlacedField = 6;
    private const int PiecesField = 7;

    public static string Encode(CraftOrder order)
    {
        var pieces = new List<string>();

        foreach (var serial in order.PieceSerials)
        {
            pieces.Add(serial.ToString(HexFormat, CultureInfo.InvariantCulture));
        }

        return string.Join(
            FieldMark,
            order.Id,
            order.BuyerSerial.ToString(HexFormat, CultureInfo.InvariantCulture),
            Clean(order.BuyerName),
            string.Join(ListMark, order.ItemTypes),
            order.Price.ToString(CultureInfo.InvariantCulture),
            order.Deposit.ToString(CultureInfo.InvariantCulture),
            order.PlacedAt.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture),
            string.Join(ListMark, pieces)
        );
    }

    public static bool TryDecode(string line, out CraftOrder order)
    {
        order = null;
        var fields = line?.Split(FieldMark);

        if (fields is not { Length: FieldCount } || fields[IdField].Length == 0 ||
            !uint.TryParse(fields[BuyerField], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var buyer) ||
            !int.TryParse(fields[PriceField], NumberStyles.Integer, CultureInfo.InvariantCulture, out var price) ||
            !int.TryParse(fields[DepositField], NumberStyles.Integer, CultureInfo.InvariantCulture, out var deposit) ||
            !long.TryParse(fields[PlacedField], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks) ||
            ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
        {
            return false;
        }

        var types = fields[TypesField].Split(ListMark, StringSplitOptions.RemoveEmptyEntries);
        var serials = new List<uint>();

        foreach (var part in fields[PiecesField].Split(ListMark, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!uint.TryParse(part, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var serial))
            {
                return false;
            }

            serials.Add(serial);
        }

        if (types.Length == 0)
        {
            return false;
        }

        order = new CraftOrder(
            fields[IdField], buyer, fields[NameField], types, price, deposit, new DateTime(ticks, DateTimeKind.Utc), serials
        );
        return true;
    }

    private static string Clean(string name) =>
        (name ?? string.Empty).Replace(FieldMark, NameStandIn).Replace(ListMark, NameStandIn);
}
