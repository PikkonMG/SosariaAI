using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class PartyMembershipTests
{
    [Fact]
    public void IsMember_CopiesOfAMemberAreNotInTheParty()
    {
        // Eight copies of each crew member, homed in every town, followed the one
        // Britain leader across the world. Only the listed ids are in the party.
        Party.Configure([
            new PartyDefinition
            {
                Id = "Felucca:graveyard-crew",
                Leader = "Felucca:bran",
                Members = ["Felucca:bran", "Felucca:sela"],
                MeetAt = new Point3D(1425, 1695, 0)
            }
        ]);

        Assert.True(Party.IsMember("Felucca:graveyard-crew", "Felucca:bran"));
        Assert.True(Party.IsMember("Felucca:graveyard-crew", "Felucca:sela"));
        Assert.False(Party.IsMember("Felucca:graveyard-crew", "Felucca:sela#3"));
        Assert.False(Party.IsMember("Felucca:graveyard-crew", "Felucca:tam"));
        Assert.False(Party.IsMember("Felucca:no-such-party", "Felucca:bran"));
        Assert.False(Party.IsMember("Felucca:graveyard-crew", null));
    }
}
