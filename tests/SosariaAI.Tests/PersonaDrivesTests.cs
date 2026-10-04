using System.Collections.Generic;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class PersonaDrivesTests
{
    [Fact]
    public void From_NullOrEmpty_IsNeutral()
    {
        Assert.False(PersonaDrives.From(null).IsCustom);
        Assert.Equal(PersonaDrives.NeutralValue, PersonaDrives.From(null).Greed);
        Assert.Equal(PersonaDrives.NeutralValue, PersonaDrives.From(new Dictionary<string, double>()).Caution);
        Assert.False(Persona.CreateNeutral().ResolvedDrives().IsCustom);
    }

    [Fact]
    public void From_ReadsKeysAndClamps()
    {
        var drives = PersonaDrives.From(
            new Dictionary<string, double>
            {
                ["greed"] = 0.7,
                ["caution"] = 2,
                ["valor"] = -1
            }
        );

        Assert.True(drives.IsCustom);
        Assert.Equal(0.7, drives.Greed);
        Assert.Equal(PersonaDrives.MaxValue, drives.Caution);
        Assert.Equal(PersonaDrives.MinValue, drives.Valor);
    }

    [Fact]
    public void DefaultPersonas_HaveExpectedLeaning()
    {
        var miner = PersonaDrives.From(PersonasFile.CreateDefaultMira().Drives);
        Assert.True(miner.Greed > miner.Valor);
        Assert.True(miner.Caution > miner.Valor);

        var veteran = PersonaDrives.From(PersonasFile.CreateDefaultBran().Drives);
        Assert.True(veteran.Valor > veteran.Caution);

        var novice = PersonaDrives.From(PersonasFile.CreateDefaultTam().Drives);
        Assert.True(novice.Caution > novice.Valor);
    }
}
