using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Reads what a person burns and how much it still carries. Arrows leave the pack as the
/// bow fires, bandages as they are applied, reagents as spells are cast; nothing puts them
/// back but a purchase or a trip to the bank box.
/// </summary>
public static class SupplyCheck
{
    /// <summary>Magery at which a person casts in a fight and keeps a reagent stock.</summary>
    public const double CasterMinMagery = 50;

    /// <summary>Healing at which a person bandages itself and keeps a bandage stock.</summary>
    public const double HealerMinHealing = 30;

    public static readonly Type[] ReagentTypes =
    [
        typeof(BlackPearl),
        typeof(Bloodmoss),
        typeof(Garlic),
        typeof(Ginseng),
        typeof(MandrakeRoot),
        typeof(Nightshade),
        typeof(SpidersSilk),
        typeof(SulfurousAsh)
    ];

    /// <summary>Recall and Mark burn one of each: black pearl, blood moss and mandrake.</summary>
    public static readonly Type[] TravelReagentTypes = [typeof(BlackPearl), typeof(Bloodmoss), typeof(MandrakeRoot)];

    private static readonly Type[] RuneTypes = [typeof(RecallRune)];
    private static readonly Type[] ArrowTypes = [typeof(Arrow)];
    private static readonly Type[] BoltTypes = [typeof(Bolt)];
    private static readonly Type[] BandageTypes = [typeof(Bandage)];
    private static readonly Type[] RecallTypes = [typeof(RecallScroll)];
    private static readonly Type[] LockpickTypes = [typeof(Lockpick)];

    /// <summary>
    /// Every type of every supply kind, what <see cref="TakeFromBank"/> may draw back into the
    /// pack, and the scissors bandages are cut with. Cloth is not kept: it is cut the moment it is
    /// bought, and a tailor's cloth is its trade stock, to bank and sell as before.
    /// </summary>
    private static readonly Type[] RestockedTypes = AllRestockedTypes();

    /// <summary>
    /// True when the item is of a type some supply kind restocks (<see cref="TypesOf"/>), or the
    /// scissors a bandage maker cuts bandages with. Such an item stays in the pack: a thief
    /// banked its twenty lockpicks, the supply draw took them straight back out, and the pick
    /// left in the pack sent it to the counter again every two seconds, over a thousand times
    /// in under an hour. Scissors sold at the counter or tossed on the bank floor would be
    /// bought again on the next errand. Cloth is not one: it is cut the moment it is bought.
    /// </summary>
    public static bool IsRestocked(Item item)
    {
        if (item == null)
        {
            return false;
        }

        for (var i = 0; i < RestockedTypes.Length; i++)
        {
            if (RestockedTypes[i].IsInstanceOfType(item))
            {
                return true;
            }
        }

        return false;
    }

    private static Type[] AllRestockedTypes()
    {
        var types = new List<Type>();

        foreach (var kind in Enum.GetValues<SupplyKind>())
        {
            foreach (var type in TypesOf(kind))
            {
                if (!types.Contains(type))
                {
                    types.Add(type);
                }
            }
        }

        types.Add(typeof(Scissors));
        return [.. types];
    }

    public static SupplyProfile ProfileOf(SosariaCharacter character)
    {
        var pack = character?.Backpack;

        if (pack == null)
        {
            return default;
        }

        var ammo = AmmoType(character);
        var book = RuneShelf.Book(character);

        return new SupplyProfile(
            ammo == typeof(Arrow),
            ammo == typeof(Bolt),
            pack.GetAmount(typeof(Arrow)),
            pack.GetAmount(typeof(Bolt)),
            character.Build?.CanHeal == true || character.Build?.IsFighter == true ||
            character.Skills.Healing.Value >= HealerMinHealing,
            pack.GetAmount(typeof(Bandage)),
            character.Build?.Style == CombatStyle.Mage || character.Skills.Magery.Value >= CasterMinMagery,
            Lowest(pack, ReagentTypes),
            character.Skills.Magery.Value,
            VendorDeal.GoldHeld(character),
            // A runebook's charges are recalls in hand; its entries are runes kept.
            pack.GetAmount(typeof(RecallScroll)) + (book?.CurCharges ?? 0),
            character.Skills.Magery.Value >= RecallRules.MinMagery,
            Lowest(pack, TravelReagentTypes),
            character.Skills.Magery.Value >= MarkRules.MinMagery,
            pack.GetAmount(typeof(RecallRune)) + (book?.Entries.Count ?? 0),
            character.Definition?.UsesSkill(SkillKinds.Lockpick) == true,
            pack.GetAmount(typeof(Lockpick))
        );
    }

    /// <summary>
    /// Supplies below their low mark, most urgent first. A person with none is stocked. A red
    /// counts only what it can refill: what its bank box holds, or what a Den shop has on its
    /// shelf for every type it is short of (<see cref="SupplyRules.RedRefillable"/>).
    /// </summary>
    public static List<SupplyNeed> LowNeeds(SosariaCharacter character)
    {
        var needs = SupplyRules.LowNeeds(ProfileOf(character));

        return character != null && PkRules.IsRed(character.Kills)
            ? SupplyRules.RedRefillable(
                needs,
                need => BankLifts(character, need),
                need => DenStock.SellsAll(need.Kind, BuyLines(character.Backpack, need))
            )
            : needs;
    }

