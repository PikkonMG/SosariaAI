using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class SpellBookTests
{
    private const double Grandmaster = 100;
    private const double Novice = 25;
    private const double NoMagery = 0;
    private const int FullMana = 100;
    private const int NoMana = 0;
    private const int ArrowOnlyMana = 5;
    private const int OpenGround = 6;
    private const int Adjacent = 1;
    private const int TopCircle = CastTiming.TopCircle;
    private const int Pressed = CastTiming.FirstCircle;
    private const int TooFar = SpellBook.Reach + 1;
    private const double LowRoll = 0.0;
    private const double HighRoll = 0.999;
    private const double AlwaysRoll = 0.0;
    private const double NeverRoll = 0.999;
    private const int ClosingDistance = 3;
    private const int FarDistance = 8;
    private const int NoCircle = 0;
    private const int FirstCircle = 1;
    private static readonly int[] PreAosManaByCircle = [4, 6, 9, 11, 14, 20, 40, 50];

    [Fact]
    public void Attacks_RunFirstToSeventhCircle_WithEngineMana()
    {
        var previousCircle = NoCircle;

        foreach (var entry in SpellBook.Attacks)
        {
            Assert.True(entry.Circle >= previousCircle);
            Assert.Equal(PreAosManaByCircle[entry.Circle - FirstCircle], entry.Mana);
            previousCircle = entry.Circle;
        }

        Assert.Equal(SpellKind.MagicArrow, SpellBook.Attacks[0].Kind);
        Assert.Equal(SpellKind.FlameStrike, SpellBook.Attacks[^1].Kind);
    }

    [Fact]
    public void EntryOf_ReturnsTheMatchingEntry()
    {
        foreach (var entry in SpellBook.Attacks)
        {
            Assert.Equal(entry, SpellBook.EntryOf(entry.Kind));
        }

        Assert.Equal(SpellKind.GreaterHeal, SpellBook.EntryOf(SpellKind.GreaterHeal).Kind);
    }

    [Fact]
    public void IsCaster_NeedsArrowMagery()
    {
        Assert.True(SpellBook.IsCaster(Novice));
        Assert.False(SpellBook.IsCaster(NoMagery));
    }

    [Fact]
    public void Fits_OnlyAtOrUnderTheSafeCircle()
    {
        Assert.True(SpellBook.Fits(SpellBook.MagicArrow, Pressed));
        Assert.True(SpellBook.Fits(SpellBook.Heal, Pressed));
        Assert.False(SpellBook.Fits(SpellBook.Cure, Pressed));
        Assert.False(SpellBook.Fits(SpellBook.FlameStrike, SpellBook.EnergyBolt.Circle));
        Assert.True(SpellBook.Fits(SpellBook.FlameStrike, TopCircle));
    }

    [Fact]
    public void PickAttack_UnderPressure_OnlyMagicArrow()
    {
        Assert.Equal(SpellKind.MagicArrow, SpellBook.PickAttack(Grandmaster, FullMana, Adjacent, Pressed, LowRoll));
        Assert.Equal(SpellKind.MagicArrow, SpellBook.PickAttack(Grandmaster, FullMana, Adjacent, Pressed, HighRoll));
    }

    [Fact]
    public void PickAttack_HeldFoe_OpensTheBook() =>
        Assert.NotEqual(SpellKind.MagicArrow, SpellBook.PickAttack(Grandmaster, FullMana, Adjacent, TopCircle, LowRoll));

    [Fact]
    public void WardOf_ReactiveArmorBeforeUor_ProtectionAfter()
    {
        Assert.Equal(SpellKind.ReactiveArmor, SpellBook.WardOf(protectionGuardsCasts: false));
        Assert.Equal(SpellKind.Protection, SpellBook.WardOf(protectionGuardsCasts: true));
        Assert.Equal(CastTiming.FirstCircle, SpellBook.ReactiveArmor.Circle);
    }

    [Fact]
    public void RaisesCursor_AllButUorProtection()
    {
        Assert.True(SpellBook.RaisesCursor(SpellKind.FlameStrike, protectionGuardsCasts: true));
        Assert.True(SpellBook.RaisesCursor(SpellKind.Protection, protectionGuardsCasts: false));
        Assert.False(SpellBook.RaisesCursor(SpellKind.Protection, protectionGuardsCasts: true));
    }

    [Fact]
    public void PickAttack_Grandmaster_PicksAmongTheStrongestFew()
    {
        var strongest = SpellBook.PickAttack(Grandmaster, FullMana, OpenGround, TopCircle, LowRoll);
        var weakestInPool = SpellBook.PickAttack(Grandmaster, FullMana, OpenGround, TopCircle, HighRoll);
        var poolFloor = SpellBook.Attacks[SpellBook.Attacks.Length - SpellBook.PoolDepth].Kind;

        Assert.Equal(SpellKind.FlameStrike, strongest);
        Assert.Equal(poolFloor, weakestInPool);
    }

    [Fact]
    public void PickAttack_Novice_StaysInReach()
    {
        var pick = SpellBook.PickAttack(Novice, FullMana, OpenGround, TopCircle, LowRoll);

        Assert.Equal(SpellKind.MagicArrow, pick);
    }

    [Fact]
    public void PickAttack_LowMana_FallsToArrow() =>
        Assert.Equal(SpellKind.MagicArrow, SpellBook.PickAttack(Grandmaster, ArrowOnlyMana, OpenGround, TopCircle, LowRoll));

    [Fact]
    public void PickAttack_NoManaOrOutOfReach_IsNull()
    {
        Assert.Null(SpellBook.PickAttack(Grandmaster, NoMana, OpenGround, TopCircle, LowRoll));
        Assert.Null(SpellBook.PickAttack(Grandmaster, FullMana, TooFar, TopCircle, LowRoll));
    }

    [Fact]
    public void PickUtility_ParalyzeOnAClosingFoe()
    {
        Assert.Equal(
            SpellKind.Paralyze,
            SpellBook.PickUtility(Grandmaster, FullMana, ClosingDistance, TopCircle, false, false, AlwaysRoll, NeverRoll)
        );
        Assert.Null(SpellBook.PickUtility(Grandmaster, FullMana, ClosingDistance, TopCircle, true, true, AlwaysRoll, NeverRoll));
        Assert.Null(SpellBook.PickUtility(Grandmaster, FullMana, FarDistance, TopCircle, false, true, AlwaysRoll, NeverRoll));
    }

    [Fact]
    public void PickUtility_PoisonOnlyOnACleanFoe()
    {
        Assert.Equal(
            SpellKind.Poison,
            SpellBook.PickUtility(Grandmaster, FullMana, FarDistance, TopCircle, false, false, NeverRoll, AlwaysRoll)
        );
        Assert.Null(SpellBook.PickUtility(Grandmaster, FullMana, FarDistance, TopCircle, false, true, NeverRoll, AlwaysRoll));
    }

    [Fact]
    public void PickUtility_NoParalyzeWhenItsWordsWouldBeBroken() =>
        Assert.Null(
            SpellBook.PickUtility(Grandmaster, FullMana, ClosingDistance, Pressed, false, true, AlwaysRoll, NeverRoll)
        );

    [Fact]
    public void PickUtility_UnskilledOrUnlucky_IsNull()
    {
        Assert.Null(SpellBook.PickUtility(Novice, FullMana, ClosingDistance, TopCircle, false, false, AlwaysRoll, AlwaysRoll));
        Assert.Null(SpellBook.PickUtility(Grandmaster, FullMana, ClosingDistance, TopCircle, false, false, NeverRoll, NeverRoll));
    }
}
