using System;
using System.Collections.Generic;
using SosariaAI.Combat;
using SosariaAI.Navigation;

namespace SosariaAI.Economy;

public enum GearBuyKind
{
    Weapon,
    Shield,
    Armor,
    Robe,

    /// <summary>A piece for the spare kit a red keeps in its bank box: carried to the bank, never worn.</summary>
    Spare,

    /// <summary>A GM piece out of a crafter's shop stock, bought at its station.</summary>
    Crafted,

    /// <summary>An order for a GM piece, placed at a crafter's station with half paid down.</summary>
    Order,

    /// <summary>The pickup of a ready order: the rest paid, the work taken.</summary>
    Pickup
}

/// <summary>
/// One buy: the piece, a spare type the shop may stock instead, the shelf price the purse
/// must cover, the rank it brings, where it is sold, what it replaces, and whether it
/// betters a worn piece (<paramref name="Upgrade"/>) or fills an empty place.
/// <paramref name="CrafterSerial"/> is the crafter a crafted buy, an order or a pickup goes to;
/// 0 for a shop buy.
/// </summary>
public readonly record struct GearOffer(
    string ItemTypeName,
    string AlternateTypeName,
    int Price,
    int GearScore,
    string VendorDestination,
    string FallbackDestination,
    GearBuyKind Kind,
    GearSlot? Slot,
    bool Upgrade,
    uint CrafterSerial = 0
);

/// <summary>
/// What a person wears against what its class and purse call for. Built from the world by
/// the shopping skill; the plan itself reads nothing else.
/// </summary>
public sealed class GearNeeds
{
    /// <summary>How much armor the class wears.</summary>
    public KitArmor Weight { get; init; }

    /// <summary>The best rung the person shops for (see <see cref="GearLadder.Target"/>).</summary>
    public GearMaterial Target { get; init; }

    public bool Female { get; init; }

    /// <summary>The kit weapon to buy when the person has none, or null when its class carries none.</summary>
    public string Weapon { get; init; }

    /// <summary>The weapon row's first piece, bought when the shop lacks the kit weapon.</summary>
    public string WeaponRow { get; init; }

    public bool Armed { get; init; }

    /// <summary>The tier's shield, or null when the build fights without one.</summary>
    public string Shield { get; init; }

    public bool ShieldWorn { get; init; }

    /// <summary>True when the person's look is a robe (a mage's cloth).</summary>
    public bool WantsRobe { get; init; }

    public bool RobeWorn { get; init; }

    /// <summary>The rank of the armor worn on each slot; a missing slot ranks zero.</summary>
    public IReadOnlyDictionary<GearSlot, int> WornRanks { get; init; } = new Dictionary<GearSlot, int>();

    /// <summary>False while the last upgrade is recent: a lost piece is still replaced.</summary>
    public bool MayUpgrade { get; init; } = true;
}

/// <summary>
/// Gear buys, one piece a trip, above a gold reserve. A person first replaces
/// what it lost (a weapon, its body armor, a shield, its helm and limbs, a robe) and then
/// climbs the armor ladder a piece at a time, no more often than
/// <see cref="UpgradeInterval"/>, up to the rung its class, tier and purse allow. A worn piece
/// competes with its make and magic (see <see cref="GearScore.RankOf"/>), so crafted or
/// magic kit is never swapped for a plain shelf piece. The plan names the piece and the shop;
/// the shopping skill first asks a crafter at the bank for that piece (<see cref="Fits"/>).
/// Characters never buy from a person at a keyboard on their own errands (<see cref="TradeRules"/>).
/// </summary>
public static class GearPlan
{
    /// <summary>The least time between two upgrades of one person: a shopping day, not a spree.</summary>
    public static readonly TimeSpan UpgradeInterval = TimeSpan.FromMinutes(30);

    /// <summary>A shop weapon of any kit row costs no more than this on a smith's shelf.</summary>
    public const int WeaponBudget = 60;

