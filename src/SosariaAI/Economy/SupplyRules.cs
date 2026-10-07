using System;
using System.Collections.Generic;
using SosariaAI.Navigation;
using SosariaAI.Skills;

namespace SosariaAI.Economy;

/// <summary>A consumable a person carries and burns for real.</summary>
public enum SupplyKind
{
    Arrows,
    Bolts,
    Bandages,
    Reagents,
    RecallScrolls,

    /// <summary>Black pearl, blood moss and mandrake: what a sword or bow user with travel magic burns.</summary>
    TravelReagents,

    /// <summary>Runes a mage marks for the places it goes, blank or marked.</summary>
    RecallRunes,

    /// <summary>Lockpicks a lock picker breaks on a miss; the tinker and the provisioner sell them.</summary>
    Lockpicks
}

/// <summary>A supply that ran low: what is carried now and the stock a restock fills to.</summary>
public readonly record struct SupplyNeed(SupplyKind Kind, int Have, int Target)
{
    public int Shortfall => Math.Max(0, Target - Have);
}

/// <summary>
/// What a person burns and how much of it: the facts a supply check reads off the pack,
/// the bank box and the skills. <see cref="LowestReagent"/> is the smallest pile among the
/// eight reagents, because a caster is stopped by the first reagent that runs out.
/// <see cref="TravelCasts"/> is a person who casts only to travel, stopped by the first of
/// the three travel reagents; <see cref="Marks"/> a mage who marks runes.
/// </summary>
public readonly record struct SupplyProfile(
    bool ShootsArrows,
    bool ShootsBolts,
    int Arrows,
    int Bolts,
    bool Heals,
    int Bandages,
    bool Casts,
    int LowestReagent,
    double Magery,
    int Wealth,
    int RecallScrolls,
    bool TravelCasts = false,
    int LowestTravelReagent = 0,
    bool Marks = false,
    int RecallRunes = 0,
    bool PicksLocks = false,
    int Lockpicks = 0
);

/// <summary>
/// T2A had no refills: arrows, bandages, reagents and recall scrolls ran out and the
/// person went back to town for more. A supply below its low mark makes a shopping
/// errand to the shop that sells it; a restock fills to the target. No vendor sold recall
/// scrolls (mage shops stock the first three circles only), so scrolls come from the bank
/// box. The mage shop sells the travel reagents and blank runes. A lock picker out of
/// lockpicks buys them at the tinker, else the provisioner (the engine's SBTinker and
/// SBProvisioner, 12 gold each). Pure. No world objects.
/// </summary>
public static class SupplyRules
{
    public const int ArrowTarget = 200;
    public const int ArrowLowMark = 50;
    public const int BandageTarget = 50;
    public const int BandageLowMark = 10;
    public const int ReagentTarget = 40;
    public const int ReagentLowMark = 8;
    public const int RecallLowMark = 1;
    public const int TravelReagentTarget = 30;
    public const int TravelReagentLowMark = 5;

    /// <summary>Runes a marking mage carries: home bank, a dungeon door, a hunt and a spare.</summary>
    public const int RuneTarget = 4;

    /// <summary>A marking mage restocks runes when looters left it fewer than this.</summary>
    public const int RuneLowMark = 2;

    /// <summary>A lock picker carries a handful: a pick breaks on every miss.</summary>
    public const int LockpickTarget = 20;

    /// <summary>A lock picker goes shopping once its last pick broke.</summary>
    public const int LockpickLowMark = 1;


    public const int ModestWealth = 1000;
    public const int ComfortableWealth = 5000;
    public const int RichWealth = 20000;
    public const int ModestRecallTarget = 2;
    public const int ComfortableRecallTarget = 5;
    public const int RichRecallTarget = 10;
    public const int NoTarget = 0;

    public const string HealerToken = "healer";

    /// <summary>The order a restock goes in: what stops a fight first.</summary>
    private static readonly SupplyKind[] Priority =
    [
        SupplyKind.Arrows,
        SupplyKind.Bolts,
        SupplyKind.Bandages,
        SupplyKind.Reagents,
        SupplyKind.TravelReagents,
        SupplyKind.RecallScrolls,
        SupplyKind.RecallRunes,
        SupplyKind.Lockpicks
    ];

    public static int TargetOf(SupplyKind kind, double magery, int wealth) =>
        kind switch
        {
            SupplyKind.Arrows or SupplyKind.Bolts => ArrowTarget,
            SupplyKind.Bandages => BandageTarget,
            SupplyKind.Reagents => ReagentTarget,
            SupplyKind.TravelReagents => TravelReagentTarget,
            SupplyKind.RecallRunes => RuneTarget,
            SupplyKind.Lockpicks => LockpickTarget,
            _ => RecallTarget(magery, wealth)
        };

