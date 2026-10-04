using System.Collections.Generic;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A gatherer stops before the engine taxes its steps, and an overloaded person sheds only
/// what takes it back to its limit, heaviest pieces first.
/// </summary>
public class CarryRulesTests
{
    // A T2A body of 50 strength: 40 + 3.5 * 50 (PlayerMobile.MaxWeight).
    private const int Limit = 215;
    private const double FillFraction = 0.8;
    private const int FillLine = 172;
    private const int NoSwingYet = 0;
    private const int FeluccaLogSwing = 40;
    private const double LogWeight = 2.0;
    private const double OreWeight = 12.0;
    private const double IngotWeight = 0.1;
    private const int LogStack = 170;

    [Fact]
    public void StopsWork_AtTheFillLineOfTheBodyLimit()
    {
        Assert.False(CarryRules.StopsWork(FillLine - 1, Limit, FillFraction, NoSwingYet));
        Assert.True(CarryRules.StopsWork(FillLine, Limit, FillFraction, NoSwingYet));
    }

    [Fact]
    public void StopsWork_WhenOneMoreSwingWouldPassTheLimit()
    {
        // Conan's backpack cap was 400 stones; its body carried half that.
        var belowLine = FillLine - 1;

        Assert.True(CarryRules.StopsWork(belowLine, Limit, FillFraction, Limit - belowLine + 1));
        Assert.False(CarryRules.StopsWork(belowLine, Limit, FillFraction, Limit - belowLine));
        Assert.True(CarryRules.StopsWork(Limit - FeluccaLogSwing + 1, Limit, fillFraction: 1.0, FeluccaLogSwing));
    }

    [Fact]
    public void StopsWork_IgnoresAWeightLoss()
    {
        const int beastTookTheLoad = -100;

        Assert.False(CarryRules.StopsWork(FillLine - 1, Limit, FillFraction, beastTookTheLoad));
    }

    [Theory]
    [InlineData(Limit - 1, 0)]
    [InlineData(Limit, 0)]
    [InlineData(Limit + 1, 1)]
    [InlineData(371, 156)]
    public void SurplusStones_AreWhatLiesAboveTheLimit(int carried, int expected) =>
        Assert.Equal(expected, CarryRules.SurplusStones(carried, Limit));

    [Theory]
    [InlineData(0, Limit, 0.9, 193)]
    [InlineData(150, Limit, 0.9, 43)]
    [InlineData(300, Limit, 0.9, 0)]
    public void RoomStones_FitUnderTheFillLine(int carried, int limit, double fraction, int expected) =>
        Assert.Equal(expected, CarryRules.RoomStones(carried, limit, fraction));

    [Fact]
    public void UnitsToShed_RoundUpToWholePieces_AndStayInsideTheStack()
    {
        Assert.Equal(78, CarryRules.UnitsToShed(156, LogWeight, LogStack));
        Assert.Equal(79, CarryRules.UnitsToShed(157, LogWeight, LogStack));
        Assert.Equal(2, CarryRules.UnitsToShed(13, OreWeight, 30));
        Assert.Equal(LogStack, CarryRules.UnitsToShed(1000, LogWeight, LogStack));
    }

    [Theory]
    [InlineData(0, LogWeight, LogStack)]
    [InlineData(10, 0.0, LogStack)]
    [InlineData(10, LogWeight, 0)]
    public void UnitsToShed_NothingWithoutSurplusWeightOrPieces(int surplus, double unitWeight, int amount) =>
        Assert.Equal(0, CarryRules.UnitsToShed(surplus, unitWeight, amount));

    [Fact]
    public void HeaviestFirst_PutsOreBeforeLogsBeforeIngots()
    {
        var weights = new List<double> { IngotWeight, LogWeight, OreWeight };

        weights.Sort(CarryRules.HeaviestFirst);

        Assert.Equal(new[] { OreWeight, LogWeight, IngotWeight }, weights);
    }
}
