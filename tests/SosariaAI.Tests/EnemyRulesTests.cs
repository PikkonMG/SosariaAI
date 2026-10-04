using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class EnemyRulesTests
{
    [Fact]
    public void IsEnemy_Player_IsFalse() =>
        Assert.False(EnemyRules.IsEnemy(-1000, false, true, false, false, false, false, false));

    [Fact]
    public void IsEnemy_Sosaria_IsFalse() =>
        Assert.False(EnemyRules.IsEnemy(-1000, true, false, true, false, false, false, false));

    [Fact]
    public void IsEnemy_Controlled_IsFalse() =>
        Assert.False(EnemyRules.IsEnemy(-1000, false, false, false, true, false, false, false));

    [Fact]
    public void IsEnemy_Summoned_IsFalse() =>
        Assert.False(EnemyRules.IsEnemy(-1000, false, false, false, false, true, false, false));

    [Fact]
    public void IsEnemy_Vendor_IsFalse() =>
        Assert.False(EnemyRules.IsEnemy(-1000, false, false, false, false, false, true, false));

    [Fact]
    public void IsEnemy_Invulnerable_IsFalse() =>
        Assert.False(EnemyRules.IsEnemy(-1000, false, false, false, false, false, false, true));

    [Fact]
    public void IsEnemy_NegativeKarma_IsTrue() =>
        Assert.True(EnemyRules.IsEnemy(-1, false, false, false, false, false, false, false));

    [Fact]
    public void IsEnemy_AlwaysMurderer_IsTrue() =>
        Assert.True(EnemyRules.IsEnemy(0, true, false, false, false, false, false, false));

    [Fact]
    public void IsEnemy_AnimalKarmaZero_IsFalse() =>
        Assert.False(EnemyRules.IsEnemy(0, false, false, false, false, false, false, false));

    [Fact]
    public void IsGuardedFlag_ActiveGuardsOnly()
    {
        Assert.True(EnemyRules.IsGuardedFlag(true, false));
        Assert.False(EnemyRules.IsGuardedFlag(true, true));
        Assert.False(EnemyRules.IsGuardedFlag(false, false));
    }
}
