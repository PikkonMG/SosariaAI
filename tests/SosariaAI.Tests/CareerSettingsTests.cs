using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class CareerSettingsTests
{
    [Fact]
    public void EffectiveGoldReserve_HonorsTheTestFlag()
    {
        var career = new CareerSettings { IgnorePriceLimits = true };
        Assert.Equal(CareerSettings.NoGoldReserve, career.EffectiveGoldReserve());

        var strict = new CareerSettings { GoldReserve = 150 };
        Assert.Equal(150, strict.EffectiveGoldReserve());

        // An unset or zero reserve still falls back to the default.
        var unset = new CareerSettings { GoldReserve = 0 };
        Assert.Equal(CareerSettings.DefaultGoldReserve, unset.EffectiveGoldReserve());
    }

    [Fact]
    public void FleeThreshold_TakesTheOperatorValue_AndFallsBackWhenItIsUnsetOrNotPositive()
    {
        const int operatorThreshold = 65;

        Assert.Equal(operatorThreshold, CareerSettings.FleeThreshold(new CareerSettings { MinThreatToFlee = operatorThreshold }));
        Assert.Equal(CareerSettings.DefaultMinThreatToFlee, CareerSettings.FleeThreshold(null));
        Assert.Equal(CareerSettings.DefaultMinThreatToFlee, CareerSettings.FleeThreshold(new CareerSettings { MinThreatToFlee = 0 }));
        Assert.Equal(CareerSettings.DefaultMinThreatToFlee, CareerSettings.FleeThreshold(new CareerSettings { MinThreatToFlee = -1 }));
    }
}
