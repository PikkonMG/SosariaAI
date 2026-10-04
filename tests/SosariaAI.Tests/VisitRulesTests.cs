using Server;
using SosariaAI.Behaviour;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class VisitRulesTests
{
    private const uint IdleSerial = 0x8B03;

    [Fact]
    public void MayVisit_FriendInTown_Yes()
    {
        Assert.True(VisitRules.MayVisit(alive: true, isPerson: true, sameMap: true, friendUnderGuards: true));
    }

    [Fact]
    public void MayVisit_FriendHuntingOutsideTheGuards_No()
    {
        // Zenaide the miner visited Jed while he hunted in the graveyard, and had to run.
        Assert.False(VisitRules.MayVisit(alive: true, isPerson: true, sameMap: true, friendUnderGuards: false));
    }

    [Fact]
    public void MayPick_ANearSettledFriend_Only()
    {
        // Pieter set out for Hulda while she mustered a party for Deceit; she gated away.
        Assert.True(VisitRules.MayPick(mayVisit: true, VisitRules.ReachTiles, friendSettled: true));
        Assert.False(VisitRules.MayPick(mayVisit: true, VisitRules.ReachTiles + 1, friendSettled: true));
        Assert.False(VisitRules.MayPick(mayVisit: true, 0, friendSettled: false));
        Assert.False(VisitRules.MayPick(mayVisit: false, 0, friendSettled: true));
    }

    [Fact]
    public void Reached_UsesTheSquareReachOfTheWalk()
    {
        // Pieter stood two across and three down from Hulda: the walk had arrived, the round
        // distance of 3.6 had not, and he stood there five minutes.
        var pieter = new Point3D(4407, 1170, 0);

        Assert.True(VisitRules.Reached(pieter, new Point3D(4405, 1173, 0)));
        Assert.False(VisitRules.Reached(pieter, new Point3D(4405, 1174, 0)));
    }

    [Fact]
    public void IsSettled_AHumanOrACharacterStandingAbout()
    {
        TestMap.EnsureInternal();

        Assert.True(VisitSkill.IsSettled(new SosariaAI.Mobiles.SosariaCharacter((Serial)IdleSerial)));
    }

    [Fact]
    public void MayVisit_DeadOrAwayOrNotAPerson_No()
    {
        Assert.False(VisitRules.MayVisit(alive: false, isPerson: true, sameMap: true, friendUnderGuards: true));
        Assert.False(VisitRules.MayVisit(alive: true, isPerson: false, sameMap: true, friendUnderGuards: true));
        Assert.False(VisitRules.MayVisit(alive: true, isPerson: true, sameMap: false, friendUnderGuards: true));
    }
}
