using System;
using SosariaAI.Configuration;

namespace SosariaAI.Behaviour;

public enum DispositionKind
{
    Lawful,
    Neutral,
    Outlaw
}

/// <summary>
/// Lawful, neutral or outlaw, plus courage and greed. Free. No model.
/// </summary>
public static class DispositionRules
{
    public const string LawfulName = "lawful";
    public const string NeutralName = "neutral";
    public const string OutlawName = "outlaw";
    public const double DefaultCourage = 0.5;

    public static DispositionKind Parse(string raw, bool isPk)
    {
        if (isPk)
        {
            return DispositionKind.Outlaw;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return DispositionKind.Neutral;
        }

        if (raw.Equals(LawfulName, StringComparison.OrdinalIgnoreCase))
        {
            return DispositionKind.Lawful;
        }

        if (raw.Equals(OutlawName, StringComparison.OrdinalIgnoreCase))
        {
            return DispositionKind.Outlaw;
        }

        return DispositionKind.Neutral;
    }

    public static string NameOf(DispositionKind kind) =>
        kind switch
        {
            DispositionKind.Lawful => LawfulName,
            DispositionKind.Outlaw => OutlawName,
            _ => NeutralName
        };

    public static double CourageOf(PersonaDrives drives) =>
        drives?.Valor ?? DefaultCourage;
}
