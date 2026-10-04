using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Skills;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class DungeonCrawlRulesTests
{
    private const int BaseMinutes = 20;
    private const int SeedsToTry = 400;
    private static readonly Point3D Here = new(5200, 900, 0);
    private static readonly bool[] NoPrey = [];
    private static readonly FloorWays Down = new(Down: true, DownBarred: false, Up: false);
    private static readonly FloorWays NoStairs = new(Down: false, DownBarred: false, Up: false);

    [Fact]
    public void DescendWeight_GrowsWithTheTier()
    {
        Assert.Equal((int)SkillTier.Grandmaster + 1, DungeonCrawlRules.DescendWeightByTier.Length);

        for (var tier = SkillTier.Novice; tier < SkillTier.Grandmaster; tier++)
        {
            Assert.True(DungeonCrawlRules.DescendWeight(tier + 1) > DungeonCrawlRules.DescendWeight(tier));
        }

        Assert.True(DungeonCrawlRules.DescendWeight(SkillTier.Novice) < DungeonCrawlRules.RoomWeight);
        Assert.True(DungeonCrawlRules.DescendWeight(SkillTier.Grandmaster) > DungeonCrawlRules.RoomWeight);
    }

    [Fact]
    public void NextStop_GrandmastersGoDownMoreOftenThanNovices()
    {
        Point3D[] rooms = [new(5210, 900, 0), new(5200, 915, 0)];
        var masters = 0;
        var novices = 0;

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            masters += DungeonCrawlRules.NextStop(Here, rooms, [], NoPrey, Down, false, SkillTier.Grandmaster, seed) == DungeonCrawlRules.Descend ? 1 : 0;
            novices += DungeonCrawlRules.NextStop(Here, rooms, [], NoPrey, Down, false, SkillTier.Novice, seed) == DungeonCrawlRules.Descend ? 1 : 0;
        }

        Assert.True(masters > novices * 4);
    }

    [Fact]
    public void NextStop_NeverDownWhenTheFloorIsTheDeepest()
    {
        Point3D[] rooms = [new(5210, 900, 0)];

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            Assert.NotEqual(DungeonCrawlRules.Descend, DungeonCrawlRules.NextStop(Here, rooms, [], NoPrey, NoStairs, false, SkillTier.Grandmaster, seed));
        }
    }

    [Fact]
    public void NextStop_FreshRoomsWin_ButASweptFloorStillRollsARoom()
    {
        Point3D[] rooms = [new(5203, 900, 0), new(5230, 900, 0)];
        Point3D[] cleared = [new(5200, 900, 0)];
        var fresh = 0;

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            fresh += DungeonCrawlRules.NextStop(Here, rooms, cleared, NoPrey, NoStairs, false, SkillTier.Novice, seed) == 1 ? 1 : 0;
            Assert.NotEqual(DungeonCrawlRules.NoStop, DungeonCrawlRules.NextStop(Here, rooms, [.. rooms], NoPrey, NoStairs, false, SkillTier.Novice, seed));
        }

        Assert.True(fresh > SeedsToTry * 3 / 4);
    }

    [Fact]
    public void NextStop_NearRoomsWeighMoreThanFarOnes()
    {
        Point3D[] rooms = [new(5210, 900, 0), new(5200 + DungeonCrawlRules.RoomMaxStep + 5, 900, 0)];
        var near = 0;

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            near += DungeonCrawlRules.NextStop(Here, rooms, [], NoPrey, NoStairs, false, SkillTier.Novice, seed) == 0 ? 1 : 0;
        }

        Assert.True(near > SeedsToTry / 2);
    }

    [Fact]
    public void NextStop_BareFloor_TakesTheStairWhenThereIsOne()
    {
        Point3D[] rooms = [new(5210, 900, 0), new(5200, 915, 0)];

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            Assert.Equal(DungeonCrawlRules.Descend, DungeonCrawlRules.NextStop(Here, rooms, [], NoPrey, Down, true, SkillTier.Novice, seed));
            Assert.NotEqual(DungeonCrawlRules.NoStop, DungeonCrawlRules.NextStop(Here, rooms, [], NoPrey, NoStairs, true, SkillTier.Novice, seed));
        }
    }

    [Fact]
    public void NextStop_TheRoomWithPreyInReachWins()
    {
        // The Wrong crawl rolled corridor points as often as the spawn rooms and never met a juka.
        Point3D[] rooms = [new(5210, 900, 0), new(5200, 915, 0), new(5190, 900, 0)];
        bool[] prey = [false, true, false];
        var toPrey = 0;

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            toPrey += DungeonCrawlRules.NextStop(Here, rooms, [], prey, NoStairs, false, SkillTier.Novice, seed) == 1 ? 1 : 0;
        }

        Assert.True(toPrey > SeedsToTry * 3 / 4);
    }

    [Fact]
    public void NextStop_AClearedRoomWithPreyAgainBeatsAFreshBareOne()
    {
        Point3D[] rooms = [new(5210, 900, 0), new(5200, 915, 0)];
        Point3D[] cleared = [rooms[1]];
        bool[] prey = [false, true];
        var respawn = 0;

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            respawn += DungeonCrawlRules.NextStop(Here, rooms, cleared, prey, NoStairs, false, SkillTier.Novice, seed) == 1 ? 1 : 0;
        }

        Assert.True(respawn > SeedsToTry / 2);
    }

    [Fact]
    public void NextStop_ABareStreak_StaysOnAFloorThatStillShowsPrey()
    {
        Point3D[] rooms = [new(5210, 900, 0), new(5200, 915, 0)];
        bool[] prey = [false, true];
        var down = 0;

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            down += DungeonCrawlRules.NextStop(Here, rooms, [], prey, Down, true, SkillTier.Novice, seed) == DungeonCrawlRules.Descend ? 1 : 0;
        }

        Assert.True(down < SeedsToTry / 4);
    }

    [Fact]
    public void RoomPreyRange_IsWhatTheRoomHuntWouldSee() =>
        Assert.Equal(DungeonCrawlRules.RoomRadius + HuntSkill.ReachSlack, DungeonCrawlRules.RoomPreyRange);

    [Fact]
    public void CountBareRooms_CountsOnlyEmptyRoomsWithNoKillInARow()
    {
        const int NoKills = 0;
        const int OneKill = 1;
        var bare = DungeonCrawlRules.CountBareRooms(0, HuntEndReason.Empty, NoKills);
        Assert.False(DungeonCrawlRules.FloorIsBare(bare));

        bare = DungeonCrawlRules.CountBareRooms(bare, HuntEndReason.Empty, NoKills);
        Assert.True(DungeonCrawlRules.FloorIsBare(bare));

        Assert.Equal(0, DungeonCrawlRules.CountBareRooms(bare, HuntEndReason.Empty, OneKill));
        Assert.Equal(0, DungeonCrawlRules.CountBareRooms(bare, HuntEndReason.TimeUp, NoKills));
    }

    [Fact]
    public void RoomEmptyLimit_IsAGlanceNotTheWholeRoomClock() =>
        Assert.True(DungeonCrawlRules.RoomEmptyLimit < TimeSpan.FromMinutes(DungeonCrawlRules.RoomMinutes));

    [Fact]
    public void NextStop_NoRooms_TakesTheStairOrNothing()
    {
        Assert.Equal(DungeonCrawlRules.Descend, DungeonCrawlRules.NextStop(Here, [], [], NoPrey, Down, false, SkillTier.Novice, seed: 1));
        Assert.Equal(DungeonCrawlRules.NoStop, DungeonCrawlRules.NextStop(Here, [], [], NoPrey, NoStairs, false, SkillTier.Novice, seed: 1));
        Assert.Equal(DungeonCrawlRules.NoStop, DungeonCrawlRules.NextStop(Here, null, null, null, NoStairs, false, SkillTier.Novice, seed: 1));
    }

    [Fact]
    public void CampChance_HomebodiesAndTheCautiousCampRestlessOnesRoam()
    {
        var plain = DungeonCrawlRules.CampChance(PersonTrait.None);

        Assert.True(DungeonCrawlRules.CampChance(PersonTrait.Homebody | PersonTrait.Cautious) > plain);
        Assert.True(DungeonCrawlRules.CampChance(PersonTrait.Restless) < plain);
    }

    [Fact]
    public void RunLength_RestlessShorterHomebodyLonger()
    {
        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            var plain = DungeonCrawlRules.RunLength(PersonTrait.None, seed, camper: false);

            Assert.True(DungeonCrawlRules.RunLength(PersonTrait.Restless, seed, camper: false) < plain);
            Assert.True(DungeonCrawlRules.RunLength(PersonTrait.Homebody, seed, camper: false) > plain);
        }
    }

    [Fact]
    public void RunLength_ALongSession_AndACamperStaysTwiceAsLong()
    {
        var shortest = TimeSpan.MaxValue;
        var longest = TimeSpan.Zero;

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            var run = DungeonCrawlRules.RunLength(PersonTrait.None, seed, camper: false);
            shortest = run < shortest ? run : shortest;
            longest = run > longest ? run : longest;

            var camped = DungeonCrawlRules.RunLength(PersonTrait.None, seed, camper: true);
            Assert.True(Math.Abs((camped - run * DungeonCrawlRules.CamperRuns).TotalMilliseconds) < 1);
        }

        Assert.True(shortest >= TimeSpan.FromMinutes(DungeonCrawlRules.RunMinMinutes));
        Assert.True(longest <= TimeSpan.FromMinutes(DungeonCrawlRules.RunMaxMinutes));
        Assert.True(longest - shortest > TimeSpan.FromMinutes(BaseMinutes / 2));
    }

    [Fact]
    public void LeaveReason_NamesTheTimerWoundsSuppliesAFullPackOrAClosedFloor()
    {
        var run = TimeSpan.FromMinutes(BaseMinutes);
        var early = TimeSpan.FromMinutes(1);

        Assert.Null(DungeonCrawlRules.LeaveReason(early, run, 1, suppliesLow: false, packFull: false, roomFailures: 0));
        Assert.Equal(
            DungeonCrawlRules.RunDoneWhy,
            DungeonCrawlRules.LeaveReason(run, run, 1, suppliesLow: false, packFull: false, roomFailures: 0)
        );
        Assert.Equal(
            DungeonCrawlRules.WoundsWhy,
            DungeonCrawlRules.LeaveReason(early, run, DungeonCrawlRules.LeaveHitsFraction / 2, false, false, 0)
        );
        Assert.Equal(
            DungeonCrawlRules.SuppliesWhy,
            DungeonCrawlRules.LeaveReason(early, run, 1, suppliesLow: true, packFull: false, roomFailures: 0)
        );
        Assert.Equal(
            DungeonCrawlRules.PackFullWhy,
            DungeonCrawlRules.LeaveReason(early, run, 1, suppliesLow: false, packFull: true, roomFailures: 0)
        );
        Assert.Equal(
            DungeonCrawlRules.RoomsFailedWhy,
            DungeonCrawlRules.LeaveReason(early, run, 1, false, false, DungeonCrawlRules.MaxRoomFailures)
        );
    }

    [Fact]
    public void LeaveReason_ACrawlBegunLow_StaysUntilItsWoundsOrItsTime()
    {
        var run = TimeSpan.FromMinutes(BaseMinutes);
        var early = TimeSpan.FromMinutes(1);
        var stillLow = HuntEndDecision.RanLowOnRun(lowAtStart: true, lowNow: true);

        Assert.Null(DungeonCrawlRules.LeaveReason(early, run, 1, stillLow, packFull: false, roomFailures: 0));
        Assert.Equal(
            DungeonCrawlRules.WoundsWhy,
            DungeonCrawlRules.LeaveReason(early, run, DungeonCrawlRules.LeaveHitsFraction / 2, stillLow, false, 0)
        );
        Assert.Equal(DungeonCrawlRules.RunDoneWhy, DungeonCrawlRules.LeaveReason(run, run, 1, stillLow, false, 0));
    }

    [Fact]
    public void LeaveReason_ACrawlThatRanLowInside_LeavesForSupplies() =>
        Assert.Equal(
            DungeonCrawlRules.SuppliesWhy,
            DungeonCrawlRules.LeaveReason(
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(BaseMinutes),
                1,
                HuntEndDecision.RanLowOnRun(lowAtStart: false, lowNow: true),
                packFull: false,
                roomFailures: 0
            )
        );

    [Fact]
    public void CrawlFailed_OnlyWhenNoRoomWasReachedAndTheRoomsClosedTheRun()
    {
        Assert.True(DungeonCrawlRules.CrawlFailed(roomsReached: 0, DungeonCrawlRules.RoomsFailedWhy));
        Assert.False(DungeonCrawlRules.CrawlFailed(roomsReached: 1, DungeonCrawlRules.RoomsFailedWhy));
        Assert.False(DungeonCrawlRules.CrawlFailed(roomsReached: 0, DungeonCrawlRules.WoundsWhy));
        Assert.False(DungeonCrawlRules.CrawlFailed(roomsReached: 0, DungeonCrawlRules.FloorEmptyWhy));
    }

    [Fact]
    public void LeaveLine_NamesTheDungeonTheMinutesAndWhy() =>
        Assert.Equal(
            "Iolo leaves Despise after 23 minutes: the run is over",
            DungeonCrawlRules.LeaveLine("Iolo", "Despise", TimeSpan.FromMinutes(23.7), DungeonCrawlRules.RunDoneWhy)
        );

    [Fact]
    public void RoomArea_IsASquareRoundTheRoom()
    {
        var area = DungeonCrawlRules.RoomArea(Here);

        Assert.True(area.Contains(Here));
        Assert.Equal(DungeonCrawlRules.RoomRadius * 2 + 1, area.Width);
        Assert.Equal(DungeonCrawlRules.RoomRadius * 2 + 1, area.Height);
    }

    [Theory]
    [InlineData(HuntEndReason.TimeUp, true)]
    [InlineData(HuntEndReason.Empty, false)]
    [InlineData(HuntEndReason.Hurt, false)]
    [InlineData(HuntEndReason.PackFull, false)]
    [InlineData(HuntEndReason.SuppliesLow, false)]
    public void CampsOn_OnlyAHallThatStillHadPrey(HuntEndReason reason, bool camps) =>
        Assert.Equal(camps, DungeonCrawlRules.CampsOn(reason));

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void CampsAtHall_OnlyOnTheHallsOwnFloor(bool campRoll, bool hallOnThisFloor, bool camps) =>
        Assert.Equal(camps, DungeonCrawlRules.CampsAtHall(campRoll, hallOnThisFloor));

    [Fact]
    public void NextStop_ABareFloorWhoseStairsDownLeadTooDeep_GoesUpOrOut()
    {
        Point3D[] rooms = [new(5210, 900, 0), new(5200, 915, 0)];

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            Assert.Equal(DungeonCrawlRules.Ascend, DungeonCrawlRules.NextStop(Here, rooms, [], NoPrey, TooDeepUp, true, SkillTier.Grandmaster, seed));
            Assert.Equal(DungeonCrawlRules.NoStop, DungeonCrawlRules.NextStop(Here, rooms, [], NoPrey, TooDeep, true, SkillTier.Grandmaster, seed));
        }
    }

    [Fact]
    public void NextStop_NeverDownAStairThatLeadsTooDeep_AndARoomWithPreyStillWins()
    {
        Point3D[] rooms = [new(5210, 900, 0), new(5200, 915, 0)];
        bool[] prey = [false, true];

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            var stop = DungeonCrawlRules.NextStop(Here, rooms, [], NoPrey, TooDeepUp, false, SkillTier.Grandmaster, seed);
            Assert.True(stop >= 0, $"seed {seed} gave {stop}");
            Assert.True(DungeonCrawlRules.NextStop(Here, rooms, [], prey, TooDeepUp, true, SkillTier.Grandmaster, seed) >= 0);
        }
    }

    [Fact]
    public void FloorFits_WithinTheReach_AndAFloorWithNoSpawnFitsAnyone()
    {
        Assert.True(DungeonCrawlRules.FloorFits(HuntGround.Reach(CrawlerPower), CrawlerPower));
        Assert.False(DungeonCrawlRules.FloorFits(HuntGround.Reach(CrawlerPower) + 1, CrawlerPower));
        Assert.True(DungeonCrawlRules.FloorFits(0, CrawlerPower));
    }

    [Fact]
    public void StaysOn_AFloorALittlePastTheReach_ButNotOneAPadDroppedItOnFarPast()
    {
        var reach = HuntGround.Reach(CrawlerPower);

        Assert.True(DungeonCrawlRules.StaysOn(reach + reach / 10, CrawlerPower));
        Assert.False(DungeonCrawlRules.StaysOn(reach * 2, CrawlerPower));
    }

    [Theory]
    [InlineData(2, 0, 0, 1.0, 1.0, DungeonCrawlRules.RunsWhy)]
    [InlineData(1, 1, 0, 1.0, 1.0, DungeonCrawlRules.PetLostWhy)]
    [InlineData(0, 0, 1, 1.0, 1.0, DungeonCrawlRules.MateLostWhy)]
    [InlineData(0, 0, 0, 1.0, 0.2, DungeonCrawlRules.WoundsWhy)]
    [InlineData(0, 0, 0, 0.3, 0.2, null)]
    [InlineData(1, 0, 0, 1.0, 0.5, null)]
    public void LosingWhy_RunsAFallenPetOrMateOrWoundsTakenHere(int runs, int pets, int mates, double hitsAtEntry, double hitsNow, string why) =>
        Assert.Equal(why, DungeonCrawlRules.LosingWhy(runs, pets, mates, hitsAtEntry, hitsNow));

    [Fact]
    public void StillTooHard_ForTheRestOnly()
    {
        var marked = new DateTime(2026, 9, 29, 8, 44, 47, DateTimeKind.Utc);

        Assert.False(DungeonCrawlRules.StillTooHard(default, marked));
        Assert.True(DungeonCrawlRules.StillTooHard(marked, marked));
        Assert.True(DungeonCrawlRules.StillTooHard(marked, marked + DungeonCrawlRules.TooHardRest - TimeSpan.FromSeconds(1)));
        Assert.False(DungeonCrawlRules.StillTooHard(marked, marked + DungeonCrawlRules.TooHardRest));
    }

    [Fact]
    public void GoesUp_OnlyWithAStairThatFits_TheRunOn_AndTheWoundsBearable()
    {
        Assert.True(DungeonCrawlRules.GoesUp(upFits: true, leaveReason: null, hitsFraction: 1.0));
        Assert.False(DungeonCrawlRules.GoesUp(upFits: false, leaveReason: null, hitsFraction: 1.0));
        Assert.False(DungeonCrawlRules.GoesUp(upFits: true, DungeonCrawlRules.RunDoneWhy, hitsFraction: 1.0));
        Assert.False(DungeonCrawlRules.GoesUp(upFits: true, leaveReason: null, DungeonCrawlRules.LeaveHitsFraction / 2));
    }

    [Fact]
    public void FitLines_NameTheFloorAndThePowerAgainstIt()
    {
        Assert.Equal(
            "Robard Hawke goes down to Shame level 2 (fits power 191 vs floor 202)",
            DungeonCrawlRules.FitLine("Robard Hawke", "goes down to", "Shame", 2, 191, 202)
        );
        Assert.Equal(
            "Robard Hawke keeps off Shame level 4 (power 191 below floor 948): the floor is too hard for it",
            DungeonCrawlRules.KeepOffLine("Robard Hawke", "Shame", 4, 191, 948, DungeonCrawlRules.TooHardWhy)
        );
    }

    private const int CrawlerPower = 150;
    private static readonly FloorWays TooDeepUp = new(Down: false, DownBarred: true, Up: true);
    private static readonly FloorWays TooDeep = new(Down: false, DownBarred: true, Up: false);
}
