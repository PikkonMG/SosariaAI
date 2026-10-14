using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A shard's maps by name, size and rules. The test land (<see cref="TestMap.EnsureLand"/>) is
/// a registered map of no stock name with Felucca's rules: a one-map custom shard in small.
/// </summary>
public class FacetMapsTests
{
    private const string Unknown = "Atlantis";
    private const string Custom = "Sosaria";
    private const int CustomSide = 4096;

    [Theory]
    [InlineData(FacetNames.Felucca, FacetMaps.BritanniaWidth, FacetMaps.BritanniaHeight)]
    [InlineData(FacetNames.Felucca, FacetMaps.BritanniaClassicWidth, FacetMaps.BritanniaHeight)]
    [InlineData(FacetNames.Trammel, FacetMaps.BritanniaWidth, FacetMaps.BritanniaHeight)]
    [InlineData(FacetNames.Ilshenar, FacetMaps.IlshenarWidth, FacetMaps.IlshenarHeight)]
    [InlineData(FacetNames.Malas, FacetMaps.MalasWidth, FacetMaps.MalasHeight)]
    [InlineData(FacetNames.Tokuno, FacetMaps.TokunoSide, FacetMaps.TokunoSide)]
    [InlineData(FacetNames.TerMur, FacetMaps.TerMurWidth, FacetMaps.TerMurHeight)]
    [InlineData("felucca", FacetMaps.BritanniaWidth, FacetMaps.BritanniaHeight)]
    public void IsStock_AStockNameAtItsStockSize(string name, int width, int height) =>
        Assert.True(FacetMaps.IsStock(name, width, height));

    [Theory]
    [InlineData(FacetNames.Felucca, CustomSide, CustomSide)]
    [InlineData(FacetNames.Ilshenar, FacetMaps.BritanniaWidth, FacetMaps.BritanniaHeight)]
    [InlineData(Custom, FacetMaps.BritanniaWidth, FacetMaps.BritanniaHeight)]
    [InlineData(null, FacetMaps.BritanniaWidth, FacetMaps.BritanniaHeight)]
    public void IsStock_NotAnotherSizeOrName(string name, int width, int height) =>
        Assert.False(FacetMaps.IsStock(name, width, height));

    [Fact]
    public void IsStock_ByName_ARegisteredCustomMapIsNot()
    {
        var land = TestMap.EnsureLand();

        Assert.False(FacetMaps.IsStock(land.Name));
        Assert.False(FacetMaps.IsStock(Unknown));
        Assert.True(FacetMaps.IsStock(FacetNames.Trammel));
    }

    [Fact]
    public void TryFind_ByNameInAnyCase_NeverInternalNorANumber()
    {
        var land = TestMap.EnsureLand();

        Assert.True(FacetMaps.TryFind(land.Name.ToUpperInvariant(), out var found));
        Assert.Same(land, found);
        Assert.False(FacetMaps.TryFind("Internal", out _));
        Assert.False(FacetMaps.TryFind("0", out _));
        Assert.False(FacetMaps.TryFind(Unknown, out _));
        Assert.False(FacetMaps.TryFind("  ", out _));
        Assert.Null(FacetMaps.Resolve(Unknown));
    }

    [Fact]
    public void TryCanonical_AcceptsAnyRegisteredMap()
    {
        var land = TestMap.EnsureLand();

        Assert.True(FacetNames.TryCanonical(land.Name.ToLowerInvariant(), out var canonical));
        Assert.Equal(land.Name, canonical);
        Assert.True(FacetNames.TryCanonical("trammel", out var stock));
        Assert.Equal(FacetNames.Trammel, stock);
        Assert.False(FacetNames.TryCanonical(Unknown, out _));
        Assert.False(FacetNames.IsStockName(land.Name));
    }

    [Fact]
    public void FacetIds_QualifyAnyRegisteredMap()
    {
        var land = TestMap.EnsureLand();
        var id = FacetIds.Prefix(land.Name, "mira#3");

        Assert.Equal($"{land.Name}{FacetIds.Separator}mira#3", id);
        Assert.Equal(id, FacetIds.Prefix(land.Name, id));
        Assert.Equal(land.Name, FacetIds.FacetOf(id));
        Assert.Equal(FacetNames.Felucca, FacetIds.FacetOf("Felucca:bran"));
        Assert.Null(FacetIds.FacetOf("bran#2"));
        Assert.Null(FacetIds.FacetOf($"{Unknown}:bran"));
    }

