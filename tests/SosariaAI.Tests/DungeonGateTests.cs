using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class DungeonGateTests
{
    private const string Destard = "Destard";
    private const string Shame = "Shame";

    [Fact]
    public void EntryLine_NamesPersonDungeonAndLevel() =>
        Assert.Equal("Anna entered Destard level 2", DungeonGate.EntryLine("Anna", Destard, 2));

    [Fact]
    public void IsNewArrival_OnlyANewDungeonOrLevel()
    {
        var levelOne = new DungeonFloor(Destard, 1, 0);
        var otherHallSameLevel = new DungeonFloor(Destard, 1, 4);
        var levelTwo = new DungeonFloor(Destard, 2, 1);

        Assert.True(DungeonGate.IsNewArrival(null, levelOne));
        Assert.False(DungeonGate.IsNewArrival((Destard, 1), otherHallSameLevel));
        Assert.True(DungeonGate.IsNewArrival((Destard, 1), levelTwo));
        Assert.True(DungeonGate.IsNewArrival((Shame, 1), levelOne));
        Assert.False(DungeonGate.IsNewArrival((Destard, 1), null));
    }

    [Fact]
    public void NoteWhere_NoCharacter_IsNoFloor() =>
        Assert.Null(DungeonGate.NoteWhere(null));
}