    public static int LowMarkOf(SupplyKind kind) =>
        kind switch
        {
            SupplyKind.Arrows or SupplyKind.Bolts => ArrowLowMark,
            SupplyKind.Bandages => BandageLowMark,
            SupplyKind.Reagents => ReagentLowMark,
            SupplyKind.TravelReagents => TravelReagentLowMark,
            SupplyKind.RecallRunes => RuneLowMark,
            SupplyKind.Lockpicks => LockpickLowMark,
            _ => RecallLowMark
        };

    /// <summary>Recall scrolls scale with the purse: a poor person walks, a rich one keeps a stack.</summary>
    public static int RecallTarget(double magery, int wealth)
    {
        if (magery < RecallRules.ScrollMinMagery || wealth < ModestWealth)
        {
            return NoTarget;
        }

        if (wealth >= RichWealth)
        {
            return RichRecallTarget;
        }

        return wealth >= ComfortableWealth ? ComfortableRecallTarget : ModestRecallTarget;
    }

    public static bool IsLow(SupplyKind kind, int have, int target) =>
        target > NoTarget && have < Math.Min(target, LowMarkOf(kind));

    /// <summary>
    /// The units of one supply type the person keeps: the highest target among the supply kinds
    /// of that type it burns (black pearl is a reagent and a travel reagent), and none of a type
    /// it burns under no kind: a fighter keeps no looted reagents.
    /// </summary>
    public static int KeepOf(SupplyProfile profile, IReadOnlyList<SupplyKind> kinds)
    {
        var keep = NoTarget;

        for (var i = 0; i < (kinds?.Count ?? 0); i++)
        {
            if (Uses(profile, kinds[i]))
            {
                keep = Math.Max(keep, TargetOf(kinds[i], profile.Magery, profile.Wealth));
            }
        }

        return keep;
    }

    /// <summary>
    /// What a person spares of a supply to another player: the units past its own keep. It never
    /// sells below its target, so its bank draw (which fills only up to the target) never takes
    /// back what it sold, and a buyer that buys no more than its shortfall never has any to sell.
    /// </summary>
    public static int Spare(int carried, int keep) => Math.Max(0, carried - Math.Max(NoTarget, keep));

    /// <summary>
    /// A supply without which the person cannot fight on: arrows, bolts, bandages and
    /// reagents. Travel reagents, recall scrolls and runes are the way home, and lockpicks a
    /// thief's trade, not the fight: running out sends the person shopping or to its bank box
    /// and never ends a hunt or bars a delve.
    /// </summary>
    public static bool StopsFighting(SupplyKind kind) =>
        kind is not (SupplyKind.RecallScrolls or SupplyKind.TravelReagents or SupplyKind.RecallRunes or SupplyKind.Lockpicks);

    /// <summary>
    /// The low supplies that stop the fight, named for the log ("Bandages, Reagents"); empty
    /// for none. A red's run that would not start said only "the reagents or bandages ran
    /// short", and no log showed which one held it in the Den.
    /// </summary>
    public static string FightStoppers(IReadOnlyList<SupplyNeed> needs)
    {
        var names = new List<string>();

        for (var i = 0; i < (needs?.Count ?? 0); i++)
        {
            if (StopsFighting(needs[i].Kind))
            {
                names.Add(needs[i].Kind.ToString());
            }
        }

        return string.Join(NameSeparator, names);
    }

    private const string NameSeparator = ", ";

