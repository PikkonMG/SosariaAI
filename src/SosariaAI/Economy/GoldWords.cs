using System;
using System.Collections.Generic;
using System.Globalization;

namespace SosariaAI.Economy;

/// <summary>A number heard in a line: its value, where it stood, and whether it was plainly money.</summary>
public readonly record struct HeardNumber(int Value, int Position, bool IsMoney);

/// <summary>
/// Gold the way players typed it: "750", "1.5k", "5k", "2m", "1,200", "5000gp". Prices are
/// said in round steps, so a haggle never names 4,137 gold. Pure.
/// </summary>
public static class GoldWords
{
    public const int Thousand = 1000;
    public const int Million = 1000000;
    public const int KiloDecimalStep = 100;
    public const int SmallestPrice = 1;

    /// <summary>A price at or above this is said in steps of <see cref="LargeStep"/>.</summary>
    public const int LargeFrom = 20000;

    public const int LargeStep = 1000;
    public const int MediumFrom = 5000;
    public const int MediumStep = 250;
    public const int SmallFrom = 1000;
    public const int SmallStep = 50;
    public const int TinyFrom = 100;
    public const int TinyStep = 5;

    /// <summary>A bare number below this, said in a haggle over thousands, means thousands: "would you do 3".</summary>
    public const int ShortThousandsBelow = 100;

    private const char KiloSuffix = 'k';
    private const char MegaSuffix = 'm';
    private const char DecimalPoint = '.';
    private const char GroupSeparator = ',';
    private static readonly string[] MoneySuffixes = ["gp", "gold", "g"];

    /// <summary>Rounds down to the step a player would say. Never below one gold.</summary>
    public static int RoundSpoken(int gold)
    {
        if (gold <= SmallestPrice)
        {
            return SmallestPrice;
        }

        var step = gold >= LargeFrom ? LargeStep
            : gold >= MediumFrom ? MediumStep
            : gold >= SmallFrom ? SmallStep
            : gold >= TinyFrom ? TinyStep
            : SmallestPrice;
        return Math.Max(SmallestPrice, gold / step * step);
    }

    /// <summary>"750" below a thousand, then "1.5k", "5k", "12k"; "1250" when a tenth cannot say it.</summary>
    public static string Spoken(int gold)
    {
        if (gold < Thousand || gold % KiloDecimalStep != 0)
        {
            return Math.Max(SmallestPrice, gold).ToString(CultureInfo.InvariantCulture);
        }

        var hundreds = gold / KiloDecimalStep;
        var whole = hundreds / (Thousand / KiloDecimalStep);
        var tenth = hundreds % (Thousand / KiloDecimalStep);

        return tenth == 0
            ? $"{whole.ToString(CultureInfo.InvariantCulture)}k"
            : $"{whole.ToString(CultureInfo.InvariantCulture)}.{tenth.ToString(CultureInfo.InvariantCulture)}k";
    }

    /// <summary>
    /// Reads one word as a number. "4k", "2.5k", "1m" and "500gp" are money; "3500" and "1,200"
    /// are plain numbers that may be money or a count.
    /// </summary>
    public static bool TryRead(string word, out int value, out bool isMoney)
    {
        value = 0;
        isMoney = false;

        if (string.IsNullOrEmpty(word) || !char.IsDigit(word[0]) && word[0] != DecimalPoint)
        {
            return false;
        }

        var text = word.ToLowerInvariant().Replace(GroupSeparator.ToString(), string.Empty);

        foreach (var suffix in MoneySuffixes)
        {
            if (text.Length > suffix.Length && text.EndsWith(suffix, StringComparison.Ordinal))
            {
                text = text[..^suffix.Length];
                isMoney = true;
                break;
            }
        }

        var scale = 1;
        var last = text.Length > 0 ? text[^1] : '\0';

        if (last == KiloSuffix || last == MegaSuffix)
        {
            scale = last == KiloSuffix ? Thousand : Million;
            text = text[..^1];
            isMoney = true;
        }

        if (!double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) ||
            number <= 0)
        {
            return false;
        }

        var scaled = number * scale;

        if (scaled > int.MaxValue || scaled < 1)
        {
            return false;
        }

        value = (int)scaled;
        return true;
    }

    /// <summary>Every number in the line, in the order heard.</summary>
    public static List<HeardNumber> NumbersIn(IReadOnlyList<string> words)
    {
        var found = new List<HeardNumber>();

        for (var i = 0; i < (words?.Count ?? 0); i++)
        {
            if (TryRead(words[i], out var value, out var money))
            {
                found.Add(new HeardNumber(value, i, money));
            }
        }

        return found;
    }

    /// <summary>
    /// A bare small number in a haggle over thousands is shorthand: "would you do 3" for 3k.
    /// Anything else stands as said.
    /// </summary>
    public static int InContext(HeardNumber number, int standing) =>
        !number.IsMoney && number.Value < ShortThousandsBelow && standing >= SmallFrom
            ? number.Value * Thousand
            : number.Value;
}
