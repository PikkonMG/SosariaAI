using Server;
using Server.Engines.Craft;
using Server.Items;
using SosariaAI.Behaviour;
using SosariaAI.Economy;
using SosariaAI.Spawning;
using SosariaAI.Skills;

namespace SosariaAI.Combat;

/// <summary>
/// Finishes a fresh character's worn kit the way the period made it. First the magic:
/// the Second Age rolls the old damage, accuracy and protection levels, later eras roll
/// item properties on the same odds. A magic piece is loot: it keeps no maker's mark.
/// Then every other worn weapon and armor piece that a player trade can make may be
/// exceptional work with its maker's mark, the look of a shard that shopped at its
/// grandmaster crafters; a veteran's smith-made suit may be coloured ore. The purse
/// raises the odds of both magic and ore. Magic armor is a piece the build keeps on: a
/// mage's hardening is never on a plate chest (see <see cref="GearLadder.KeepCeiling"/>).
/// </summary>
public static class KitFinish
{
    /// <summary>The trades whose work a person wears, asked in this order which one makes a piece.</summary>
    private static readonly CraftTrade[] MakerTrades =
    [
        SmithRules.Trade, TailorRules.Trade, FletchRules.Trade, CarpentryRules.Trade, TinkerRules.Trade
    ];

    /// <summary>The one armor piece that can be magic: the chest first, then the shield, then the rest.</summary>
    private static readonly Layer[] MagicArmorLayers =
    [
        Layer.InnerTorso, Layer.TwoHanded, Layer.Pants, Layer.Helm, Layer.Arms, Layer.Gloves, Layer.Neck
    ];

    /// <param name="armorCeiling">The heaviest armor the build keeps on, on the <see cref="GearScore"/> scale.</param>
    public static void Apply(Mobile mobile, SkillTier tier, PersonWealth wealth, string uniqueId, EraBand band, int armorCeiling)
    {
        if (mobile == null)
        {
            return;
        }

        Enchant(mobile, tier, wealth, uniqueId, band, armorCeiling);
        MarkCraftedWork(mobile, tier, KitOreRules.OreFor(tier, wealth, uniqueId), uniqueId);
    }

    /// <summary>The trade whose craft list holds this piece, or null when no player trade makes it.</summary>
    public static CraftTrade MakerTradeOf(Item item)
    {
        if (item == null)
        {
            return null;
        }

        var type = item.GetType();

        for (var i = 0; i < MakerTrades.Length; i++)
        {
            if (MakerTrades[i].System?.CraftItems.SearchFor(type) != null)
            {
                return MakerTrades[i];
            }
        }

        return null;
    }

    private static void Enchant(Mobile mobile, SkillTier tier, PersonWealth wealth, string uniqueId, EraBand band, int armorCeiling)
    {
        var weapon = CombatBrain.HeldWeapon(mobile);
        var armor = MagicArmorPiece(mobile, armorCeiling);

        if (KitMagicRules.UsesItemProperties(band))
        {
            var magic = KitMagicRules.AosMagicFor(tier);

            if (weapon != null && KitMagicRules.HasMagicWeapon(tier, wealth, uniqueId))
            {
                BaseRunicTool.ApplyAttributesTo(weapon, magic.Properties, magic.MinIntensity, magic.MaxIntensity);
            }

            if (armor != null && KitMagicRules.HasMagicArmor(tier, wealth, uniqueId))
            {
                BaseRunicTool.ApplyAttributesTo(armor, magic.Properties, magic.MinIntensity, magic.MaxIntensity);
            }

            return;
        }

        if (weapon != null)
        {
            weapon.DamageLevel = KitMagicRules.WeaponDamage(tier, wealth, uniqueId);
            weapon.AccuracyLevel = KitMagicRules.WeaponAccuracy(tier, wealth, uniqueId);
            weapon.Identified = GearScore.MagicLevel(weapon) > GearScore.NoMagicLevel;
        }

        if (armor != null)
        {
            armor.ProtectionLevel = KitMagicRules.ArmorProtection(tier, wealth, uniqueId);
            armor.Identified = GearScore.MagicLevel(armor) > GearScore.NoMagicLevel;
        }
    }

    private static BaseArmor MagicArmorPiece(Mobile mobile, int armorCeiling)
    {
        for (var i = 0; i < MagicArmorLayers.Length; i++)
        {
            if (mobile.FindItemOnLayer(MagicArmorLayers[i]) is BaseArmor armor && GearEquip.WithinCeiling(armor, armorCeiling))
            {
                return armor;
            }
        }

        return null;
    }

    private static void MarkCraftedWork(Mobile mobile, SkillTier tier, CraftResource ore, string uniqueId)
    {
        for (var i = 0; i < mobile.Items.Count; i++)
        {
            var item = mobile.Items[i];

            if (item is not (BaseWeapon or BaseArmor) || GearScore.MagicLevel(item) > GearScore.NoMagicLevel ||
                !KitCraftRules.IsExceptional(tier, uniqueId, (int)item.Layer) || MakerTradeOf(item) is not { } trade)
            {
                continue;
            }

            var maker = KitCraftRules.MakerName(trade.Kind, uniqueId);

            if (item is BaseWeapon weapon)
            {
                weapon.Quality = WeaponQuality.Exceptional;
                weapon.Crafter = maker;
            }
            else if (item is BaseArmor armor)
            {
                armor.Quality = ArmorQuality.Exceptional;
                armor.Crafter = maker;

                if (trade == SmithRules.Trade && armor.DefaultResource == CraftResource.Iron)
                {
                    armor.Resource = ore;
                }
            }
        }
    }
}
