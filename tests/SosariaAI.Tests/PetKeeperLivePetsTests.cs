using Server;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A crawler with no pet entered a dungeon floor and the server fell: the engine keeps no
/// follower set for a person who never had a pet, and the floor watch counted it.
/// </summary>
public class PetKeeperLivePetsTests
{
    public PetKeeperLivePetsTests() => TestMap.EnsureInternal();

    [Fact]
    public void LivePets_AnOwnerThatNeverHadAPet_HasNone()
    {
        var owner = new SosariaCharacter((Serial)0x7F01) { Name = "Lucan" };

        Assert.Null(owner.AllFollowers);
        Assert.Equal(0, PetKeeper.LivePets(owner));
    }

    [Fact]
    public void LivePets_NoOwner_HasNone() => Assert.Equal(0, PetKeeper.LivePets(null));
}
