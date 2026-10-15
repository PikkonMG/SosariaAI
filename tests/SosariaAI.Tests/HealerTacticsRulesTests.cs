using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>A healer treats the worst off of its friends first, and raises a fallen mate near and unhindered.</summary>
public class HealerTacticsRulesTests
{
    private const double FullHits = 1.0;
    private const double Scratched = 0.8;
    private const double Wounded = 0.5;
    private const double Dying = 0.2;
    private const int Beside = 1;
    private const int Far = HealerTacticsRules.FightRaiseTiles + 1;
    private const int SecondMate = 1;

    [Fact]
    public void NeedsCare_WoundedOrPoisoned()
    {
        Assert.False(HealerTacticsRules.NeedsCare(new MateCare(Scratched, false)));
        Assert.True(HealerTacticsRules.NeedsCare(new MateCare(Wounded, false)));
        Assert.True(HealerTacticsRules.NeedsCare(new MateCare(FullHits, true)));
    }

    [Fact]
    public void Pick_TheWorstOffFirst()
    {
        MateCare[] mates = [new(Wounded, false), new(Dying, false), new(Scratched, false)];

        Assert.Equal(SecondMate, HealerTacticsRules.Pick(mates));
    }

    [Fact]
    public void Pick_PoisonWeighsAsHitsLost()
    {
        MateCare[] mates = [new(Wounded, false), new(Wounded, true)];

        Assert.Equal(SecondMate, HealerTacticsRules.Pick(mates));
    }

    [Fact]
    public void Pick_NobodyHurt_IsNoPatient()
    {
        MateCare[] mates = [new(FullHits, false), new(Scratched, false)];

        Assert.Equal(HealerTacticsRules.NoPatient, HealerTacticsRules.Pick(mates));
        Assert.Equal(HealerTacticsRules.NoPatient, HealerTacticsRules.Pick([]));
    }

    [Fact]
    public void RaisesInFight_NearUnhinderedAndAble()
    {
        Assert.True(HealerTacticsRules.RaisesInFight(canRaise: true, hindered: false, Beside));
        Assert.False(HealerTacticsRules.RaisesInFight(canRaise: false, hindered: false, Beside));
        Assert.False(HealerTacticsRules.RaisesInFight(canRaise: true, hindered: true, Beside));
        Assert.False(HealerTacticsRules.RaisesInFight(canRaise: true, hindered: false, Far));
    }
}
