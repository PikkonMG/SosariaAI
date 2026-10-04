using Server;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

public class KeepFedTests
{
    public KeepFedTests() => TestMap.EnsureInternal();

    [Fact]
    public void KeepFed_FillsAStarvingLoadToFull()
    {
        var person = new SosariaCharacter((Serial)0x7181) { Name = "Odo" };
        Assert.Equal(0, person.Hunger);

        person.KeepFed();

        Assert.Equal(SosariaCharacter.FullHunger, person.Hunger);
        Assert.Equal(SosariaCharacter.FullHunger, person.Thirst);
    }
}
