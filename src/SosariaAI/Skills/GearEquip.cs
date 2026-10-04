using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Spawning;

namespace SosariaAI.Skills;

/// <summary>
/// Mobile.AddItem does not check layers, so two items can share one. EquipItem only checks
/// the item's own layer: a two-handed tool equips over a worn one-handed weapon and the
/// paperdoll shows both hands full. Conflicting layers must be cleared first.
/// </summary>
public static class GearEquip
{
    private static readonly ILogger logger = SosariaLog.For(typeof(GearEquip));

    /// <summary>Layers that must be empty before an item on <paramref name="layer"/> can be worn.</summary>
    public static IReadOnlyList<Layer> ConflictingLayers(Layer layer) =>
        layer switch
        {
            Layer.OneHanded or Layer.TwoHanded => [Layer.OneHanded, Layer.TwoHanded],
            _ => [layer]
        };

    /// <summary>Everything worn on layers that block <paramref name="layer"/>, except <paramref name="keep"/>.</summary>
    public static List<Item> WornConflicts(Mobile mobile, Layer layer, Item keep = null)
    {
        var conflicts = new List<Item>();
        var layers = ConflictingLayers(layer);

        for (var i = 0; i < layers.Count; i++)
        {
            var worn = mobile?.FindItemOnLayer(layers[i]);

            if (worn != null && worn != keep)
            {
                conflicts.Add(worn);
            }
        }

        return conflicts;
    }

    /// <summary>Moves everything on conflicting layers to the backpack. Returns true when the way is clear.</summary>
    public static bool ClearConflicts(Mobile mobile, Layer layer, Item keep = null)
    {
        if (mobile == null)
        {
            return false;
        }

        var conflicts = WornConflicts(mobile, layer, keep);

        for (var i = 0; i < conflicts.Count; i++)
        {
            mobile.AddToBackpack(conflicts[i]);
        }

        return true;
    }

    /// <summary>Wears a tool, moving whatever is in the way to the backpack.</summary>
    public static bool EquipTool(Mobile mobile, Item tool)
    {
        if (mobile == null || tool?.Deleted != false || tool.Layer == Layer.Invalid)
        {
            return false;
        }

        if (tool.Parent == mobile)
        {
            return true;
        }

        ClearConflicts(mobile, tool.Layer, tool);
        return mobile.EquipItem(tool);
    }

    /// <summary>
    /// Saves made before layer conflicts were checked can still hold an item in each
    /// hand, and HasWeapon sees the pair as armed and never repairs it. A shield is
    /// the only legal partner for a weapon hand. Returns the item that must come
    /// off, or null when the pair is legal: weapon plus shield.
    /// </summary>
    public static Item BusyHandConflict(Mobile mobile)
    {
        var one = mobile?.FindItemOnLayer(Layer.OneHanded);
        var two = mobile?.FindItemOnLayer(Layer.TwoHanded);

        if (one == null || two == null || two is BaseShield)
        {
            return null;
        }

        return one is BaseWeapon or Spellbook || two is not (BaseWeapon or Spellbook) ? two : one;
    }

    public static void FreeBusyHands(Mobile mobile)
    {
        if (BusyHandConflict(mobile) is { } drop)
        {
            mobile.AddToBackpack(drop);
        }
    }

    /// <summary>
    /// A person on the road wears a weapon. A hatchet in the pack while an orc
    /// swings is how a neighbour dies unarmed. Empty hands draw the best weapon in the
    /// pack, so the exceptional katana comes out before the pickaxe. An archer whose
    /// quiver ran dry draws its best melee weapon; nothing refills the arrows.
    /// Equipping breaks the words in the hands (Spell.OnCasterEquipping), even a first circle
    /// spell a blow would spare, so the weapon waits for the spell to end: every arrow on a
    /// casting mage drew its weapon through AggressiveAction and broke its Magic Arrow, 87 of
    /// 97 casts in one fight. The combat brain draws it once the spell is over.
    /// </summary>
    public static bool EquipReadyWeapon(Mobile mobile)
    {
        if (mobile == null)
        {
            return false;
        }

        if (mobile.Spell != null)
        {
            return GearScore.HasWeapon(mobile);
        }

        FreeBusyHands(mobile);
        var dryBow = BowWithoutAmmo(mobile);

        if (GearScore.HasWeapon(mobile) && !dryBow)
        {
            return true;
        }

        var weapon = BestPackWeapon(mobile, allowRanged: !dryBow);
        return weapon != null && EquipTool(mobile, weapon) || GearScore.HasWeapon(mobile);
    }

    /// <summary>
    /// A weapon in hand or one in the pack <see cref="EquipReadyWeapon"/> would draw. Read
    /// without touching the hands, so a worker asked to help keeps its tool until it says yes.
    /// </summary>
    public static bool CanArm(Mobile mobile) =>
        GearScore.HasWeapon(mobile) || BestPackWeapon(mobile, allowRanged: true) != null;

