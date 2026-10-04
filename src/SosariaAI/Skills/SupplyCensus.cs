using System;
using System.Collections.Generic;
using System.Text;
using Server.Items;
using Server.Logging;
using SosariaAI.Admin;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Logging;

namespace SosariaAI.Skills;

/// <summary>
/// Every ten minutes one activity line says, for the live people whose fighting supplies ran
/// low, which supply ran low first and why it is not refilled: no gold in the pack, no stocked
/// shop in reach that sells it, or a refill it can make. One run had 1,111 of 1,807 live people
/// with supplies low and no line to say why. It reads a copy of the roster: a shop look can
/// make the engine create a vendor's display beast. World thread only.
/// </summary>
public static class SupplyCensus
{
    public const string NoGold = "no gold";
    public const string NoShop = "no shop in reach";
    public const string CanRefill = "can refill";

    private static readonly ILogger logger = SosariaLog.For(typeof(SupplyCensus));
    private static int _minutes;

    public static void Initialize() => ActivityPulse.MinutePassed += OnMinute;

    /// <summary>One person's reason: its first fight-stopping low supply and why it is not refilled.</summary>
    public static string Reason(SupplyKind kind, bool hasGold, bool hasErrand) =>
        $"{kind.ToString().ToLowerInvariant()} {(!hasGold ? NoGold : hasErrand ? CanRefill : NoShop)}";

    /// <summary>"Supplies low: bandages no gold 400, reagents can refill 50 (1111 low of 1807 live)".</summary>
    public static string Line(IReadOnlyDictionary<string, int> reasons, int low, int live) =>
        CensusText.Counted(new StringBuilder("Supplies low: "), reasons)
            .Append(" (").Append(low).Append(" low of ").Append(live).Append(" live)").ToString();

    private static void OnMinute()
    {
        if (!CensusText.Due(++_minutes) || !SosariaSettings.LogActivity)
        {
            return;
        }

        var reasons = new Dictionary<string, int>(StringComparer.Ordinal);
        var low = 0;
        var live = 0;

        foreach (var character in FleetStatus.InWorld())
        {
            if (!character.Alive)
            {
                continue;
            }

            live++;

            if (FirstStopping(SupplyCheck.LowNeeds(character)) is not { } kind)
            {
                continue;
            }

            low++;
            var reason = Reason(
                kind,
                character.Backpack?.GetAmount(typeof(Gold)) > 0,
                VendorBuySkill.HasSupplyErrand(character)
            );
            reasons[reason] = reasons.GetValueOrDefault(reason) + 1;
        }

        logger.Information("{Line}", Line(reasons, low, live));
    }

    private static SupplyKind? FirstStopping(List<SupplyNeed> needs)
    {
        for (var i = 0; i < needs.Count; i++)
        {
            if (SupplyRules.StopsFighting(needs[i].Kind))
            {
                return needs[i].Kind;
            }
        }

        return null;
    }
}
