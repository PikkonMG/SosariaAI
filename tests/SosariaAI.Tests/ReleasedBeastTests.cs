using Server;
using Server.Mobiles;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A tame takes the beast off its spawner, which spawns its place again. The engine keeps a
/// let-go beast three days, and practice tames added 530 beasts to the world in one night. A
/// let-go beast that nobody tamed again leaves the world a minute later.
/// </summary>
public class ReleasedBeastTests
{
    private const uint WildSerial = 0x5F10;
    private const uint RetamedSerial = 0x5F11;

    public ReleasedBeastTests()
    {
        TestMap.EnsureInternal();
        TestMap.EnsureRunningWorld();
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    public void LeavesAfterRelease_OnlyAStillWildBeastGoes(bool deleted, bool controlled, bool leaves)
    {
        Assert.Equal(leaves, PetRules.LeavesAfterRelease(deleted, controlled));
    }

    [Fact]
    public void RemoveIfStillWild_DeletesALetGoBeast()
    {
        var beast = Beast(WildSerial);

        Assert.True(PetKeeper.RemoveIfStillWild(beast));
        Assert.True(beast.Deleted);
    }

    [Fact]
    public void RemoveIfStillWild_KeepsABeastTamedAgain()
    {
        var beast = Beast(RetamedSerial);
        beast.Controlled = true;

        try
        {
            Assert.False(PetKeeper.RemoveIfStillWild(beast));
            Assert.False(beast.Deleted);
        }
        finally
        {
            beast.Delete();
        }
    }

    // The serial constructor is the load path: it sets none of the defaults a new mobile has.
    private static GreyWolf Beast(uint serial)
    {
        var beast = new GreyWolf((Serial)serial);
        beast.DefaultMobileInit();
        return beast;
    }
}