    /// <summary>True when the wielded weapon is a bow or crossbow and the pack holds none of its ammunition.</summary>
    public static bool BowWithoutAmmo(Mobile mobile)
    {
        var ranged = mobile?.FindItemOnLayer(Layer.TwoHanded) as BaseRanged ??
                     mobile?.FindItemOnLayer(Layer.OneHanded) as BaseRanged;
        return ranged != null && (mobile.Backpack?.GetAmount(ranged.AmmoType) ?? 0) == 0;
    }

    /// <summary>The highest-ranked weapon in the pack (see <see cref="GearScore.RankOf"/>), or null.</summary>
    public static BaseWeapon BestPackWeapon(Mobile mobile, bool allowRanged)
    {
        var pack = mobile?.Backpack;
        BaseWeapon best = null;
        var bestRank = GearScore.UnarmedScore;

        if (pack == null)
        {
            return null;
        }

        foreach (var weapon in pack.FindItemsByType<BaseWeapon>())
        {
            if (weapon.Deleted || !allowRanged && weapon is BaseRanged)
            {
                continue;
            }

            var rank = GearScore.RankOf(weapon);

            if (best == null || rank > bestRank)
            {
                best = weapon;
                bestRank = rank;
            }
        }

        return best;
    }

    /// <summary>
    /// Wears bought gear, moving whatever the slot already wears to the backpack like a
    /// player changing equipment. A shield stays up beside a one-handed weapon. Anything
    /// that will not equip lands in the pack.
    /// </summary>
    public static void EquipOrPack(Mobile mobile, Item item)
    {
        var conflicts = WornConflicts(mobile, item.Layer, keep: item);

        for (var i = 0; i < conflicts.Count; i++)
        {
            var worn = conflicts[i];

            if (item.Layer == Layer.OneHanded && worn is BaseShield)
            {
                continue;
            }

            mobile.AddToBackpack(worn);
        }

        if (!mobile.EquipItem(item))
        {
            mobile.AddToBackpack(item);
        }
    }

    /// <summary>True when the mobile wears chest or leg armor.</summary>
    public static bool WearsBodyArmor(Mobile mobile) =>
        mobile?.FindItemOnLayer(Layer.InnerTorso) is BaseArmor || mobile?.FindItemOnLayer(Layer.Pants) is BaseArmor;

    /// <summary>
    /// Puts on a first-day kit piece. The starter cloth on its layer (the pants under leg
    /// armor) is thrown out, so the armor shows; a piece that cannot go on, such as a book
    /// beside a halberd, goes to the pack.
    /// </summary>
    public static void WearKitPiece(Mobile mobile, Item piece)
    {
        if (mobile == null || piece?.Deleted != false)
        {
            return;
        }

        var conflicts = WornConflicts(mobile, piece.Layer, piece);

        for (var i = 0; i < conflicts.Count; i++)
        {
            if (conflicts[i] is BaseClothing)
            {
                conflicts[i].Delete();
            }
        }

        if (!mobile.EquipItem(piece))
        {
            mobile.AddToBackpack(piece);
        }
    }

    /// <summary>
    /// A resurrected player drops its death robe once it has clothes again. Every death robe
    /// worn or packed is left on the ground to decay, or thrown out off the map. Returns the
    /// robes shed.
    /// </summary>
    public static int ShedDeathRobes(Mobile mobile)
    {
        if (mobile == null)
        {
            return 0;
        }

        var robes = new List<DeathRobe>();

        if (mobile.FindItemOnLayer(Layer.OuterTorso) is DeathRobe worn)
        {
            robes.Add(worn);
        }

        if (mobile.Backpack != null)
        {
            foreach (var packed in mobile.Backpack.FindItemsByType<DeathRobe>())
            {
                robes.Add(packed);
            }
        }

        var onMap = People.InWorld(mobile);

        for (var i = 0; i < robes.Count; i++)
        {
            if (onMap)
            {
                robes[i].MoveToWorld(mobile.Location, mobile.Map);
                robes[i].BeginDecay();
            }
            else
            {
                robes[i].Delete();
            }
        }

        return robes.Count;
    }

    /// <summary>
    /// Mends a paperdoll that shows the wrong piece: where two items share a layer the
    /// better one stays (armor over cloth, then the higher rank), and a kilt, skirt or dress
    /// over body armor comes off. Everything taken off goes to the pack. Returns the count.
    /// </summary>
    public static int TidyLayers(Mobile mobile)
    {
        if (mobile == null)
        {
            return 0;
        }

        var kept = new Dictionary<Layer, Item>();
        var off = new List<Item>();

        for (var i = 0; i < mobile.Items.Count; i++)
        {
            var item = mobile.Items[i];

            // A ghost's shroud and other fixed pieces are the engine's, not clothing.
            if (!item.Movable || !IsWearLayer(item.Layer))
            {
                continue;
            }

            if (!kept.TryGetValue(item.Layer, out var other))
            {
                kept[item.Layer] = item;
                continue;
            }

            var better = Outranks(item, other) ? item : other;
            off.Add(better == item ? other : item);
            kept[item.Layer] = better;
        }

        var armored = kept.GetValueOrDefault(Layer.InnerTorso) is BaseArmor || kept.GetValueOrDefault(Layer.Pants) is BaseArmor;

        foreach (var item in kept.Values)
        {
            if (armored && OutfitRules.HidesArmor(item.GetType().Name))
            {
                off.Add(item);
            }
        }

        for (var i = 0; i < off.Count; i++)
        {
            mobile.AddToBackpack(off[i]);
        }

        return off.Count;
    }

