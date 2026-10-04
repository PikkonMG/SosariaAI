using System;
using System.Collections.Generic;
using SosariaAI.Combat;
using SosariaAI.Spawning;

namespace SosariaAI.Economy;

/// <summary>
/// Who wants what at a bank. A mage buys reagents and scrolls, a fighter weapons and armor, a
/// smith ingots, a merchant anything it can sell on. A room does not buy every shout: the odds
/// come from the goods, a maker's mark or magic makes them sell, and a verdict sticks for a
/// while so shouting the same thing again does not change anyone's mind. Pure.
/// </summary>
public static class TradeDemandRules
{
    public const int PercentScale = 100;
    public const int ExceptionalDemandBonus = 15;
    public const int MagicDemandBonus = 30;
    public const int MaxDemandPercent = 95;

    /// <summary>Minutes a "no" to a seller's goods stands before the room looks again.</summary>
    public const int RefusalMinutes = 15;

    public static readonly TimeSpan RefusalHold = TimeSpan.FromMinutes(RefusalMinutes);

    public static TradeAppetite AppetiteOf(PersonClass personClass, CombatStyle style)
    {
        var appetite = personClass switch
        {
            PersonClass.Warrior or PersonClass.Paladin or PersonClass.Samurai => TradeAppetite.Melee | TradeAppetite.Healing,
            PersonClass.Fencer or PersonClass.Ninja => TradeAppetite.Fencing | TradeAppetite.Healing,
            PersonClass.Mage or PersonClass.Necromancer or PersonClass.TreasureHunter => TradeAppetite.Caster,
            PersonClass.Archer or PersonClass.Ranger => TradeAppetite.Archery | TradeAppetite.Healing,
            PersonClass.Healer => TradeAppetite.Healing | TradeAppetite.Caster,
            PersonClass.Tamer => TradeAppetite.Healing,
            PersonClass.Merchant => TradeAppetite.Merchant,
            PersonClass.Smith or PersonClass.Miner or PersonClass.Tinker => TradeAppetite.Smithing,
            PersonClass.Tailor => TradeAppetite.Tailoring,
            PersonClass.Carpenter or PersonClass.Lumberjack or PersonClass.Bowyer => TradeAppetite.Carpentry,
            PersonClass.Alchemist or PersonClass.Scribe => TradeAppetite.Caster,
            _ => TradeAppetite.None
        };

        if (style == CombatStyle.Mage)
        {
            appetite |= TradeAppetite.Caster;
        }
        else if (style == CombatStyle.Archer)
        {
            appetite |= TradeAppetite.Archery;
        }

        return appetite | TradeAppetite.Everyone;
    }

    public static bool Wants(TradeAppetite appetite, GoodsRow row) =>
        row != null && (row.Appetite & appetite) != TradeAppetite.None;

    /// <summary>How often, out of a hundred, a room takes an interest in these goods.</summary>
    public static int DemandPercent(GoodsClaim claim)
    {
        if (claim.Row == null)
        {
            return 0;
        }

        var percent = claim.Row.DemandPercent;

        if (claim.Exceptional)
        {
            percent += ExceptionalDemandBonus;
        }

        if (claim.MagicLevel > Appraisal.NoMagic)
        {
            percent += MagicDemandBonus;
        }

        return Math.Min(MaxDemandPercent, percent);
    }

    /// <summary>The sticky verdict: rolled once per seller and kind of goods, then remembered.</summary>
    public static bool RoomBites(GoodsClaim claim, int roll) => Math.Abs(roll % PercentScale) < DemandPercent(claim);

    /// <summary>The market row a burned supply trades under, or null when players did not trade it.</summary>
    public static GoodsRow RowFor(SupplyKind kind) =>
        Appraisal.RowByKey(
            kind switch
            {
                SupplyKind.Arrows => Appraisal.ArrowsKey,
                SupplyKind.Bolts => Appraisal.BoltsKey,
                SupplyKind.Bandages => Appraisal.BandagesKey,
                SupplyKind.Reagents or SupplyKind.TravelReagents => Appraisal.ReagentsKey,
                SupplyKind.RecallScrolls => Appraisal.RecallKey,
                _ => null
            }
        );

    /// <summary>
    /// What a person would shout WTB for: the supply it is shortest of, else a maker's piece of
    /// the gear its class uses. Only goods the purse can pay for at the middle of the band, so a
    /// WTB is always backed by coin. Null when nothing fits.
    /// </summary>
    public static GoodsClaim? WantFor(SupplyNeed? need, TradeAppetite appetite, int purse, int roll)
    {
        if (need is { Shortfall: > 0 } supply && RowFor(supply.Kind) is { } supplyRow)
        {
            var claim = new GoodsClaim(supplyRow, supply.Shortfall, false, Appraisal.NoMagic);

            if (claim.Value(Appraisal.MidRoll) <= purse)
            {
                return claim;
            }
        }

        var gear = new List<GoodsClaim>();

        foreach (var row in Appraisal.Rows)
        {
            if (row.IsGear && Wants(appetite & ~TradeAppetite.Everyone, row))
            {
                var claim = new GoodsClaim(row, 1, true, Appraisal.NoMagic);

                if (claim.Value(Appraisal.MidRoll) <= purse)
                {
                    gear.Add(claim);
                }
            }
        }

        return gear.Count == 0 ? null : gear[Math.Abs(roll) % gear.Count];
    }
}
