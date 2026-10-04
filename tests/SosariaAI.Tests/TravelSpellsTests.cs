using Server;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class TravelSpellsTests
{
    private const uint CancelSerial = 0x7D71;

    /// <summary>Fourth circle: what Recall costs in mana.</summary>
    private const int RecallMana = 11;

    private const int EnoughMana = 30;
    private static readonly Point3D Yew = new(652, 820, 0);
    private static readonly Point3D BritainBank = new(1434, 1699, 0);

    [Fact]
    public void Lines_AreEasyToCount()
    {
        Assert.Equal("recalled from (652, 820, 0) to (1434, 1699, 0)", TravelSpells.RecalledLine(Yew, BritainBank));
        Assert.Equal("opened a gate to (1434, 1699, 0)", TravelSpells.GateOpenedLine(BritainBank));
        Assert.Equal("marked a rune at (652, 820, 0)", TravelSpells.MarkedLine(Yew));
    }

    [Theory]
    [InlineData(TravelSpellKind.Recall, null, "fizzled recall")]
    [InlineData(TravelSpellKind.Gate, null, "fizzled gate")]
    [InlineData(TravelSpellKind.Mark, null, "fizzled mark")]
    [InlineData(TravelSpellKind.Recall, TravelSpells.LostWordsWhy, "fizzled recall (lost the words)")]
    public void FizzledLine_StartsWithFizzledAndTheSpell(TravelSpellKind kind, string why, string line) =>
        Assert.Equal(line, TravelSpells.FizzledLine(kind, why));

    [Theory]
    [InlineData(TravelSpellKind.Recall)]
    [InlineData(TravelSpellKind.Mark)]
    [InlineData(TravelSpellKind.Gate)]
    public void NoCaster_CastsNothing(TravelSpellKind kind)
    {
        Assert.False(TravelSpells.PlaceAllows(null, kind, null));
        Assert.False(TravelSpells.CanCastToward(null, kind, null));
        Assert.False(TravelSpells.Begin(null, kind, null));
        Assert.Null(TravelSpells.GateBeside(null));
    }

    [Theory]
    [InlineData(TravelSpellKind.Recall, true, false, false, false, EnoughMana, TravelSpells.HeatWhy)]
    [InlineData(TravelSpellKind.Gate, true, false, false, false, EnoughMana, TravelSpells.HeatWhy)]
    [InlineData(TravelSpellKind.Mark, true, false, false, false, EnoughMana, null)]
    [InlineData(TravelSpellKind.Recall, false, true, false, false, EnoughMana, TravelSpells.OverloadedWhy)]
    [InlineData(TravelSpellKind.Gate, false, true, false, false, EnoughMana, null)]
    [InlineData(TravelSpellKind.Recall, false, false, true, false, EnoughMana, TravelSpells.FrozenWhy)]
    [InlineData(TravelSpellKind.Recall, false, false, false, true, EnoughMana, TravelSpells.RecoveringWhy)]
    [InlineData(TravelSpellKind.Recall, false, false, false, false, RecallMana - 1, TravelSpells.ManaWhy)]
    [InlineData(TravelSpellKind.Recall, false, false, false, false, RecallMana, null)]
    public void EngineRefusal_TheSpellsOwnSilentNo(
        TravelSpellKind kind,
        bool heat,
        bool overloaded,
        bool frozen,
        bool recovering,
        int mana,
        string why
    ) =>
        // The engine spell refuses without a word: reds on the Den were sent to camps a
        // recall "could" reach, and no recall ever began.
        Assert.Equal(why, TravelSpells.EngineRefusal(kind, heat, overloaded, frozen, recovering, mana, RecallMana));

    [Theory]
    [InlineData(TravelSpells.HeatWhy, true)]
    [InlineData(TravelSpells.FightingWhy, true)]
    [InlineData(TravelSpells.CastingWhy, true)]
    [InlineData(TravelSpells.RecoveringWhy, true)]
    [InlineData(TravelSpells.ManaWhy, true)]
    [InlineData(TravelSpells.BookRestWhy, true)]
    [InlineData(TravelSpells.LandingTakenWhy, true)]
    [InlineData(TravelSpells.OverloadedWhy, false)]
    [InlineData(TravelSpells.NoMeansWhy, false)]
    [InlineData(TravelSpells.PlaceWhy, false)]
    [InlineData(RecallRules.NoRuneWhy, false)]
    [InlineData(null, false)]
    public void Passes_OnlyARefusalThatWearsOffSoon(string why, bool passes) =>
        Assert.Equal(passes, TravelSpells.Passes(why));

    [Fact]
    public void Cancel_NoCaster_DoesNothing() => TravelSpells.Cancel(null);

    [Fact]
    public void Cancel_LeavesNoCastAndNoOutcome()
    {
        TestMap.EnsureInternal();
        var caster = new SosariaCharacter((Serial)CancelSerial);

        TravelSpells.Cancel(caster);

        Assert.False(TravelSpells.IsCasting(caster));
        Assert.Equal(TravelCastOutcome.None, TravelSpells.TakeOutcome(caster));
    }
}
