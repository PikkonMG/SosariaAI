using System;

namespace SosariaAI.Navigation.Generation;

public static class KindFromGroup
{
    /// <summary>A healer's place that doubles as a rest spot. The role and the token are one word.</summary>
    public const string InnRole = "Inn";

    private const string VendorToken = "vendor";
    private const string WeaponsmithRole = "Weaponsmith";
    private const string ArmorerRole = "Armorer";
    private const string BowyerRole = "Bowyer";
    private const string MapmakerRole = "Mapmaker";
    private const string TailorRole = "Tailor";
    private const string MageRole = "Mage";
    private const string TinkerRole = "Tinker";
    private const string ProvisionerRole = "Provisioner";
    private const string BakerRole = "Baker";
    private const string ButcherRole = "Butcher";
    private const string JewelerRole = "Jeweler";
    private const string TannerRole = "Tanner";
    private const string ShipwrightRole = "Shipwright";
    private const string AlchemistRole = "Alchemist";
    private const string HerbalistRole = "Herbalist";
    private const string ScribeRole = "Scribe";
    private const string BarkeepRole = "Barkeep";
    private const string TavernKeeperRole = "TavernKeeper";
    private const string AnimalTrainerRole = "AnimalTrainer";
    private const string PainterRole = "Painter";
    private const string BeekeeperRole = "Beekeeper";

    private static readonly string[] BankTokens = ["bank"];
    private static readonly string[] HealerTokens = ["healer", InnRole];
    private static readonly string[] ShrineTokens =
    [
        "shrine",
        "ankh",
        "chaos",
        "compassion",
        "honesty",
        "honor",
        "humility",
        "justice",
        "sacrifice",
        "spirituality",
        "valor"
    ];
    private static readonly string[] DungeonTokens =
    [
        "dungeon",
        "despise",
        "deceit",
        "destard",
        "covetous",
        "shame",
        "wrong",
        "hythloth",
        "fire",
        "ice",
        "khaldun",
        "orc"
    ];
    private static readonly string[] ResourceTokens =
    [
        "mine",
        "ore",
        "mountain",
        "lumber",
        "forest",
        "tree",
        "fish",
        "shore"
    ];

    /// <summary>
    /// The vendor role a token means. A "bowyer" location group or a "Blacksmith" spawner
    /// name both land here. Order matters only where tokens overlap: "tavernkeeper"
    /// contains "tavern" and "blacksmith" contains "smith", and both pairs share a role.
    /// "weaponsmith" must be matched before "smith", or a blade shop is treated as a forge.
    /// </summary>
    private static readonly (string Token, string Role)[] VendorRoles =
    [
        (ForgeShop.WeaponsmithToken, WeaponsmithRole),
        (DestinationCatalog.SmithToken, ForgeShop.SmithRole),
        ("armorer", ArmorerRole),
        ("armourer", ArmorerRole),
        ("arms", ArmorerRole),
        (ShopFinder.CarpenterToken, DestinationCatalog.CarpenterRole),
        (DestinationCatalog.FishermanToken, DestinationCatalog.FishermanRole),
        ("bowyer", BowyerRole),
        ("fletcher", BowyerRole),
        ("mapmaker", MapmakerRole),
        ("cartographer", MapmakerRole),
        ("tailor", TailorRole),
        ("mage", MageRole),
        ("tinker", TinkerRole),
        ("provisioner", ProvisionerRole),
        ("baker", BakerRole),
        ("butcher", ButcherRole),
        ("jeweler", JewelerRole),
        ("tanner", TannerRole),
        ("shipwright", ShipwrightRole),
        ("alchemist", AlchemistRole),
        ("reagents", AlchemistRole),
        ("herbalist", HerbalistRole),
        ("scribe", ScribeRole),
        ("barkeep", BarkeepRole),
        ("tavern", TavernKeeperRole),
        ("animaltrainer", AnimalTrainerRole),
        ("stable", AnimalTrainerRole),
        ("painter", PainterRole),
        ("beekeeper", BeekeeperRole)
    ];

    private static readonly string[] TownTokens = ["town"];

    public static DestinationKind From(string group, string name)
    {
        var text = Combined(group, name);

        if (ContainsAny(text, BankTokens))
        {
            return DestinationKind.Bank;
        }

        if (ContainsAny(text, HealerTokens))
        {
            return DestinationKind.Healer;
        }

        if (ContainsAny(text, ShrineTokens))
        {
            return DestinationKind.Shrine;
        }

        if (ContainsAny(text, DungeonTokens))
        {
            return DestinationKind.Dungeon;
        }

        if (ContainsToken(text, VendorToken) || VendorRoleOf(text) != null)
        {
            return DestinationKind.Vendor;
        }

        if (ContainsAny(text, ResourceTokens))
        {
            return DestinationKind.Resource;
        }

        if (ContainsAny(text, TownTokens))
        {
            return DestinationKind.Vendor;
        }

        return DestinationKind.Hunt;
    }

    private static string Combined(string group, string name)
    {
        var hasGroup = !string.IsNullOrWhiteSpace(group);
        var hasName = !string.IsNullOrWhiteSpace(name);

        if (hasGroup && hasName)
        {
            return group + " " + name;
        }

        if (hasGroup)
        {
            return group;
        }

        return hasName ? name : string.Empty;
    }

    /// <summary>The shop role a token in <paramref name="text"/> names, or null.</summary>
    public static string VendorRoleOf(string text)
    {
        for (var i = 0; i < VendorRoles.Length; i++)
        {
            if (ContainsToken(text, VendorRoles[i].Token))
            {
                return VendorRoles[i].Role;
            }
        }

        return null;
    }

    private static bool ContainsAny(string text, string[] tokens)
    {
        for (var i = 0; i < tokens.Length; i++)
        {
            if (ContainsToken(text, tokens[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsToken(string text, string token) =>
        !string.IsNullOrWhiteSpace(text) && text.Contains(token, StringComparison.OrdinalIgnoreCase);
}