    /// <summary>
    /// Wears what the pack holds for a layer that is empty or holds a lesser piece, the way a
    /// player dresses again after looting its own body: armor its class wears
    /// (<paramref name="armorCeiling"/> on the <see cref="GearScore"/> scale), a shield, then
    /// cloth. A lesser piece goes to the pack first: leg armor goes on over the cloth pants a
    /// body gets back after a strip. Held off by the pants, 1,214 leg pieces of one run came
    /// out of the spare kit at the bank and went back in on the same visit. Over body armor no kilt, skirt or
    /// dress goes on, and no robe unless the person's look is one. Weapons are drawn by
    /// <see cref="EquipReadyWeapon"/>. Returns the pieces worn.
    /// </summary>
    public static int WearFromPack(Mobile mobile, int armorCeiling, bool robeLook)
    {
        if (mobile?.Backpack == null)
        {
            return 0;
        }

        var worn = WearBestPerLayer(mobile, item => item is BaseArmor && WithinCeiling(item, armorCeiling));
        var armored = WearsBodyArmor(mobile);
        return worn + WearBestPerLayer(mobile, item => item is BaseClothing cloth && FitsOver(cloth, armored, robeLook));
    }

    /// <summary>
    /// Dresses a living person the way a player does: the death robe comes off, each layer
    /// shows one piece, armor its build does not keep on comes off into the pack (a mage in
    /// plate from an older kit), and its own armor and clothes go back on from the pack.
    /// </summary>
    public static void DressForBuild(SosariaCharacter character)
    {
        var template = ClassBuilds.TemplateOf(character);
        ShedDeathRobes(character);
        TidyLayers(character);
        var off = TakeOffAbove(character, GearLadder.KeepCeiling(template));
        WearFromPack(character, GearLadder.WearCeiling(template), OutfitRules.WearsRobe(character.PersonProfile, character.CharacterId));

        if (off.Count > 0 && SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} took off {Pieces}: too heavy for a {Class} to meditate in",
                character.Name,
                string.Join(", ", off.ConvertAll(item => item.GetType().Name)),
                PersonClassRules.Title(character.PersonProfile.Class)
            );
        }
    }

    /// <summary>
    /// True for anything but armor, and for armor at or under <paramref name="armorCeiling"/>
    /// on the <see cref="GearScore"/> scale.
    /// </summary>
    public static bool WithinCeiling(Item item, int armorCeiling) =>
        item is not BaseArmor || GearPlan.ScoreOf(item.GetType().Name) <= armorCeiling;

    /// <summary>Moves every worn armor piece above <paramref name="armorCeiling"/> to the pack. Returns the pieces.</summary>
    public static List<Item> TakeOffAbove(Mobile mobile, int armorCeiling)
    {
        var off = new List<Item>();

        if (mobile == null)
        {
            return off;
        }

        for (var i = 0; i < mobile.Items.Count; i++)
        {
            var item = mobile.Items[i];

            if (item.Movable && IsWearLayer(item.Layer) && !WithinCeiling(item, armorCeiling))
            {
                off.Add(item);
            }
        }

        for (var i = 0; i < off.Count; i++)
        {
            mobile.AddToBackpack(off[i]);
        }

        return off;
    }

    private static bool FitsOver(BaseClothing cloth, bool armored, bool robeLook) =>
        cloth is not DeathRobe &&
        (!armored || !OutfitRules.HidesArmor(cloth.GetType().Name) &&
            (cloth.Layer != Layer.OuterTorso || robeLook && cloth is Robe));

    private static int WearBestPerLayer(Mobile mobile, Func<Item, bool> fits)
    {
        var best = new Dictionary<Layer, Item>();
        var items = mobile.Backpack.Items;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];

            if (!fits(item) || !IsWearLayer(item.Layer) || item.Layer == Layer.OneHanded ||
                mobile.FindItemOnLayer(item.Layer) is { } held && (!held.Movable || !Outranks(item, held)))
            {
                continue;
            }

            if (!best.TryGetValue(item.Layer, out var other) || Outranks(item, other))
            {
                best[item.Layer] = item;
            }
        }

        var worn = 0;

        foreach (var item in best.Values)
        {
            if (mobile.FindItemOnLayer(item.Layer) is { } lesser)
            {
                mobile.AddToBackpack(lesser);
            }

            worn += mobile.EquipItem(item) ? 1 : 0;
        }

        return worn;
    }

    private static bool Outranks(Item item, Item other) =>
        item is BaseArmor != other is BaseArmor
            ? item is BaseArmor
            : GearScore.RankOf(item) > GearScore.RankOf(other);

    /// <summary>A paperdoll layer a person dresses: not hair, the pack, the bank or a mount.</summary>
    private static bool IsWearLayer(Layer layer) =>
        layer is > Layer.Invalid and < Layer.Mount and not (Layer.Hair or Layer.FacialHair or Layer.Backpack);
}
