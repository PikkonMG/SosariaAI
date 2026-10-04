using Server;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class EraBandTests
{
    [Theory]
    [InlineData(Expansion.None, EraBand.T2A)]
    [InlineData(Expansion.T2A, EraBand.T2A)]
    [InlineData(Expansion.LBR, EraBand.T2A)]
    [InlineData(Expansion.AOS, EraBand.ML)]
    [InlineData(Expansion.ML, EraBand.ML)]
    [InlineData(Expansion.SA, EraBand.Modern)]
    [InlineData(Expansion.EJ, EraBand.Modern)]
    public void Of_MapsTheExpansionToItsBand(Expansion expansion, EraBand band) =>
        Assert.Equal(band, EraBands.Of(expansion));

    [Fact]
    public void Fits_UntaggedFitsEveryEraAndTagsLimitIt()
    {
        Assert.True(EraBands.Fits(null, EraBand.T2A));
        Assert.True(EraBands.Fits([], EraBand.Modern));
        Assert.True(EraBands.Fits(["ml", "modern"], EraBand.ML));
        Assert.False(EraBands.Fits(["ml", "modern"], EraBand.T2A));
        Assert.True(EraBands.Fits(["T2A"], EraBand.T2A));
    }
}
