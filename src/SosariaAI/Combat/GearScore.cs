using System;
using Server;
using Server.Items;
using SosariaAI.Economy;

namespace SosariaAI.Combat;

/// <summary>
/// How strong a character's worn gear is. Each weapon and torso piece is ranked on the
/// shop scale; exceptional work and magic add to the rank, so a grandmaster's katana of
/// force outranks a plain broadsword off the shelf and is never traded down for one.
/// </summary>
public static class GearScore
{
    public const int UnarmedScore = 0;
    public const int WeaponScore = 12;
    public const int ArmorPieceScore = 6;
    public const int ShieldScore = 5;

    /// <summary>Exceptional work: pre-AOS it gave a weapon extra damage and armor twenty percent more rating.</summary>
    public const int ExceptionalBonus = 4;

    /// <summary>Each level of the old magic scale, from ruin or defense up to vanquishing or invulnerability.</summary>
    public const int MagicLevelBonus = 4;

    /// <summary>An item-property piece counts as the middle of the old magic scale.</summary>
    public const int AosMagicLevel = 3;

    public const int NoMagicLevel = 0;

    public static int Of(Mobile mobile)
    {
        if (mobile == null)
        {
            return UnarmedScore;
        }

        var score = WeaponRank(mobile);

        if (ShieldOn(mobile) is { } shield)
        {
            score += ShieldScore + Bonus(shield);
        }

        score += TorsoRank(mobile);
        score += Piece(mobile, Layer.Pants);
        score += Piece(mobile, Layer.Helm);
        score += Piece(mobile, Layer.Gloves);
        score += Piece(mobile, Layer.Neck);
        score += Piece(mobile, Layer.Arms);
        return score;
    }

    /// <summary>
    /// The rank of one piece: its type on the shop scale plus its make and magic. A
    /// spellbook has no shop rank; it counts as a plain weapon. An empty slot ranks zero.
    /// </summary>
    public static int RankOf(Item item) =>
        item switch
        {
            null => UnarmedScore,
            Spellbook => WeaponScore,
            _ => GearPlan.ScoreOf(item.GetType().Name) + Bonus(item)
        };

    /// <summary>The rank of the wielded weapon, the piece a weapon offer would replace.</summary>
    public static int WeaponRank(Mobile mobile) => RankOf(WeaponOn(mobile));

    /// <summary>The rank of the torso piece an armor offer would replace.</summary>
    public static int TorsoRank(Mobile mobile) => RankOf(TorsoOn(mobile));

    /// <summary>The rank of the armor worn on a slot, the piece an armor offer would replace. Cloth ranks zero.</summary>
    public static int SlotRank(Mobile mobile, GearSlot slot) =>
        mobile?.FindItemOnLayer(LayerOf(slot)) is BaseArmor armor ? RankOf(armor) : UnarmedScore;

    /// <summary>The wear layer of an armor slot.</summary>
    public static Layer LayerOf(GearSlot slot) =>
        slot switch
        {
            GearSlot.Chest => Layer.InnerTorso,
            GearSlot.Legs => Layer.Pants,
            GearSlot.Arms => Layer.Arms,
            GearSlot.Gloves => Layer.Gloves,
            GearSlot.Neck => Layer.Neck,
            _ => Layer.Helm
        };

    /// <summary>What make and magic add to a piece's rank.</summary>
    public static int Bonus(Item item) =>
        (Appraisal.IsExceptional(item) ? ExceptionalBonus : 0) + MagicLevel(item) * MagicLevelBonus;

    /// <summary>
    /// The piece's place on the old magic scale, 0 for plain. A weapon counts its better
    /// of damage and accuracy; armor its protection; an item-property piece counts as
    /// <see cref="AosMagicLevel"/>.
    /// </summary>
    public static int MagicLevel(Item item) =>
        item switch
        {
            BaseWeapon weapon => Math.Max(
                Math.Max((int)weapon.DamageLevel, (int)weapon.AccuracyLevel),
                PropertyLevel(weapon.Attributes, weapon.WeaponAttributes)
            ),
            BaseArmor armor => Math.Max((int)armor.ProtectionLevel, PropertyLevel(armor.Attributes, armor.ArmorAttributes)),
            _ => NoMagicLevel
        };

    // A piece made from a save's serial constructor may hold no attribute sets yet.
    private static int PropertyLevel(BaseAttributes attributes, BaseAttributes kindAttributes) =>
        attributes?.IsEmpty == false || kindAttributes?.IsEmpty == false ? AosMagicLevel : NoMagicLevel;

    public static bool HasWeapon(Mobile mobile) => WeaponOn(mobile) != null;

    private static int Piece(Mobile mobile, Layer layer)
    {
        var item = mobile.FindItemOnLayer(layer);

        if (item is BaseArmor)
        {
            return ArmorPieceScore + Bonus(item);
        }

        if (item is BaseClothing && layer == Layer.Helm)
        {
            return ArmorPieceScore;
        }

        return UnarmedScore;
    }

    private static BaseShield ShieldOn(Mobile mobile) =>
        mobile.FindItemOnLayer(Layer.TwoHanded) as BaseShield ?? mobile.FindItemOnLayer(Layer.OneHanded) as BaseShield;

    /// <summary>
    /// Worn chest armor first, then the outer layer when it is armor or a robe. Plain
    /// clothing — a shirt, a cloak — counts as nothing so it never reads as gear.
    /// </summary>
    private static Item TorsoOn(Mobile mobile)
    {
        var inner = mobile?.FindItemOnLayer(Layer.InnerTorso);

        if (inner is BaseArmor)
        {
            return inner;
        }

        var outer = mobile?.FindItemOnLayer(Layer.OuterTorso);
        return outer is BaseArmor or Robe ? outer : null;
    }

    private static Item WeaponOn(Mobile mobile)
    {
        var oneHanded = mobile?.FindItemOnLayer(Layer.OneHanded);
        var twoHanded = mobile?.FindItemOnLayer(Layer.TwoHanded);

        if (oneHanded is BaseWeapon or Spellbook)
        {
            return oneHanded;
        }

        if (twoHanded is BaseWeapon or Spellbook)
        {
            return twoHanded;
        }

        return null;
    }
}
