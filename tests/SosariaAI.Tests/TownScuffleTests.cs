using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class TownScuffleTests
{
    private const string Britain = "Britain";

    private static readonly Serial OrderOne = (Serial)1u;
    private static readonly Serial OrderTwo = (Serial)2u;
    private static readonly Serial ChaosOne = (Serial)3u;
    private static readonly Serial Bystander = (Serial)4u;
    private static readonly DateTime Now = new(2026, 9, 28, 20, 0, 0);
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(TownScuffleSettings.DefaultTimeLimitSeconds);

    private static TownScuffle TwoOnOne() =>
        new(new ScuffleTown(null, Britain), [OrderOne, OrderTwo], [ChaosOne], Now, Limit);

    [Fact]
    public void Sides_HoldOnlyTheChosenFighters()
    {
        var scuffle = TwoOnOne();

        Assert.Equal(3, scuffle.Fighters);
        Assert.True(scuffle.IsOrder(OrderTwo));
        Assert.True(scuffle.IsChaos(ChaosOne));
        Assert.False(scuffle.Contains(Bystander));
        Assert.Equal(scuffle.Chaos, scuffle.FoesOf(OrderOne));
        Assert.Equal(scuffle.Order, scuffle.FoesOf(ChaosOne));
    }

    [Fact]
    public void Opposes_OnlyAcrossTheSidesAndOnlyWhileBothAreIn()
    {
        var scuffle = TwoOnOne();

        Assert.True(scuffle.Opposes(OrderOne, ChaosOne));
        Assert.True(scuffle.Opposes(ChaosOne, OrderTwo));
        Assert.False(scuffle.Opposes(OrderOne, OrderTwo));
        Assert.False(scuffle.Opposes(OrderOne, Bystander));

        scuffle.MarkOut(ChaosOne);

        Assert.False(scuffle.Opposes(OrderOne, ChaosOne));
    }

    [Fact]
    public void MarkOut_IsForGoodAndIgnoresABystander()
    {
        var scuffle = TwoOnOne();

        scuffle.MarkOut(OrderOne);
        scuffle.MarkOut(Bystander);

        Assert.True(scuffle.IsOut(OrderOne));
        Assert.False(scuffle.IsOut(Bystander));
        Assert.Equal(1, scuffle.Standing(scuffle.Order));
        Assert.Equal(1, scuffle.Standing(scuffle.Chaos));
    }

    [Fact]
    public void TimeUp_AtTheLimit()
    {
        var scuffle = TwoOnOne();

        Assert.False(scuffle.TimeUp(Now + Limit - TimeSpan.FromSeconds(1)));
        Assert.True(scuffle.TimeUp(Now + Limit));
    }

    [Fact]
    public void NearPeace_KeepsAWiderRingRoundABank()
    {
        const int bankRing = TownScuffleSettings.DefaultBankClearTiles;
        const int otherRing = TownScuffleSettings.DefaultPeaceClearTiles;

        Assert.True(FactionRules.NearPeace(bankRing, isBank: true, bankRing, otherRing));
        Assert.False(FactionRules.NearPeace(bankRing + 1, isBank: true, bankRing, otherRing));
        Assert.True(FactionRules.NearPeace(otherRing, isBank: false, bankRing, otherRing));
        Assert.False(FactionRules.NearPeace(otherRing + 1, isBank: false, bankRing, otherRing));
    }

    [Fact]
    public void Settings_DefaultsAreSmallRareAndClearOfTheBankPlaza()
    {
        var settings = new TownScuffleSettings();

        Assert.True(settings.Enabled);
        Assert.True(settings.TownGapMinMinutes < settings.TownGapMaxMinutes);
        Assert.True(settings.MaxSide * 2 <= settings.MaxFighters);
        Assert.True(settings.BankClearTiles > BankPlaza.Range);
        Assert.True(settings.PeaceClearTiles >= FactionRules.SafeRadius);
        Assert.True(TimeSpan.FromSeconds(settings.TimeLimitSeconds) <= FactionRules.SkirmishLimit);
        Assert.Equal(settings.MaxActive, TownScuffleSettings.Defaults.MaxActive);
        Assert.True(settings.PairRestMinutes > settings.FighterRestMinutes);
        Assert.True(settings.GatherMinutes > 0);
        Assert.True(settings.CallTiles > settings.BankClearTiles);
    }

    [Fact]
    public void Call_DropsAFighterForGoodAndEndsOnTime()
    {
        var gather = TimeSpan.FromMinutes(TownScuffleSettings.DefaultGatherMinutes);
        var call = new TownScuffleCall(new ScuffleTown(null, Britain), new Point3D(1450, 1640, 0), [OrderOne, OrderTwo], [ChaosOne], Now + gather);

        call.Drop(OrderTwo);
        call.Drop(Bystander);

        Assert.Equal([OrderOne], call.Order);
        Assert.Equal([ChaosOne], call.Chaos);
        Assert.False(call.TimeUp(Now));
        Assert.True(call.TimeUp(Now + gather));
    }

    [Fact]
    public void Settings_ANewCharactersFileCarriesTheBlockAndAnOldOneFallsBack()
    {
        Assert.NotNull(CharactersFile.CreateDefault().TownScuffles);
        Assert.Null(new CharactersConfiguration().TownScuffles);
    }

    [Fact]
    public void Talk_ScuffleLinesHaveDefaults()
    {
        var library = TalkLibrary.FromDefaults();

        Assert.False(string.IsNullOrEmpty(library.Pick(TalkCategory.ScuffleWatch, 0, new TalkSlots { Name = "Rowan" }, EraBand.T2A)));
        Assert.False(string.IsNullOrEmpty(library.Pick(TalkCategory.ScuffleYield, 0, default, EraBand.T2A)));
        Assert.False(string.IsNullOrEmpty(library.Pick(TalkCategory.ScuffleCall, 0, new TalkSlots { Place = Britain }, EraBand.T2A)));
    }
}
