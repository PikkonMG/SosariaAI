using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class MeetingRulesTests
{
    [Fact]
    public void InBankQuiet_CoversTheBritainBankStreet()
    {
        Assert.True(MeetingRules.InBankQuiet(CharactersFile.DefaultBankSpot, CharactersFile.DefaultBankSpot));
        Assert.True(MeetingRules.InBankQuiet(new Point3D(1436, 1696, 0), CharactersFile.DefaultBankSpot));
        Assert.False(MeetingRules.InBankQuiet(new Point3D(1507, 1579, 20), CharactersFile.DefaultBankSpot));
    }

    [Fact]
    public void MayGreetCrowd_OpenStreet_Always()
    {
        Assert.True(MeetingRules.MayGreetCrowd(nearbyCount: 1, atQuiet: false, roll100: 99));
        Assert.True(MeetingRules.MayGreetCrowd(nearbyCount: 2, atQuiet: false, roll100: 99));
    }

    [Fact]
    public void MayGreetCrowd_PackedStreet_UsesChance()
    {
        Assert.True(MeetingRules.MayGreetCrowd(
            nearbyCount: MeetingRules.CrowdQuietCount,
            atQuiet: false,
            roll100: MeetingRules.CrowdGreetChancePercent - 1));
        Assert.False(MeetingRules.MayGreetCrowd(
            nearbyCount: MeetingRules.CrowdQuietCount,
            atQuiet: false,
            roll100: MeetingRules.CrowdGreetChancePercent));
    }

    [Fact]
    public void MayGreetCrowd_SocialHub_UsesTheQuietChance()
    {
        Assert.True(MeetingRules.MayGreetCrowd(
            nearbyCount: 1,
            atQuiet: true,
            roll100: MeetingRules.BankGreetChancePercent - 1));
        Assert.False(MeetingRules.MayGreetCrowd(
            nearbyCount: 1,
            atQuiet: true,
            roll100: MeetingRules.BankGreetChancePercent));
        Assert.False(MeetingRules.MayGreetCrowd(nearbyCount: 12, atQuiet: true, roll100: 99));
    }

    [Fact]
    public void InQuietPlace_CoversAMoonPad()
    {
        Assert.True(MeetingRules.NearPad(WorkSites.MinocGate, WorkSites.MinocGate));
        Assert.True(
            MeetingRules.InQuietPlace(
                WorkSites.BritainGate,
                CharactersFile.DefaultBankSpot,
                [WorkSites.BritainGate, WorkSites.MinocGate]
            )
        );
        Assert.False(MeetingRules.SameWornName("Una", "Elsa"));
        Assert.True(MeetingRules.SameWornName("Una", "una"));
    }
}
