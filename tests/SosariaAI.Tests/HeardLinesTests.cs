using System;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class HeardLinesTests
{
    private const int Felucca = 0;
    private const int Trammel = 1;
    private const int X = 1432;
    private const int Y = 1696;
    private const string Line = "Anyone need iron?";

    private static readonly DateTime Start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void HeardNear_TrueForTheSameWordsInEarshot_AcrossACellEdge()
    {
        var heard = new HeardLines();
        heard.Record(Felucca, X, Y, Line, Start);

        Assert.True(heard.HeardNear(Felucca, X, Y, "anyone need iron", Start));
        Assert.True(heard.HeardNear(Felucca, X + HeardLineRules.EarshotTiles, Y - HeardLineRules.EarshotTiles, Line, Start));
        Assert.NotEqual(HeardLineRules.CellOf(X), HeardLineRules.CellOf(X + HeardLineRules.EarshotTiles));
    }

    [Fact]
    public void HeardNear_FalseOutOfEarshot_OnAnotherMap_OrForOtherWords()
    {
        var heard = new HeardLines();
        heard.Record(Felucca, X, Y, Line, Start);

        Assert.False(heard.HeardNear(Felucca, X + HeardLineRules.EarshotTiles + 1, Y, Line, Start));
        Assert.False(heard.HeardNear(Trammel, X, Y, Line, Start));
        Assert.False(heard.HeardNear(Felucca, X, Y, "anyone need logs", Start));
    }

    [Fact]
    public void HeardNear_ForgetsALineAfterTheWindow()
    {
        var heard = new HeardLines();
        heard.Record(Felucca, X, Y, Line, Start);

        Assert.True(heard.HeardNear(Felucca, X, Y, Line, Start + HeardLineRules.Window - TimeSpan.FromSeconds(1)));
        Assert.False(heard.HeardNear(Felucca, X, Y, Line, Start + HeardLineRules.Window));
    }

    [Fact]
    public void Record_KeepsACellSmall()
    {
        var heard = new HeardLines();

        for (var i = 0; i < HeardLineRules.MaxLinesPerCell + 1; i++)
        {
            heard.Record(Felucca, X, Y, $"line number {i}", Start);
        }

        Assert.False(heard.HeardNear(Felucca, X, Y, "line number 0", Start));
        Assert.True(heard.HeardNear(Felucca, X, Y, "line number 1", Start));
    }

    [Fact]
    public void CellOf_FloorsNegativeCoordinates()
    {
        Assert.Equal(0, HeardLineRules.CellOf(HeardLineRules.CellSize - 1));
        Assert.Equal(1, HeardLineRules.CellOf(HeardLineRules.CellSize));
        Assert.Equal(-1, HeardLineRules.CellOf(-1));
    }
}
