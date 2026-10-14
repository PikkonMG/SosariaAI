using Server;
using SosariaAI.Economy;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// Forges that are part of the Felucca map art, not placed items. This process loads no
/// world items, so any forge found here is map art.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapForgeTests
{
    /// <summary>The Magincia smithy's forge, drawn into the map, and where Mirabel stood beside it.</summary>
    private static readonly Point3D MaginciaForge = new(3704, 2245, 20);

    private static readonly Point3D MaginciaSmithy = new(3706, 2243, 20);

    /// <summary>The Britain bank: no forge in the map art within a forge search of it.</summary>
    private static readonly Point3D BritainBank = new(1434, 1690, 0);

    [RealMapFact]
    public void MaginciaSmithy_HasAMapArtForgeInReach()
    {
        var map = RealMapWorld.Felucca;

        Assert.Contains(MaginciaForge, StaticForges.Near(map, MaginciaSmithy, SmeltRules.ForgeSearchRange));
        Assert.Equal(MaginciaForge, SmeltRules.FindNearestForge(map, MaginciaSmithy));
    }

    /// <summary>The Skara Brae smithy's forge, drawn into the map under the smithy's roof.</summary>
    private static readonly Point3D SkaraForge = new(636, 2193, 0);

    /// <summary>Where a Skara Brae seller's walk to that forge ended: three tiles off, out of its reach.</summary>
    private static readonly Point3D SkaraSeller = new(633, 2192, 0);

    private const int OneStep = 1;

    [RealMapFact]
    public void SkaraSmithy_TheForgeHasATileInReachBesideWhereTheSellerStopped()
    {
        // "no walk to the forge" ended 147 sale trips of one night; this seller stood three
        // tiles from the forge with a free tile in its reach one step away.
        var map = RealMapWorld.Felucca;

        Assert.Contains(SkaraForge, StaticForges.Near(map, SkaraSeller, SmeltRules.ForgeSearchRange));
        Assert.True(SmeltOrBank.TryForgeStand(map, SkaraForge, SkaraSeller, out var stand));
        Assert.True(NavMetric.Chebyshev(stand, SkaraForge) <= SmeltRules.ForgeRange, $"stand {stand}");
        Assert.Equal(OneStep, NavMetric.Chebyshev(stand, SkaraSeller));
    }

    [RealMapFact]
    public void MaginciaSmithy_TheForgeHasATileInReach()
    {
        var map = RealMapWorld.Felucca;

        Assert.True(SmeltOrBank.TryForgeStand(map, MaginciaForge, MaginciaSmithy, out var stand));
        Assert.True(NavMetric.Chebyshev(stand, MaginciaForge) <= SmeltRules.ForgeRange, $"stand {stand}");
    }

    [RealMapFact]
    public void BritainBank_HasNoMapArtForge() =>
        Assert.Empty(StaticForges.Near(RealMapWorld.Felucca, BritainBank, SmeltRules.ForgeSearchRange));
}
