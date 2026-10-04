using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class MoongateGuardsTests
{
    private const string Felucca = "Felucca";
    private const string Trammel = "Trammel";
    private const string Britain = "Britain";
    private const string Yew = "Yew";
    private const int BritainGate = 1012004;
    private const int MoonglowGate = 1012003;
    private const int JhelomGate = 1012005;
    private const int UnknownGate = 1;

    [Fact]
    public void IsSharedGateRegion_OnlyTheFeluccaMoongates()
    {
        Assert.True(MoongateGuards.IsSharedGateRegion(Felucca, MoongateGuards.MoongateRegionName));
        Assert.False(MoongateGuards.IsSharedGateRegion(Felucca, Britain));
        Assert.False(MoongateGuards.IsSharedGateRegion(Trammel, MoongateGuards.MoongateRegionName));
    }

    [Fact]
    public void KeepsGuards_TheEraKeptBritainMoonglowAndJhelom()
    {
        var guarded = new CharactersConfiguration().FeluccaGuardedMoongates;

        Assert.True(MoongateGuards.KeepsGuards(MoongateGuards.TownOf(BritainGate), guarded));
        Assert.True(MoongateGuards.KeepsGuards(MoongateGuards.TownOf(MoonglowGate), guarded));
        Assert.True(MoongateGuards.KeepsGuards(MoongateGuards.TownOf(JhelomGate), guarded));
        Assert.False(MoongateGuards.KeepsGuards(Yew, guarded));
    }

    [Fact]
    public void KeepsGuards_ReadsTheOperatorsListInAnyCase()
    {
        Assert.True(MoongateGuards.KeepsGuards(Yew, [" yew "]));
        Assert.False(MoongateGuards.KeepsGuards(Britain, []));
        Assert.False(MoongateGuards.KeepsGuards(null, [Britain]));
    }

    [Fact]
    public void TownOf_NamesEveryFeluccaGateAndNothingElse()
    {
        Assert.Equal(Britain, MoongateGuards.TownOf(BritainGate));
        Assert.Null(MoongateGuards.TownOf(UnknownGate));
    }
}
