using Server.Items;
using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>An explosion potion goes only at a held person out of the blast and in reach; a Magic Trap pouch frees a paralyze.</summary>
public class CombatItemRulesTests
{
    private const int InReach = 5;
    private const int InBlast = CombatItemRules.BlastTiles;
    private const int PastReach = CombatItemRules.ThrowReachTiles + 1;

    [Fact]
    public void ThrowsExplosion_HeldPersonInReach()
    {
        Assert.True(CombatItemRules.ThrowsExplosion(foeIsPerson: true, foeHeld: true, keepsDistance: true, InReach, ml: false));
        Assert.True(CombatItemRules.ThrowsExplosion(foeIsPerson: true, foeHeld: true, keepsDistance: true, CombatItemRules.ThrowReachTiles, ml: false));
    }

    [Fact]
    public void ThrowsExplosion_NotAtAFreeFoeOrAMonster()
    {
        Assert.False(CombatItemRules.ThrowsExplosion(foeIsPerson: true, foeHeld: false, keepsDistance: true, InReach, ml: false));
        Assert.False(CombatItemRules.ThrowsExplosion(foeIsPerson: false, foeHeld: true, keepsDistance: true, InReach, ml: false));
    }

    [Fact]
    public void ThrowsExplosion_NotIntoItsOwnBlastNorPastTheCursor()
    {
        Assert.False(CombatItemRules.ThrowsExplosion(foeIsPerson: true, foeHeld: true, keepsDistance: true, InBlast, ml: false));
        Assert.False(CombatItemRules.ThrowsExplosion(foeIsPerson: true, foeHeld: true, keepsDistance: false, InReach, ml: false));
        Assert.False(CombatItemRules.ThrowsExplosion(foeIsPerson: true, foeHeld: true, keepsDistance: true, PastReach, ml: false));
        Assert.True(CombatItemRules.ThrowsExplosion(foeIsPerson: true, foeHeld: true, keepsDistance: true, PastReach, ml: true));
    }

    [Fact]
    public void FreesFromParalyze_OnlyAMagicTrap()
    {
        Assert.True(CombatItemRules.FreesFromParalyze(TrapType.MagicTrap));
        Assert.False(CombatItemRules.FreesFromParalyze(TrapType.None));
        Assert.False(CombatItemRules.FreesFromParalyze(TrapType.PoisonTrap));
    }
}
