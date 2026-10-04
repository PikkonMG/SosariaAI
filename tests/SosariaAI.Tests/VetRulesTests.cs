using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class VetRulesTests
{
    private const string ExpectedKind = "Vet";
    private const int ExpectedReachTiles = 8;
    private const int InjuredHits = 50;
    private const int FullHits = 100;
    private const uint LoneSerial = 0x8B02;

    [Fact]
    public void Kind_IsVet()
    {
        Assert.Equal(ExpectedKind, VetRules.Kind);
        Assert.Equal(ExpectedKind, new VetSkill().Name);
    }

    [Fact]
    public void Reach_IsEightTiles() =>
        Assert.Equal(ExpectedReachTiles, VetRules.ReachTiles);

    [Fact]
    public void MayVet_InjuredVsFull()
    {
        Assert.True(VetRules.MayVet(InjuredHits, FullHits, poisoned: false));
        Assert.False(VetRules.MayVet(FullHits, FullHits, poisoned: false));
    }

    [Fact]
    public void Begin_NoHurtBeastInReach_SaysSo()
    {
        // Tamers at the Moonglow bank chose "tend a beast" with nothing to tend, 267 times.
        TestMap.EnsureInternal();
        var vet = new SosariaAI.Mobiles.SosariaCharacter((Serial)LoneSerial);
        var skill = new VetSkill();

        Assert.False(VetSkill.HasWork(vet));
        Assert.False(skill.Begin(vet));
        Assert.Equal(VetRules.NoPatientWhy, skill.FailReason);
    }

    [Fact]
    public void MayVet_PoisonedAtFull_IsTrue() =>
        Assert.True(VetRules.MayVet(FullHits, FullHits, poisoned: true));
}
