using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class MageRulesTests
{
    private const string ExpectedKind = "Mage";
    private const int ExpectedReachTiles = 8;

    [Fact]
    public void Kind_IsMage()
    {
        Assert.Equal(ExpectedKind, MageRules.Kind);
        Assert.Equal(ExpectedKind, new MageSkill().Name);
    }

    [Fact]
    public void Reach_IsEightTiles() =>
        Assert.Equal(ExpectedReachTiles, MageRules.ReachTiles);

    [Fact]
    public void IsMageTarget_RejectsPlayerVendorAndFriend()
    {
        Assert.False(MageRules.IsMageTarget(
            isSelf: false,
            isPlayer: true,
            isSosaria: false,
            isVendor: false,
            invulnerable: false,
            isControlled: false,
            isSummoned: false,
            karma: 0,
            alwaysMurderer: false,
            attackerIsPk: false
        ));
        Assert.False(MageRules.IsMageTarget(
            isSelf: false,
            isPlayer: false,
            isSosaria: false,
            isVendor: true,
            invulnerable: false,
            isControlled: false,
            isSummoned: false,
            karma: -100,
            alwaysMurderer: false,
            attackerIsPk: false
        ));
        Assert.True(MageRules.IsMageTarget(
            isSelf: false,
            isPlayer: false,
            isSosaria: false,
            isVendor: false,
            invulnerable: false,
            isControlled: false,
            isSummoned: false,
            karma: -1000,
            alwaysMurderer: true,
            attackerIsPk: false
        ));
        Assert.True(MageRules.IsMageTarget(
            isSelf: false,
            isPlayer: true,
            isSosaria: false,
            isVendor: false,
            invulnerable: false,
            isControlled: false,
            isSummoned: false,
            karma: 0,
            alwaysMurderer: false,
            attackerIsPk: true
        ));
    }

    [Fact]
    public void MayCastHeal_NeedsHurtManaAndMagery()
    {
        Assert.True(MageRules.MayCastHeal(hits: 10, hitsMax: 20, mana: 4, magery: 20));
        Assert.False(MageRules.MayCastHeal(hits: 20, hitsMax: 20, mana: 4, magery: 20));
        Assert.False(MageRules.MayCastHeal(hits: 10, hitsMax: 20, mana: 3, magery: 20));
        Assert.False(MageRules.MayCastHeal(hits: 10, hitsMax: 20, mana: 4, magery: 0));
    }

    [Fact]
    public void MayCastArrow_NeedsManaAndMagery()
    {
        Assert.True(MageRules.MayCastArrow(mana: 4, magery: 20));
        Assert.False(MageRules.MayCastArrow(mana: 0, magery: 20));
    }
}