    /// <summary>
    /// True when what the bank box holds would lift the need off its low mark: every type of
    /// the supply, counted as <see cref="BuyLines"/> counts it, with the box's units added.
    /// </summary>
    private static bool BankLifts(SosariaCharacter character, SupplyNeed need)
    {
        var bank = character.BankBox;
        var pack = character.Backpack;
        var types = TypesOf(need.Kind);
        var lowest = int.MaxValue;

        for (var i = 0; i < types.Count; i++)
        {
            var have = types.Count == 1 ? need.Have : pack?.GetAmount(types[i]) ?? 0;
            lowest = Math.Min(lowest, have + (bank?.GetAmount(types[i]) ?? 0));
        }

        return !SupplyRules.IsLow(need.Kind, lowest, need.Target);
    }

    /// <summary>
    /// True when a supply the person fights with ran low: the next step should be a shopping
    /// errand. A person short only of recall scrolls fights on.
    /// </summary>
    public static bool IsLow(SosariaCharacter character) => SupplyRules.AnyStopsFighting(LowNeeds(character));

    /// <summary>
    /// True when a supply a shop sells ran low, fight-stopping or not: the travel reagents
    /// and blank runes send a mage to the mage shop as surely as bandages send a healer.
    /// </summary>
    public static bool WantsShopping(SosariaCharacter character) => SupplyRules.AnyShopSells(LowNeeds(character));

    public static IReadOnlyList<Type> TypesOf(SupplyKind kind) =>
        kind switch
        {
            SupplyKind.Arrows => ArrowTypes,
            SupplyKind.Bolts => BoltTypes,
            SupplyKind.Bandages => BandageTypes,
            SupplyKind.Reagents => ReagentTypes,
            SupplyKind.TravelReagents => TravelReagentTypes,
            SupplyKind.RecallRunes => RuneTypes,
            SupplyKind.Lockpicks => LockpickTypes,
            _ => RecallTypes
        };

    /// <summary>
    /// What to buy for a need: one line per type still short of the target. Reagents are
    /// counted one by one, since each spell wants its own mix. A single-type supply keeps
    /// the need's own count, which holds a runebook's charges and entries.
    /// </summary>
    public static List<(Type Type, int Amount)> BuyLines(Container pack, SupplyNeed need)
    {
        var lines = new List<(Type, int)>();
        var types = TypesOf(need.Kind);

        for (var i = 0; i < types.Count; i++)
        {
            var have = types.Count == 1 ? need.Have : pack?.GetAmount(types[i]) ?? 0;
            var shortfall = new SupplyNeed(need.Kind, have, need.Target).Shortfall;

            if (shortfall > 0)
            {
                lines.Add((types[i], shortfall));
            }
        }

        return lines;
    }

    /// <summary>Units the bank box holds toward supplies that ran low: a reason to visit the bank.</summary>
    public static int InBankBox(SosariaCharacter character) =>
        MoveFromBank(character, SupplyRules.LowNeeds(ProfileOf(character)), move: false);

    /// <summary>
    /// Moves supplies out of the bank box into the pack up to each target, as a player drags
    /// its reserve out of the box. Returns the units moved.
    /// </summary>
    public static int TakeFromBank(SosariaCharacter character) =>
        MoveFromBank(character, SupplyRules.Shortfalls(ProfileOf(character)), move: true);

    /// <summary>Moves <paramref name="amount"/> units of a stack into a container, splitting the stack when needed.</summary>
    public static void MoveUnits(Item stack, int amount, Container to)
    {
        if (stack == null || to == null || amount <= 0)
        {
            return;
        }

        if (amount < stack.Amount)
        {
            Mobile.LiftItemDupe(stack, amount);
        }

        to.DropItem(stack);
    }

    /// <summary>Moves units for one need out of the bank box into the pack. Returns the units moved.</summary>
    public static int TakeFromBank(SosariaCharacter character, SupplyNeed need) =>
        MoveNeed(character?.BankBox, character?.Backpack, need, move: true);

    private static int MoveFromBank(SosariaCharacter character, List<SupplyNeed> needs, bool move)
    {
        var total = 0;

        for (var n = 0; n < needs.Count; n++)
        {
            total += MoveNeed(character?.BankBox, character?.Backpack, needs[n], move);
        }

        return total;
    }

    private static int MoveNeed(Container bank, Container pack, SupplyNeed need, bool move)
    {
        if (bank == null || pack == null)
        {
            return 0;
        }

        var total = 0;
        var lines = BuyLines(pack, need);

        for (var l = 0; l < lines.Count; l++)
        {
            var (type, amount) = lines[l];
            var left = Math.Min(amount, bank.GetAmount(type));
            total += left;

            while (move && left > 0 && bank.FindItemByType(type) is { } stack)
            {
                var take = Math.Min(left, stack.Amount);
                MoveUnits(stack, take, pack);
                left -= take;
            }
        }

        return total;
    }

    private static int Lowest(Container pack, Type[] types)
    {
        var lowest = int.MaxValue;

        for (var i = 0; i < types.Length; i++)
        {
            lowest = Math.Min(lowest, pack.GetAmount(types[i]));
        }

        return lowest;
    }

    /// <summary>The ammunition of the ranged weapon the person wields or carries, or null.</summary>
    public static Type AmmoType(Mobile person)
    {
        var ranged = person?.FindItemOnLayer(Layer.TwoHanded) as BaseRanged ??
                     person?.FindItemOnLayer(Layer.OneHanded) as BaseRanged ??
                     person?.Backpack?.FindItemByType<BaseRanged>();
        return ranged?.AmmoType;
    }
}