    /// <summary>No gold held back: the reserve an unarmed person gives up for its weapon.</summary>
    public const int NoReserve = 0;

    public const int RobePrice = 18;
    public const string RobeType = "Robe";
    public const string DeathRobeType = "DeathRobe";

    public const int NoGearScore = 0;
    public const int RobeGearScore = 4;
    public const int KatanaGearScore = 12;
    public const int BroadswordGearScore = 16;
    public const int LongswordGearScore = 14;
    public const int VikingSwordGearScore = 12;
    public const int ScimitarGearScore = 12;
    public const int CutlassGearScore = 10;
    public const int AxeGearScore = 10;
    public const int BowGearScore = 12;
    public const int CrossbowGearScore = 14;
    public const int HeavyCrossbowGearScore = 16;
    public const int ClubGearScore = 6;
    public const int ShepherdsCrookGearScore = 4;
    public const int QuarterStaffGearScore = 8;
    public const int GnarledStaffGearScore = 8;
    public const int MaceGearScore = 10;
    public const int BlackStaffGearScore = 10;
    public const int ToolWeaponGearScore = 4;

    /// <summary>Gear the tables do not know — looted or exotic — counts as plate so a buy never
    /// "upgrades" into something worse.</summary>
    public const int UnlistedGearScore = GearLadder.PlateScore;

    // A person refits its hands, its body, its shield, its head and limbs, then its robe.
    private static readonly GearSlot[] BodyFirst = [GearSlot.Chest, GearSlot.Legs];

    private static readonly GearSlot[] LimbsAfter = [GearSlot.Helm, GearSlot.Arms, GearSlot.Gloves, GearSlot.Neck];

