using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class SystemOneConfidenceTests
{
    private const int ThreeOptions = 3;
    private const int TwoOptions = 2;
    private const double Precision = 1e-9;

    [Fact]
    public void ChoiceConfidence_AnEvenSpreadIsZero() =>
        Assert.Equal(0, SystemOneApi.ChoiceConfidence(1.0 / ThreeOptions, ThreeOptions), Precision);

    [Fact]
    public void ChoiceConfidence_ACertainPickIsOne() =>
        Assert.Equal(1, SystemOneApi.ChoiceConfidence(1, ThreeOptions), Precision);

    [Fact]
    public void ChoiceConfidence_MatchesTheTypeSafeExample() =>
        // TypeSafe's docs: 90% on one of three options gives 0.85.
        Assert.Equal(0.85, SystemOneApi.ChoiceConfidence(0.9, ThreeOptions), Precision);

    [Fact]
    public void ChoiceConfidence_ABelowEvenPickClampsToZero() =>
        Assert.Equal(0, SystemOneApi.ChoiceConfidence(0.2, TwoOptions), Precision);
}
