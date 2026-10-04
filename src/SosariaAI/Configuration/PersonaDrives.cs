using System;
using System.Collections.Generic;

namespace SosariaAI.Configuration;

public sealed class PersonaDrives
{
    public const string GreedKey = "greed";
    public const string CautionKey = "caution";
    public const string ValorKey = "valor";
    public const double NeutralValue = 0.5;
    public const double MinValue = 0;
    public const double MaxValue = 1;

    public static PersonaDrives Neutral { get; } = new(NeutralValue, NeutralValue, NeutralValue, isCustom: false);

    public PersonaDrives(double greed, double caution, double valor, bool isCustom)
    {
        Greed = Clamp(greed);
        Caution = Clamp(caution);
        Valor = Clamp(valor);
        IsCustom = isCustom;
    }

    public double Greed { get; }

    public double Caution { get; }

    public double Valor { get; }

    public bool IsCustom { get; }

    public static PersonaDrives From(IReadOnlyDictionary<string, double> drives)
    {
        if (drives == null || drives.Count == 0)
        {
            return Neutral;
        }

        return new PersonaDrives(
            Read(drives, GreedKey),
            Read(drives, CautionKey),
            Read(drives, ValorKey),
            isCustom: true
        );
    }

    private static double Read(IReadOnlyDictionary<string, double> drives, string key)
    {
        foreach (var pair in drives)
        {
            if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return NeutralValue;
    }

    private static double Clamp(double value) =>
        value < MinValue ? MinValue : value > MaxValue ? MaxValue : value;
}
