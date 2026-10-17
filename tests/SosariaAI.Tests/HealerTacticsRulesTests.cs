using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A healer treats the worst off of its friends first, and raises a fallen mate near and
/// unhindered. Any other caster heals only a friend deep in trouble, while it stands safe.
/// </summary>
public class HealerTacticsRulesTests
{
    private const double FullHits = 1.0;
    private const double Scratched = 0.8;
    private const double Wounded = 0.5;
    private const double Dying = 0.2;
    private const double DeepWound = 0.4;
    private const int Beside = 1;
    private const int Far = HealerTacticsRules.FightRaiseTiles + 1;
    private const int SecondMate = 1;

    [Fact]
    public void NeedsCare_WoundedOrPoisoned()
    {
        Assert.False(HealerTacticsRules.NeedsCare(new MateCare(Scratched, false), healer: true));
        Assert.True(HealerTacticsRules.NeedsCare(new MateCare(Wounded, false), healer: true));
        Assert.True(HealerTacticsRules.NeedsCare(new MateCare(FullHits, true), healer: true));
    }

    [Fact]
    public void NeedsCare_OtherCaster_OnlyADeepWound()
    {
        Assert.False(HealerTacticsRules.NeedsCare(new MateCare(Wounded, false), healer: false));
        Assert.False(HealerTacticsRules.NeedsCare(new MateCare(FullHits, true), healer: false));
        Assert.True(HealerTacticsRules.NeedsCare(new MateCare(DeepWound, false), healer: false));
    }

    [Fact]
    public void HealsMates_HealerAlways_OtherCasterOnlyWhenSafe()
    {
        Assert.True(HealerTacticsRules.HealsMates(healer: true, Dying, ownPoisoned: true));
        Assert.True(HealerTacticsRules.HealsMates(healer: false, Scratched, ownPoisoned: false));
        Assert.False(HealerTacticsRules.HealsMates(healer: false, Wounded, ownPoisoned: false));
        Assert.False(HealerTacticsRules.HealsMates(healer: false, Scratched, ownPoisoned: true));
    }

    [Fact]
    public void MayAid_NoCrime_OrARedHealer()
    {
        Assert.True(HealerTacticsRules.MayAid(crime: false, healerRed: false));
        Assert.False(HealerTacticsRules.MayAid(crime: true, healerRed: false));
        Assert.True(HealerTacticsRules.MayAid(crime: true, healerRed: true));
    }

    [Fact]
    public void Pick_OtherCaster_PassesOverALightWound()
    {
        MateCare[] mates = [new(Wounded, false), new(DeepWound, false)];

        Assert.Equal(SecondMate, HealerTacticsRules.Pick(mates, healer: false));
        Assert.Equal(HealerTacticsRules.NoPatient, HealerTacticsRules.Pick([new(Wounded, true)], healer: false));
    }

    [Fact]
    public void Pick_TheWorstOffFirst()
    {
        MateCare[] mates = [new(Wounded, false), new(Dying, false), new(Scratched, false)];

        Assert.Equal(SecondMate, HealerTacticsRules.Pick(mates, healer: true));
    }

    [Fact]
    public void Pick_PoisonWeighsAsHitsLost()
    {
        MateCare[] mates = [new(Wounded, false), new(Wounded, true)];

        Assert.Equal(SecondMate, HealerTacticsRules.Pick(mates, healer: true));
    }

    [Fact]
    public void Pick_NobodyHurt_IsNoPatient()
    {
        MateCare[] mates = [new(FullHits, false), new(Scratched, false)];

        Assert.Equal(HealerTacticsRules.NoPatient, HealerTacticsRules.Pick(mates, healer: true));
        Assert.Equal(HealerTacticsRules.NoPatient, HealerTacticsRules.Pick([], healer: true));
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
