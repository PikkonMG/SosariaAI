using System.Linq;
using Server;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class DestinationCatalogTests
{
    [Fact]
    public void Resolve_ExactName_ReturnsDestination()
    {
        var catalog = new DestinationCatalog([Bank("Britain West Bank", 10, 0)]);
        var dest = catalog.Resolve("Britain West Bank", new Point3D(0, 0, 0));

        Assert.NotNull(dest);
        Assert.Equal("Britain West Bank", dest.Name);
        Assert.Same(dest, catalog.GetByName("Britain West Bank"));
    }

    [Fact]
    public void Resolve_Alias_ReturnsDestination()
    {
        var catalog = new DestinationCatalog(
            [
                new Destination
                {
                    Name = "Britain West Bank",
                    Kind = "Bank",
                    X = 10,
                    Y = 0,
                    Z = 0,
                    Aliases = ["west-bank"]
                }
            ]
        );

        var dest = catalog.Resolve("west-bank", new Point3D(0, 0, 0));
        Assert.NotNull(dest);
        Assert.Equal("Britain West Bank", dest.Name);
    }

    [Fact]
    public void Resolve_Bank_PicksNearestBank()
    {
        var catalog = new DestinationCatalog(
            [
                Bank("Near Bank", 10, 0),
                Bank("Far Bank", 200, 0)
            ]
        );

        var dest = catalog.Resolve("bank", new Point3D(12, 0, 0));
        Assert.NotNull(dest);
        Assert.Equal("Near Bank", dest.Name);
        Assert.Equal(DestinationKind.Bank, dest.ParsedKind);
    }

    [Fact]
    public void Resolve_Blacksmith_SetsVendorAndSmith()
    {
        var catalog = new DestinationCatalog(
            [
                new Destination
                {
                    Name = "Town Tailor",
                    Kind = "Vendor",
                    Role = "Tailor",
                    X = 0,
                    Y = 0,
                    Z = 0
                },
                new Destination
                {
                    Name = "Town Forge",
                    Kind = "Vendor",
                    Role = "Smith",
                    X = 40,
                    Y = 0,
                    Z = 0
                }
            ]
        );

        var dest = catalog.Resolve("blacksmith", new Point3D(0, 0, 0));
        Assert.NotNull(dest);
        Assert.Equal("Town Forge", dest.Name);
        Assert.Equal(DestinationKind.Vendor, dest.ParsedKind);
        Assert.Equal("Smith", dest.Role);
    }

    [Fact]
    public void Resolve_UnknownToken_ReturnsNull()
    {
        var catalog = new DestinationCatalog([Bank("Britain West Bank", 10, 0)]);

        Assert.Null(catalog.Resolve("no-such-place", new Point3D(0, 0, 0)));
    }

    [Fact]
    public void Resolve_KindTokens_FindBankGraveyardDespise()
    {
        var catalog = new DestinationCatalog(
            [
                Bank("Britain West Bank", 10, 0),
                new Destination { Name = "Britain Graveyard", Kind = "Hunt", Role = "Graveyard", X = 20, Y = 0 },
                new Destination { Name = "Despise Entrance", Kind = "Dungeon", Role = "Despise", X = 30, Y = 0 }
            ]
        );
        var from = CharactersFile.DefaultBankSpot;

        var bank = catalog.Resolve("bank", from);
        Assert.NotNull(bank);
        Assert.Equal(DestinationKind.Bank, bank.ParsedKind);

        var graveyard = catalog.Resolve("graveyard", from);
        Assert.NotNull(graveyard);
        Assert.Equal(DestinationKind.Hunt, graveyard.ParsedKind);
        Assert.Equal("Graveyard", graveyard.Role);

        var despise = catalog.Resolve("despise", from);
        Assert.NotNull(despise);
        Assert.Equal(DestinationKind.Dungeon, despise.ParsedKind);
        Assert.Equal("Despise", despise.Role);
    }

    [Fact]
    public void TryParseKind_Bank_IsBank()
    {
        Assert.True(DestinationCatalog.TryParseKind("bank", out var kind, out var role));
        Assert.Equal(DestinationKind.Bank, kind);
        Assert.Null(role);
    }

    [Fact]
    public void TryParseKind_SmithTokens_AreVendorSmith()
    {
        Assert.True(DestinationCatalog.TryParseKind("blacksmith", out var kind, out var role));
        Assert.Equal(DestinationKind.Vendor, kind);
        Assert.Equal("Smith", role);

        Assert.True(DestinationCatalog.TryParseKind("smith", out kind, out role));
        Assert.Equal(DestinationKind.Vendor, kind);
        Assert.Equal("Smith", role);

        Assert.True(DestinationCatalog.TryParseKind("forge", out kind, out role));
        Assert.Equal(DestinationKind.Vendor, kind);
        Assert.Equal("Smith", role);
    }

    [Fact]
    public void TryParseKind_Graveyard_IsHuntGraveyard()
    {
        Assert.True(DestinationCatalog.TryParseKind("graveyard", out var kind, out var role));
        Assert.Equal(DestinationKind.Hunt, kind);
        Assert.Equal("Graveyard", role);
    }

    [Fact]
    public void TryParseKind_Despise_IsDungeonDespise()
    {
        Assert.True(DestinationCatalog.TryParseKind("despise", out var kind, out var role));
        Assert.Equal(DestinationKind.Dungeon, kind);
        Assert.Equal("Despise", role);
    }

    [Fact]
    public void Resolve_Blacksmith_SkipsAWeaponsmithWithNoForge()
    {
        // Cove's weaponsmith was tagged Smith because the name contains "smith".
        // Smiths walked there, found no forge, and failed the step every try.
        var cove = new Point3D(2216, 1192, 0);
        var catalog = new DestinationCatalog(
            [
                Smith("Weaponsmith 2216-1167", 2216, 1167),
                Smith("Britain Blacksmith", 1507, 1579),
                new Destination
                {
                    Name = "Armorer 2216-1167",
                    Kind = "Vendor",
                    Role = "Armorer",
                    X = 2216,
                    Y = 1167,
                    Z = 0
                }
            ]
        );

        var dest = catalog.Resolve("blacksmith", cove);

        Assert.NotNull(dest);
        Assert.Equal("Britain Blacksmith", dest.Name);
        Assert.DoesNotContain(
            catalog.NearestFirst("blacksmith", cove, limit: 4),
            d => d.Name.Contains("Weaponsmith", System.StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void NearestFirst_Blacksmith_ListsEverySmithByDistance()
    {
        // From the Britain bank the nearest smith stands where no route reaches. The
        // seller tries the next one, so it needs them all, nearest first.
        var bank = new Point3D(1425, 1695, 0);
        var catalog = new DestinationCatalog(
            [
                Smith("Hill Smith", 1418, 1547),
                Smith("Britain Blacksmith", 1507, 1579),
                Smith("Guildmaster", 1349, 1778),
                Bank("Britain West Bank", 1425, 1690)
            ]
        );

        var found = catalog.NearestFirst("blacksmith", bank, limit: 3);

        Assert.Equal(["Guildmaster", "Britain Blacksmith", "Hill Smith"], found.Select(d => d.Name));
        Assert.Single(catalog.NearestFirst("blacksmith", bank, limit: 1));
    }

    [Fact]
    public void Resolve_SharedAlias_PicksTheNearest()
    {
        // Every inn answers to "tavern". The first one in the list was in Papua, so every
        // Britain character who wanted a drink walked off toward the far side of the world.
        var britain = new Point3D(1425, 1695, 0);
        var catalog = new DestinationCatalog(
            [
                Inn("Papua Inn", 5769, 3176),
                Inn("Britain Tavern", 1427, 1716),
                Inn("Britain Inn", 1493, 1616)
            ]
        );

        Assert.Equal("Britain Tavern", catalog.Resolve("tavern", britain).Name);
        Assert.Equal("Papua Inn", catalog.Resolve("tavern", new Point3D(5760, 3170, 0)).Name);
        Assert.Equal(
            ["Britain Tavern", "Britain Inn"],
            catalog.NearestFirst("tavern", britain, limit: 2).Select(d => d.Name)
        );
    }

    private static Destination Inn(string name, int x, int y) =>
        new()
        {
            Name = name,
            Kind = "Healer",
            Role = "InnKeeper",
            X = x,
            Y = y,
            Z = 0,
            Aliases = ["tavern", "pub"]
        };

    [Fact]
    public void NearestFirst_Name_ReturnsThatOneOrNothing()
    {
        var catalog = new DestinationCatalog([Bank("Britain West Bank", 10, 0)]);

        Assert.Single(catalog.NearestFirst("Britain West Bank", Point3D.Zero, limit: 3));
        Assert.Empty(catalog.NearestFirst("Nowhere", Point3D.Zero, limit: 3));
    }

    [Fact]
    public void ApproachPoint_NodeOnRaisedFloor_UsesNodeZ()
    {
        // The Unicorn's Horn keeper seeds z = 0 but stands on the z = 10 common
        // room floor. A crowd stalled on that floor, told to reach z = 0 beneath
        // it. The bound node names the floor the approach uses.
        var inn = new Destination
        {
            Name = "The Unicorn's Horn",
            X = 1547,
            Y = 1768,
            Z = 0,
            Node = "floor"
        };
        var graph = new NavGraph(
            FacetNames.Felucca,
            [new NavNode { Name = "floor", X = 1548, Y = 1777, Z = 10 }]
        );

        Assert.Equal(new Point3D(1547, 1768, 10), inn.ApproachPoint(graph));
    }

    [Fact]
    public void ApproachPoint_RoofMarker_UsesNodeZ()
    {
        // Shop signs seed z = 20 on the roof while the bound node sits on the
        // street — the same fix that made the blacksmith walkable.
        var sign = new Destination
        {
            Name = "Smith sign",
            X = 1427,
            Y = 1716,
            Z = 20,
            Node = "street"
        };
        var graph = new NavGraph(
            FacetNames.Felucca,
            [new NavNode { Name = "street", X = 1428, Y = 1720, Z = 0 }]
        );

        Assert.Equal(new Point3D(1427, 1716, 0), sign.ApproachPoint(graph));
    }

    [Fact]
    public void ApproachPoint_NoNodeOrGraph_KeepsSeedPoint()
    {
        var inn = new Destination { X = 1427, Y = 1716, Z = 20 };
        var graph = new NavGraph(
            FacetNames.Felucca,
            [new NavNode { Name = "other", X = 0, Y = 0, Z = 5 }]
        );

        Assert.Equal(new Point3D(1427, 1716, 20), inn.ApproachPoint(graph));
        Assert.Equal(new Point3D(1427, 1716, 20), inn.ApproachPoint(null));
    }

    private static Destination Smith(string name, int x, int y) =>
        new()
        {
            Name = name,
            Kind = "Vendor",
            Role = "Smith",
            X = x,
            Y = y,
            Z = 0
        };

    [Fact]
    public void Resolve_AvoidRedTown_PassesOverTheDenWhileAnotherStands()
    {
        var den = PkRules.BucsDenHaven;
        var catalog = new DestinationCatalog([Bank("Den Bank", den.X, den.Y), Bank("Far Bank", den.X + RedTownSpan, den.Y)]);

        Assert.Equal("Den Bank", catalog.Resolve("bank", den).Name);
        Assert.Equal("Den Bank", catalog.Resolve("bank", den, avoidRedTown: false).Name);
        Assert.Equal("Far Bank", catalog.Resolve("bank", den, avoidRedTown: true).Name);
    }

    [Fact]
    public void Resolve_AvoidRedTown_TakesTheDenWhenNothingElseStands()
    {
        var den = PkRules.BucsDenHaven;
        var catalog = new DestinationCatalog([Bank("Den Bank", den.X, den.Y)]);

        Assert.Equal("Den Bank", catalog.Resolve("bank", den, avoidRedTown: true).Name);
    }

    [Fact]
    public void NearestBank_AMurdererTakesTheDenTeller_ABlueTheNearest()
    {
        // The Den's own "A Place Fer Yer Stuff" teller, and the Britain bank a red cannot use.
        var britain = CharactersFile.DefaultBankSpot;
        var catalog = new DestinationCatalog([Bank("Britain Bank", britain.X, britain.Y), Bank("Den Bank", DenTeller.X, DenTeller.Y)]);

        Assert.Equal("Britain Bank", catalog.NearestBank(britain, murderer: false).Name);
        Assert.Equal("Den Bank", catalog.NearestBank(britain, murderer: true).Name);
    }

    [Fact]
    public void NearestBank_NoDenTeller_NoneForAMurderer() =>
        Assert.Null(
            new DestinationCatalog([Bank("Britain Bank", CharactersFile.DefaultBankSpot.X, CharactersFile.DefaultBankSpot.Y)])
                .NearestBank(Point3D.Zero, murderer: true)
        );

    /// <summary>The Buccaneer's Den teller in the live catalog.</summary>
    private static readonly Point3D DenTeller = new(2731, 2192, 0);

    /// <summary>Wider than the Den, so a bank this far off stands outside it.</summary>
    private const int RedTownSpan = PkRules.BucsDenMaxX - PkRules.BucsDenMinX + 1;

    private static Destination Bank(string name, int x, int y) =>
        new()
        {
            Name = name,
            Kind = "Bank",
            X = x,
            Y = y,
            Z = 0
        };
}
