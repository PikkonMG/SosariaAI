using Server;
using Server.Items;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class PoisonRulesTests
{
    private const string ExpectedKind = "Poison";
    private const int ExpectedLesserLevel = 0;
    private const int ExpectedRegularLevel = 1;
    private const int ExpectedGreaterLevel = 2;
    private const int ExpectedDeadlyLevel = 3;
    private const int ExpectedLethalLevel = 4;
    private const int ExpectedChargesBase = 18;
    private const int ExpectedChargesPerLevel = 2;
    private const int ExpectedPotionConsumeAmount = 1;

    [Fact]
    public void Kind_IsPoison()
    {
        Assert.Equal(ExpectedKind, PoisonRules.Kind);
        Assert.Equal(ExpectedKind, new PoisonSkill().Name);
    }

    [Fact]
    public void IsPoisonable_OnlyOneHandedBladesAndPoints()
    {
        // The engine's pre-AOS rule: "You can only poison bladed or piercing weapons, food or drink."
        Assert.True(PoisonRules.IsPoisonable(Layer.OneHanded, WeaponType.Slashing));
        Assert.True(PoisonRules.IsPoisonable(Layer.OneHanded, WeaponType.Piercing));
        Assert.False(PoisonRules.IsPoisonable(Layer.OneHanded, WeaponType.Bashing));
        Assert.False(PoisonRules.IsPoisonable(Layer.TwoHanded, WeaponType.Slashing));
    }

    [Fact]
    public void HasWork_NoneOutOfTheWorld() =>
        Assert.False(PoisonRules.HasWork(null));

    [Fact]
    public void PoisonCharges_MatchModernUOFormula()
    {
        Assert.Equal(ExpectedChargesBase, PoisonRules.PoisonChargesBase);
        Assert.Equal(ExpectedChargesPerLevel, PoisonRules.PoisonChargesPerLevel);
        Assert.Equal(
            ExpectedChargesBase,
            PoisonRules.ChargesFor(ExpectedLesserLevel));
        Assert.Equal(
            ExpectedChargesBase - ExpectedRegularLevel * ExpectedChargesPerLevel,
            PoisonRules.ChargesFor(ExpectedRegularLevel));
        Assert.Equal(
            ExpectedChargesBase - ExpectedGreaterLevel * ExpectedChargesPerLevel,
            PoisonRules.ChargesFor(ExpectedGreaterLevel));
        Assert.Equal(
            ExpectedChargesBase - ExpectedDeadlyLevel * ExpectedChargesPerLevel,
            PoisonRules.ChargesFor(ExpectedDeadlyLevel));
        Assert.Equal(
            ExpectedChargesBase - ExpectedLethalLevel * ExpectedChargesPerLevel,
            PoisonRules.ChargesFor(ExpectedLethalLevel));
    }

    [Fact]
    public void PotionConsumeAmount_IsOne() =>
        Assert.Equal(ExpectedPotionConsumeAmount, PoisonRules.PotionConsumeAmount);

    [Fact]
    public void FindPotion_Null_IsNull() =>
        Assert.Null(PoisonRules.FindPotion(null));

    [Fact]
    public void FindWeapon_Null_IsNull() =>
        Assert.Null(PoisonRules.FindWeapon(null));

    [Fact]
    public void Apply_NoTargetOrPoison_IsFalse()
    {
        Assert.False(PoisonRules.Apply(null, null, null));
        Assert.False(PoisonRules.Apply(null, Poison.Lesser, null));
    }
}
