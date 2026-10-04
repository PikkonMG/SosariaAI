using System.Collections.Generic;
using System.Linq;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class EraBuildCapsTests
{
    [Fact]
    public void FitSkills_UnderTheCap_OnlyClamps()
    {
        var fitted = EraBuildCaps.FitSkills(new Dictionary<string, double> { ["Swords"] = 120, ["Tactics"] = 80 });

        Assert.Equal(EraBuildCaps.SkillCap, fitted["Swords"]);
        Assert.Equal(80, fitted["Tactics"]);
    }

    [Fact]
    public void FitSkills_OverTheCap_ScalesEverySkillDown()
    {
        var skills = new Dictionary<string, double>();

        for (var i = 0; i < 9; i++)
        {
            skills["Skill" + i] = 100;
        }

        var fitted = EraBuildCaps.FitSkills(skills);

        Assert.True(fitted.Values.Sum() <= EraBuildCaps.SkillTotalCap);
        Assert.Single(fitted.Values.Distinct());
    }

    [Fact]
    public void FitSkills_Null_IsEmpty() => Assert.Empty(EraBuildCaps.FitSkills(null));

    [Fact]
    public void FitStats_TakesTheExcessAndKeepsTheShape()
    {
        var (strength, dexterity, intelligence) = EraBuildCaps.FitStats(100, 100, 100, EraBand.T2A);

        Assert.Equal(EraBuildCaps.StatTotalCap, strength + dexterity + intelligence);
        Assert.InRange(strength - intelligence, -1, 1);
    }

    [Fact]
    public void FitStats_ClampsEachStatAndKeepsTheFloor()
    {
        var (strength, dexterity, intelligence) = EraBuildCaps.FitStats(150, 0, 90, EraBand.T2A);

        Assert.True(strength <= EraBuildCaps.StatCap);
        Assert.Equal(EraBuildCaps.MinStat, dexterity);
        Assert.True(strength + dexterity + intelligence <= EraBuildCaps.StatTotalCap);
    }

    [Fact]
    public void FitStats_LegalLine_IsUntouched() =>
        Assert.Equal((100, 100, 25), EraBuildCaps.FitStats(100, 100, 25, EraBand.T2A));

    [Theory]
    [InlineData(EraBand.T2A, EraBuildCaps.StatCap)]
    [InlineData(EraBand.ML, EraBuildCaps.EngineStatCap)]
    [InlineData(EraBand.Modern, EraBuildCaps.EngineStatCap)]
    public void StatCap_IsTheSecondAgeHundred_ThenTheEngineCeiling(EraBand band, int cap) =>
        Assert.Equal(cap, EraBuildCaps.StatCapFor(band));

    [Fact]
    public void FitStats_LaterEras_LetOneStatPassAHundred()
    {
        const int Strength = 120;
        const int Dexterity = 80;
        const int Intelligence = 25;

        Assert.Equal((Strength, Dexterity, Intelligence), EraBuildCaps.FitStats(Strength, Dexterity, Intelligence, EraBand.ML));
        Assert.Equal(EraBuildCaps.StatCap, EraBuildCaps.FitStats(Strength, Dexterity, Intelligence, EraBand.T2A).Strength);
        Assert.Equal(EraBuildCaps.EngineStatCap, EraBuildCaps.FitStats(150, 50, 25, EraBand.Modern).Strength);
    }
}
