using Server;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// The healer of Buccaneer's Den on the real Felucca tiles. The Den has no guards, so its
/// healer is where a red buys bandages. The healer stands deeper in the shop than the indoor
/// allowance, and before the goal's own building counted, no route reached it and the
/// catalog left it out.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapDenShopTests
{
    /// <summary>The Den healer spawner in ModernUO's shared Felucca vendor spawns.</summary>
    private static readonly Point3D DenHealer = new(2709, 2130, 0);

    /// <summary>The street node north of the shop, the nearest one to the healer.</summary>
    private static readonly Point3D NorthStreet = new(2708, 2117, 0);

    [RealMapFact]
    public void DenHealer_IsInTheCatalog() =>
        RealMapWorld.WithLiveGraph(() =>
        {
            var draft = new DestinationDraft
            {
                Name = $"Healer {DenHealer.X}-{DenHealer.Y}",
                Kind = nameof(DestinationKind.Healer),
                Role = nameof(DestinationKind.Healer),
                X = DenHealer.X,
                Y = DenHealer.Y,
                Z = DenHealer.Z
            };

            var catalog = CatalogBuilder.Build([draft], RealMapWorld.LiveGraph(), RealMapWorld.Walker(), RealMapWorld.IsIndoor);

            Assert.Contains(catalog.All, destination => destination.Name == draft.Name);
        });

    [RealMapFact]
    public void DenHealer_WorldWalkFromTheNorthStreet_ReachesTheCounter()
    {
        var path = TileRoute.Find(NorthStreet, DenHealer, RealMapWorld.Walker(), RealMapWorld.IsIndoor, 0, out var reason);

        Assert.True(path.Count > 0, reason);
    }
}