    /// <summary>True when a low supply stops the fight: the next step is a shop, not the field.</summary>
    public static bool AnyStopsFighting(IReadOnlyList<SupplyNeed> needs)
    {
        for (var i = 0; i < (needs?.Count ?? 0); i++)
        {
            if (StopsFighting(needs[i].Kind))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when a shop sells one of the low supplies: a reason for a shopping errand.</summary>
    public static bool AnyShopSells(IReadOnlyList<SupplyNeed> needs)
    {
        for (var i = 0; i < (needs?.Count ?? 0); i++)
        {
            if (ShopToken(needs[i].Kind) != null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Every supply the person burns that sits below its low mark, most urgent first.</summary>
    public static List<SupplyNeed> LowNeeds(SupplyProfile profile)
    {
        var needs = new List<SupplyNeed>();

        for (var i = 0; i < Priority.Length; i++)
        {
            var kind = Priority[i];

            if (!Uses(profile, kind))
            {
                continue;
            }

            var have = Have(profile, kind);
            var target = TargetOf(kind, profile.Magery, profile.Wealth);

            if (IsLow(kind, have, target))
            {
                needs.Add(new SupplyNeed(kind, have, target));
            }
        }

        return needs;
    }

    /// <summary>Every supply the person burns that is short of its target, most urgent first.</summary>
    public static List<SupplyNeed> Shortfalls(SupplyProfile profile)
    {
        var needs = new List<SupplyNeed>();

        for (var i = 0; i < Priority.Length; i++)
        {
            var kind = Priority[i];

            if (!Uses(profile, kind))
            {
                continue;
            }

            var need = new SupplyNeed(kind, Have(profile, kind), TargetOf(kind, profile.Magery, profile.Wealth));

            if (need.Shortfall > 0)
            {
                needs.Add(need);
            }
        }

        return needs;
    }

    /// <summary>
    /// The low supplies a red can refill: those its bank box holds enough of to lift off the
    /// low mark (<paramref name="bankLifts"/>), or those a Den shop has on its shelf
    /// (<paramref name="denSells"/>). A red caster out of reagents with none banked rode out,
    /// turned back at once "the reagents or bandages ran short", found no shop, and rode out
    /// again: 292 times in one night. With nothing to refill from it fights on with its weapon.
    /// </summary>
    public static List<SupplyNeed> RedRefillable(
        IReadOnlyList<SupplyNeed> needs,
        Func<SupplyNeed, bool> bankLifts,
        Func<SupplyNeed, bool> denSells
    )
    {
        var kept = new List<SupplyNeed>();

        for (var i = 0; i < (needs?.Count ?? 0); i++)
        {
            if (bankLifts?.Invoke(needs[i]) == true || denSells?.Invoke(needs[i]) == true)
            {
                kept.Add(needs[i]);
            }
        }

        return kept;
    }

    /// <summary>The shop that sells a supply, or null when no shop does.</summary>
    public static string ShopToken(SupplyKind kind) =>
        kind switch
        {
            SupplyKind.Arrows or SupplyKind.Bolts => ShopFinder.BowyerToken,
            SupplyKind.Bandages => HealerToken,
            SupplyKind.Reagents or SupplyKind.TravelReagents or SupplyKind.RecallRunes => ShopFinder.MageToken,
            SupplyKind.Lockpicks => ShopFinder.TinkerToken,
            _ => null
        };

    /// <summary>The shops that stock the supply, first choice first; empty when no shop does.</summary>
    public static List<string> ShopTokens(SupplyKind kind)
    {
        var tokens = new List<string>();

        if (ShopToken(kind) is { } first)
        {
            tokens.Add(first);
        }

        if (AlternateShopToken(kind) is { } second)
        {
            tokens.Add(second);
        }

        return tokens;
    }

    /// <summary>A second shop that stocks the supply when the first is out of reach.</summary>
    public static string AlternateShopToken(SupplyKind kind) =>
        kind switch
        {
            SupplyKind.Arrows or SupplyKind.Bolts => ShopFinder.ProvisionerToken,
            SupplyKind.Reagents or SupplyKind.TravelReagents => ShopFinder.AlchemistToken,
            SupplyKind.Lockpicks => ShopFinder.ProvisionerToken,
            _ => null
        };

    /// <summary>
    /// True when scissors make the supply from cloth a tailor or a weaver sells: bandages, the
    /// way a player made them when the healer's 20 were gone (<see cref="ClothBandageRules"/>).
    /// </summary>
    public static bool MadeFromCloth(SupplyKind kind) => kind == SupplyKind.Bandages;

    private static bool Uses(SupplyProfile profile, SupplyKind kind) =>
        kind switch
        {
            SupplyKind.Arrows => profile.ShootsArrows,
            SupplyKind.Bolts => profile.ShootsBolts,
            SupplyKind.Bandages => profile.Heals,
            SupplyKind.Reagents => profile.Casts,
            SupplyKind.TravelReagents => profile.TravelCasts && !profile.Casts,
            SupplyKind.RecallRunes => profile.Marks,
            SupplyKind.Lockpicks => profile.PicksLocks,
            _ => profile.Magery >= RecallRules.ScrollMinMagery
        };

    private static int Have(SupplyProfile profile, SupplyKind kind) =>
        kind switch
        {
            SupplyKind.Arrows => profile.Arrows,
            SupplyKind.Bolts => profile.Bolts,
            SupplyKind.Bandages => profile.Bandages,
            SupplyKind.Reagents => profile.LowestReagent,
            SupplyKind.TravelReagents => profile.LowestTravelReagent,
            SupplyKind.RecallRunes => profile.RecallRunes,
            SupplyKind.Lockpicks => profile.Lockpicks,
            _ => profile.RecallScrolls
        };
}