    // Weapons and cloth on the shop scale; armor ranks by its rung (see GearLadder).
    private static readonly Dictionary<string, int> ShopScores = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Katana"] = KatanaGearScore,
        ["Broadsword"] = BroadswordGearScore,
        ["Longsword"] = LongswordGearScore,
        ["VikingSword"] = VikingSwordGearScore,
        ["Scimitar"] = ScimitarGearScore,
        ["Cutlass"] = CutlassGearScore,
        ["Axe"] = AxeGearScore,
        ["Bow"] = BowGearScore,
        ["Crossbow"] = CrossbowGearScore,
        ["HeavyCrossbow"] = HeavyCrossbowGearScore,
        ["Club"] = ClubGearScore,
        ["ShepherdsCrook"] = ShepherdsCrookGearScore,
        ["QuarterStaff"] = QuarterStaffGearScore,
        ["GnarledStaff"] = GnarledStaffGearScore,
        ["Mace"] = MaceGearScore,
        ["BlackStaff"] = BlackStaffGearScore,
        ["Hatchet"] = ToolWeaponGearScore,
        ["Pickaxe"] = ToolWeaponGearScore,
        ["ButcherKnife"] = ToolWeaponGearScore,
        ["SkinningKnife"] = ToolWeaponGearScore,
        ["Cleaver"] = ToolWeaponGearScore,
        [RobeType] = RobeGearScore,
        [DeathRobeType] = NoGearScore
    };

    // Shelf prices from the smith's buy list.
    private static readonly Dictionary<string, int> ShieldPrices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Buckler"] = 50,
        ["WoodenShield"] = 30,
        ["MetalShield"] = 121,
        ["BronzeShield"] = 66,
        ["WoodenKiteShield"] = 70,
        ["HeaterShield"] = 231,
        ["MetalKiteShield"] = 123
    };

    /// <summary>True for a shield a smith keeps on its shelf.</summary>
    public static bool IsShelfShield(string typeName) =>
        !string.IsNullOrWhiteSpace(typeName) && ShieldPrices.ContainsKey(typeName);

    /// <summary>
    /// True when a piece of <paramref name="itemTypeName"/> answers the buy: the piece planned or
    /// its spare type. A crafter's own make of that piece at the bank answers it as the shelf does.
    /// </summary>
    public static bool Fits(GearOffer offer, string itemTypeName) =>
        !string.IsNullOrWhiteSpace(itemTypeName) &&
        (string.Equals(offer.ItemTypeName, itemTypeName, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(offer.AlternateTypeName, itemTypeName, StringComparison.OrdinalIgnoreCase));

    public static bool Affordable(int gold, int price, int reserve) =>
        price > 0 && gold - price >= reserve;

    /// <summary>
    /// The part of the gold reserve the pack must still keep: the reserve is kept in the bank
    /// (<see cref="Configuration.SosariaSettings.GearGoldReserve"/>), so bank gold covers it
    /// first. Held in the pack whole, it left a fighter carrying its purse no piece to buy after a
    /// death: 86 rebuys of two runs ended with "wants no piece it can pay for".
    /// </summary>
    public static int PackReserve(int bankGold, int reserve) => Math.Max(0, reserve - Math.Max(0, bankGold));

    /// <summary>
    /// Rank of a worn piece on the shop scale. A bare slot scores zero; gear the tables do
    /// not know counts as <see cref="UnlistedGearScore"/>.
    /// </summary>
    public static int ScoreOf(string itemTypeName)
    {
        if (string.IsNullOrWhiteSpace(itemTypeName))
        {
            return NoGearScore;
        }

        if (ShopScores.TryGetValue(itemTypeName, out var score) || GearLadder.TryScoreOf(itemTypeName, out score))
        {
            return score;
        }

        return UnlistedGearScore;
    }

    /// <summary>The next piece this person buys, or null when it wants nothing it can afford.</summary>
    public static GearOffer? NextBuy(GearNeeds needs, int gold, int reserve)
    {
        if (needs == null)
        {
            return null;
        }

        // An unarmed person spends its last coin on the weapon: the reserve kept a blue raised with
        // a few hundred gold empty-handed, and 147 rebuys of one run wanted "no piece it can pay for".
        if (!needs.Armed && needs.Weapon != null && Affordable(gold, WeaponBudget, NoReserve))
        {
            return WeaponOffer(needs);
        }

        var replacement = ArmorOffer(needs, BodyFirst, gold, reserve, upgrade: false) ??
                          ShieldOffer(needs, gold, reserve) ??
                          ArmorOffer(needs, LimbsAfter, gold, reserve, upgrade: false) ??
                          RobeOffer(needs, gold, reserve);

        if (replacement != null || !needs.MayUpgrade)
        {
            return replacement;
        }

        return ArmorOffer(needs, BodyFirst, gold, reserve, upgrade: true) ??
               ArmorOffer(needs, LimbsAfter, gold, reserve, upgrade: true);
    }

    /// <summary>
    /// A spare of <paramref name="piece"/> for the kit in a red's bank box, at its shelf price
    /// and shop, when the purse covers it above the reserve: the kit weapon
    /// (<paramref name="kitWeapon"/>, or its row's first piece), a shield, or a shelf piece of
    /// armor. Null for anything else: a helm or bone no shop sells, a book, a stack.
    /// </summary>
    public static GearOffer? SpareOffer(string piece, string kitWeapon, string weaponRow, int gold, int reserve)
    {
        if (string.IsNullOrWhiteSpace(piece))
        {
            return null;
        }

        if (string.Equals(piece, kitWeapon, StringComparison.OrdinalIgnoreCase))
        {
            return Affordable(gold, WeaponBudget, reserve)
                ? new GearOffer(piece, weaponRow ?? piece, WeaponBudget, ScoreOf(piece), ShopFinder.SmithToken, ShopFinder.WeaponsmithToken, GearBuyKind.Spare, null, false)
                : null;
        }

        if (ShieldPrices.TryGetValue(piece, out var shieldPrice))
        {
            return Affordable(gold, shieldPrice, reserve)
                ? new GearOffer(piece, piece, shieldPrice, GearScore.ShieldScore, ShopFinder.SmithToken, ShopFinder.ArmorerToken, GearBuyKind.Spare, null, false)
                : null;
        }

        for (var i = 0; i < GearLadder.Pieces.Count; i++)
        {
            var shelf = GearLadder.Pieces[i];

            if (string.Equals(shelf.TypeName, piece, StringComparison.OrdinalIgnoreCase))
            {
                return Affordable(gold, shelf.Price, reserve)
                    ? new GearOffer(piece, piece, shelf.Price, GearLadder.ScoreOf(shelf.Material), shelf.Vendor, shelf.FallbackVendor, GearBuyKind.Spare, shelf.Slot, false)
                    : null;
            }
        }

        return null;
    }

    // A smith keeps every blade, axe, mace, staff and bow; a weaponsmith is the second stop.
    private static GearOffer WeaponOffer(GearNeeds needs) =>
        new(
            needs.Weapon,
            needs.WeaponRow ?? needs.Weapon,
            WeaponBudget,
            ScoreOf(needs.Weapon),
            ShopFinder.SmithToken,
            ShopFinder.WeaponsmithToken,
            GearBuyKind.Weapon,
            null,
            false
        );

    private static GearOffer? ShieldOffer(GearNeeds needs, int gold, int reserve)
    {
        if (needs.Shield == null || needs.ShieldWorn || !ShieldPrices.TryGetValue(needs.Shield, out var price) ||
            !Affordable(gold, price, reserve))
        {
            return null;
        }

        return new GearOffer(
            needs.Shield,
            needs.Shield,
            price,
            GearScore.ShieldScore,
            ShopFinder.SmithToken,
            ShopFinder.ArmorerToken,
            GearBuyKind.Shield,
            null,
            false
        );
    }

    private static GearOffer? RobeOffer(GearNeeds needs, int gold, int reserve)
    {
        if (!needs.WantsRobe || needs.RobeWorn || !Affordable(gold, RobePrice, reserve))
        {
            return null;
        }

        return new GearOffer(RobeType, RobeType, RobePrice, RobeGearScore, ShopFinder.TailorToken, ShopFinder.TailorToken, GearBuyKind.Robe, null, false);
    }

    /// <summary>
    /// The first slot, in order, where a rung between the person's target and leather beats
    /// what it wears and fits the purse: the best such rung, so a thin purse still buys ring
    /// where plate is out of reach. An <paramref name="upgrade"/> looks only at worn slots,
    /// a replacement only at empty ones.
    /// </summary>
    private static GearOffer? ArmorOffer(GearNeeds needs, GearSlot[] order, int gold, int reserve, bool upgrade)
    {
        var slots = GearLadder.Slots(needs.Weight);

        for (var i = 0; i < order.Length; i++)
        {
            var slot = order[i];

            if (!Contains(slots, slot))
            {
                continue;
            }

            var worn = needs.WornRanks.TryGetValue(slot, out var rank) ? rank : NoGearScore;

            if (worn > NoGearScore != upgrade)
            {
                continue;
            }

            for (var rung = needs.Target; rung > GearMaterial.None; rung--)
            {
                var score = GearLadder.ScoreOf(rung);

                if (score <= worn)
                {
                    break;
                }

                if (GearLadder.Piece(slot, rung, needs.Female) is { } piece && Affordable(gold, piece.Price, reserve))
                {
                    return new GearOffer(
                        piece.TypeName,
                        piece.TypeName,
                        piece.Price,
                        score,
                        piece.Vendor,
                        piece.FallbackVendor,
                        GearBuyKind.Armor,
                        slot,
                        upgrade
                    );
                }
            }
        }

        return null;
    }

    private static bool Contains(IReadOnlyList<GearSlot> slots, GearSlot slot)
    {
        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i] == slot)
            {
                return true;
            }
        }

        return false;
    }
}
