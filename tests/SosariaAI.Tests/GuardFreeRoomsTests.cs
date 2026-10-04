using Server;
using Server.Regions;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class GuardFreeRoomsTests
{
    private const string Den = "Buccaneer's Den";
    private const string Britain = "Britain";
    private const int TownPriority = 50;

    /// <summary>The Den's inn rooms, one of the areas the engine data gives the Den's child region.</summary>
    private static readonly Rectangle3D DenInn = new(new Point3D(2664, 2232, Region.MinZ), new Point3D(2688, 2240, Region.MaxZ));

    private static readonly Rectangle3D DenTown = new(new Point3D(2612, 2057, Region.MinZ), new Point3D(2776, 2267, Region.MaxZ));

    [Fact]
    public void TurnsOff_OnlyARoomWithGuardsInATownWithout()
    {
        Assert.True(GuardFreeRooms.TurnsOff(ownGuardsOff: false, townGuardsOff: true));
        Assert.False(GuardFreeRooms.TurnsOff(ownGuardsOff: false, townGuardsOff: false));
        Assert.False(GuardFreeRooms.TurnsOff(ownGuardsOff: true, townGuardsOff: true));
    }

    [Fact]
    public void Apply_TheDenInnLosesItsGuards_ABritainRoomKeepsThem()
    {
        var map = TestMap.EnsureLand();
        var den = new GuardedRegion(Den, map, TownPriority, DenTown) { GuardsDisabled = true };
        var inn = new GuardedRegion(null, map, den, DenInn);
        var innRoom = new GuardedRegion(null, map, inn, DenInn);
        var britain = new GuardedRegion(Britain, map, TownPriority, DenTown);
        var britainRoom = new GuardedRegion(null, map, britain, DenInn);

        Assert.Equal(2, GuardFreeRooms.Apply([den, inn, innRoom, britain, britainRoom]));
        Assert.True(inn.IsDisabled());
        Assert.True(innRoom.IsDisabled());
        Assert.False(britain.IsDisabled());
        Assert.False(britainRoom.IsDisabled());
    }
}
