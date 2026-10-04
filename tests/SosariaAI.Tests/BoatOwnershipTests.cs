using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class BoatOwnershipTests
{
    [Fact]
    public void OwnedBoat_ZeroSerial_IsNull() =>
        Assert.Null(BoatRules.OwnedBoat(BoatRules.NoBoatSerial));

    [Fact]
    public void NearerPlank_NoBoat_IsNull() =>
        Assert.Null(BoatRules.NearerPlank(null, Point3D.Zero));
}
