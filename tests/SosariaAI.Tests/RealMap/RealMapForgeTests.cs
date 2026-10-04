using Server;
using SosariaAI.Economy;
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

    [RealMapFact]
    public void BritainBank_HasNoMapArtForge() =>
        Assert.Empty(StaticForges.Near(RealMapWorld.Felucca, BritainBank, SmeltRules.ForgeSearchRange));
}
