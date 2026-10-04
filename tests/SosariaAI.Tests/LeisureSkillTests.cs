using System;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class LeisureSkillTests
{
    [Fact]
    public void TavernSkill_NameAndDefaults()
    {
        var skill = new TavernSkill(ShopFinder.TavernToken);
        Assert.Equal(SkillKinds.Tavern, skill.Name);
        Assert.Equal("Tavern", skill.Name);
        Assert.True(TavernSkill.Linger > TimeSpan.Zero);
        Assert.True(TavernSkill.StayChance >= IdleWanderSkill.WanderChanceToNotMove);
    }

    [Fact]
    public void VisitSkill_NameAndDefaults()
    {
        var skill = new VisitSkill(ShopFinder.TavernToken);
        Assert.Equal(SkillKinds.Visit, skill.Name);
        Assert.Equal("Visit", skill.Name);
        Assert.True(VisitSkill.VisitDuration > TimeSpan.Zero);
    }

    [Fact]
    public void SightseeSkill_NameAndDefaults()
    {
        var skill = new SightseeSkill(SkillFactory.DefaultSightseeDestination);
        Assert.Equal(SkillKinds.Sightsee, skill.Name);
        Assert.Equal("Sightsee", skill.Name);
        Assert.True(SightseeSkill.LookDuration > TimeSpan.Zero);
    }

    [Fact]
    public void PlaceStay_NotStartedBeforeArrival() =>
        Assert.False(new PlaceStay(TavernSkill.Linger, TavernSkill.StayChance, chat: null).Started);

    [Fact]
    public void LoiterSkill_NameAndDefaults()
    {
        var center = new Point3D(100, 200, 0);
        var skill = new LoiterSkill(center, CharactersFile.DefaultIdleRadius, LoiterSkill.DefaultDuration);
        Assert.Equal(SkillKinds.Loiter, skill.Name);
        Assert.Equal("Loiter", skill.Name);
        Assert.True(LoiterSkill.DefaultDuration > TimeSpan.Zero);
        Assert.True(LoiterSkill.StayChance > IdleWanderSkill.WanderChanceToNotMove);
        Assert.NotNull(new LoiterSkill(Point3D.Zero, 0, TimeSpan.Zero));
    }

    [Fact]
    public void VisitSkill_GiveUpOutcome_CountsAStartedVisit()
    {
        Assert.Equal(SkillStatus.Done, VisitSkill.GiveUpOutcome(lingered: true));
        Assert.Equal(SkillStatus.Failed, VisitSkill.GiveUpOutcome(lingered: false));
    }

    [Fact]
    public void VisitSkill_NeedsReplan_OnlyAfterALongerMove()
    {
        var dest = new Point3D(1400, 1600, 0);

        Assert.False(VisitSkill.NeedsReplan(dest, new Point3D(1400 + VisitSkill.ReplanTiles, 1600, 0)));
        Assert.True(VisitSkill.NeedsReplan(dest, new Point3D(1400 + VisitSkill.ReplanTiles + 1, 1600, 0)));
        Assert.True(VisitSkill.NeedsReplan(dest, new Point3D(1400, 1600 - VisitSkill.ReplanTiles - 1, 0)));
    }
}