    [Fact]
    public void RulesAllowHarm_FeluccasRulesOnly()
    {
        Assert.True(FacetMaps.RulesAllowHarm(MapRules.FeluccaRules));
        Assert.False(FacetMaps.RulesAllowHarm(MapRules.TrammelRules));
        Assert.False(FacetMaps.RulesAllowHarm(MapRules.Internal));
    }

    [Fact]
    public void OpenPvp_ReadsTheMapsRules_AndASettingMayCloseIt()
    {
        // The test land has Felucca's rules under a name of its own: the name never decides.
        var land = TestMap.EnsureLand();
        var closed = new Dictionary<string, MapToggle>(StringComparer.OrdinalIgnoreCase)
        {
            [land.Name] = new MapToggle { Enabled = true, OpenPvp = false }
        };
        var open = new Dictionary<string, MapToggle>(StringComparer.OrdinalIgnoreCase)
        {
            [land.Name] = new MapToggle { Enabled = true, OpenPvp = true }
        };

        Assert.True(FacetMaps.OpenPvp(land, settings: null));
        Assert.True(FacetMaps.OpenPvp(land, open));
        Assert.False(FacetMaps.OpenPvp(land, closed));
        Assert.False(FacetMaps.OpenPvp(Map.Internal, settings: null));
        Assert.False(FacetMaps.OpenPvp((Map)null, settings: null));
    }

    [Fact]
    public void OpenPvp_ByName_StockRulesOrTheMapsOwn()
    {
        // Registered or not, stock Felucca allows harm and stock Trammel does not.
        Assert.True(FacetMaps.OpenPvp(FacetNames.Felucca));
        Assert.False(FacetMaps.OpenPvp(FacetNames.Trammel));
        Assert.False(FacetMaps.OpenPvp(Unknown));
        Assert.True(FacetMaps.OpenPvp(TestMap.EnsureLand().Name));

        // A person bound to no facet yet keeps out of PvP life.
        Assert.False(FacetMaps.OpenPvp((string)null));
        Assert.False(FacetMaps.OpenPvp(" "));
    }

    [Fact]
    public void DefaultFrom_PrefersFelucca_ThenTheFirstEnabledMapTheServerHas()
    {
        var both = Settings((Custom, true), (FacetNames.Felucca, true));
        var customOnly = Settings((Unknown, true), (Custom, true), (FacetNames.Felucca, false));

        Assert.Equal(FacetNames.Felucca, FacetMaps.DefaultFrom(both, [Custom, FacetNames.Felucca]));
        Assert.Equal(Custom, FacetMaps.DefaultFrom(customOnly, [Custom, FacetNames.Felucca]));
        Assert.Equal(Custom, FacetMaps.DefaultFrom(customOnly, [Custom]));
    }

    [Fact]
    public void DefaultFrom_NothingEnabled_IsTheServersOwnMap()
    {
        var off = Settings((Custom, false));

        Assert.Equal(Custom, FacetMaps.DefaultFrom(off, [Custom]));
        Assert.Equal(FacetNames.Felucca, FacetMaps.DefaultFrom(off, [Custom, FacetNames.Felucca]));
        Assert.Equal(Custom, FacetMaps.DefaultFrom(null, [Custom]));
        Assert.Equal(FacetNames.Felucca, FacetMaps.DefaultFrom(null, []));
    }

    [Fact]
    public void EraRules_AllowACustomMapInEveryEra()
    {
        var land = TestMap.EnsureLand();

        Assert.True(Behaviour.EraRules.FacetAllowed(land.Name, Expansion.None));
        Assert.True(Behaviour.EraRules.FacetAllowed(land.Name, Expansion.EJ));
        Assert.False(Behaviour.EraRules.FacetAllowed(Unknown, Expansion.EJ));
    }

    [Fact]
    public void SavedPeople_OfAMapTheServerHasNot_AreKept()
    {
        var land = TestMap.EnsureLand();

        Assert.True(CharacterSpawner.RemovesOffPlan(land.Name));
        Assert.False(CharacterSpawner.RemovesOffPlan(Unknown));
        Assert.Equal(land.Name, CharacterSpawner.SavedFacet(land.Name, Unknown));
        Assert.Equal(Unknown, CharacterSpawner.SavedFacet(" ", Unknown));
        Assert.Equal(FacetMaps.Default, CharacterSpawner.SavedFacet(null, null));
    }

    private static Dictionary<string, MapToggle> Settings(params (string Name, bool Enabled)[] maps)
    {
        var settings = new Dictionary<string, MapToggle>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, enabled) in maps)
        {
            settings[name] = new MapToggle { Enabled = enabled };
        }

        return settings;
    }
}
