using Server;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

public class PartyRoadsTests
{
    [Fact]
    public void Leads_FalseForNoOneAndForAPersonWithNoRoadGroup()
    {
        // Only a road group's leader backs an invite with its call; standing about backs none.
        var standingAbout = new SosariaCharacter((Serial)0x6E51) { Name = "Odric" };

        Assert.False(PartyRoads.Leads(null));
        Assert.False(PartyRoads.Leads(standingAbout));
    }
}
