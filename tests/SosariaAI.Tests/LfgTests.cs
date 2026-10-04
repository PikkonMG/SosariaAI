using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Social;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class LfgTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0);

    static LfgTests() => Timer.Init(0);

    public LfgTests() => TestMap.EnsureInternal();

    [Theory]
    [InlineData(0, 3)]
    [InlineData(149, 3)]
    [InlineData(150, 4)]
    [InlineData(300, 5)]
    [InlineData(5000, 5)]
    public void GroupSizeFor_ScalesWithThePopulation(int population, int size)
    {
        Assert.Equal(size, PartyScale.GroupSizeFor(population));
    }

    [Fact]
    public void MayShout_NeedsAnArmedFreeFighterStillAtAMeetingSpot()
    {
        Assert.True(LfgRules.MayShout(fighter: true, armed: true, inParty: false, outlaw: false, healthy: true, atMeetingSpot: true));
        Assert.False(LfgRules.MayShout(fighter: false, armed: true, inParty: false, outlaw: false, healthy: true, atMeetingSpot: true));
        Assert.False(LfgRules.MayShout(fighter: true, armed: false, inParty: false, outlaw: false, healthy: true, atMeetingSpot: true));
        Assert.False(LfgRules.MayShout(fighter: true, armed: true, inParty: true, outlaw: false, healthy: true, atMeetingSpot: true));
        Assert.False(LfgRules.MayShout(fighter: true, armed: true, inParty: false, outlaw: true, healthy: true, atMeetingSpot: true));
        Assert.False(LfgRules.MayShout(fighter: true, armed: true, inParty: false, outlaw: false, healthy: false, atMeetingSpot: true));
        Assert.False(LfgRules.MayShout(fighter: true, armed: true, inParty: false, outlaw: false, healthy: true, atMeetingSpot: false));
    }

    [Fact]
    public void RollsShout_DungeonsCallMoreOftenThanHunts()
    {
        Assert.True(LfgRules.RollsShout(dungeon: true, LfgRules.DungeonShoutPercent - 1));
        Assert.False(LfgRules.RollsShout(dungeon: true, LfgRules.DungeonShoutPercent));
        Assert.True(LfgRules.RollsShout(dungeon: false, LfgRules.HuntShoutPercent - 1));
        Assert.False(LfgRules.RollsShout(dungeon: false, LfgRules.HuntShoutPercent));
        Assert.False(LfgRules.RollsShout(dungeon: true, -1));
    }

    [Theory]
    [InlineData(92, 100, true)]
    [InlineData(91, 100, false)]
    [InlineData(46, 50, true)]
    [InlineData(45, 50, false)]
    [InlineData(0, 100, false)]
    [InlineData(60, 0, false)]
    public void StrongHunt_IsAGroundNearTheTopOfTheHuntersReach(int difficulty, int power, bool strong)
    {
        Assert.Equal(strong, LfgRules.StrongHunt(difficulty, power));
    }

    [Fact]
    public void NextStep_FillsWaitsForThreeThenForTwoThenGivesUp()
    {
        var window = LfgRules.RecruitWindow;
        var shortHanded = window + LfgRules.ShortHandedWait;
        const int fullSize = 4;

        Assert.Equal(LfgCallStep.Muster, LfgRules.NextStep(fullSize - 1, fullSize, TimeSpan.Zero, window));
        Assert.Equal(LfgCallStep.Recruit, LfgRules.NextStep(LfgRules.MinJoiners, fullSize, window - TimeSpan.FromSeconds(1), window));
        Assert.Equal(LfgCallStep.Muster, LfgRules.NextStep(LfgRules.MinJoiners, fullSize, window, window));
        Assert.Equal(LfgCallStep.Recruit, LfgRules.NextStep(LfgRules.PairJoiners, fullSize, window, window));
        Assert.Equal(LfgCallStep.Muster, LfgRules.NextStep(LfgRules.PairJoiners, fullSize, shortHanded, window));
        Assert.Equal(LfgCallStep.Recruit, LfgRules.NextStep(0, fullSize, window, window));
        Assert.Equal(LfgCallStep.GiveUp, LfgRules.NextStep(0, fullSize, shortHanded, window));
    }

    [Fact]
    public void Mustered_WhenEveryoneStandsCloseOrTheWaitIsOver()
    {
        Assert.True(LfgRules.Mustered(LfgRules.MusterRange, TimeSpan.Zero));
        Assert.False(LfgRules.Mustered(LfgRules.MusterRange + 1, LfgRules.MusterWait - TimeSpan.FromSeconds(1)));
        Assert.True(LfgRules.Mustered(LfgRules.MusterRange + 1, LfgRules.MusterWait));
    }

    [Fact]
    public void HoldForCrew_OnlyForSomeoneFarAndNeverLong()
    {
        Assert.False(LfgRules.HoldForCrew(LfgRules.CrewRange, TimeSpan.Zero));
        Assert.True(LfgRules.HoldForCrew(LfgRules.CrewRange + 1, TimeSpan.Zero));
        Assert.False(LfgRules.HoldForCrew(LfgRules.CrewRange + 1, LfgRules.CrewHoldLimit));
    }

    [Fact]
    public void TripOverAndTakeOver_WaitTheirGrace()
    {
        Assert.False(LfgRules.TripOver(default, Now));
        Assert.False(LfgRules.TripOver(Now, Now + LfgRules.TripGrace - TimeSpan.FromSeconds(1)));
        Assert.True(LfgRules.TripOver(Now, Now + LfgRules.TripGrace));
        Assert.False(LfgRules.TakeOverDue(default, Now));
        Assert.False(LfgRules.TakeOverDue(Now, Now + LfgRules.TakeOverWait - TimeSpan.FromSeconds(1)));
        Assert.True(LfgRules.TakeOverDue(Now, Now + LfgRules.TakeOverWait));
    }

    [Fact]
    public void FreeToJoin_IdleOrJustSettingOutFromTown()
    {
        Assert.True(LfgRules.FreeToJoin(SkillKinds.Loiter, ownTripStartingInTown: false));
        Assert.True(LfgRules.FreeToJoin(SkillKinds.Dungeon, ownTripStartingInTown: true));
        Assert.False(LfgRules.FreeToJoin(SkillKinds.Dungeon, ownTripStartingInTown: false));
        Assert.False(LfgRules.FreeToJoin(SkillKinds.BankDeposit, ownTripStartingInTown: false));
    }

    [Theory]
    [InlineData("GiantSpider 960-910", "GiantSpider", null, "giant spiders")]
    [InlineData("Lizardman 5503-529", "Lizardman", null, "lizardmen")]
    [InlineData("Harpy 100-200", "Harpy", null, "harpies")]
    [InlineData("Lich 100-200", "Lich", null, "liches")]
    [InlineData("Britain Cemetery", "Graveyard", null, "britain cemetery")]
    [InlineData("GiantRat 5316-1332", "GiantRat", "Despise", "despise")]
    [InlineData(null, "Orc", null, null)]
    public void HuntWord_SaysTheGroundTheWayPeopleDo(string ground, string prey, string dungeon, string word)
    {
        Assert.Equal(word, LfgRules.HuntWord(ground, prey, dungeon));
    }

    [Fact]
    public void DungeonWord_IsLowerCase()
    {
        Assert.Equal("terathan keep", LfgRules.DungeonWord(" Terathan Keep "));
        Assert.Null(LfgRules.DungeonWord(" "));
    }

    /// <summary>
    /// A busy bank: eight fitting fighters within earshot, two of them friends. Played a
    /// second at a time through the answer and step rules with a fixed roll order, the
    /// call must set out as a real party of three to five inside its wait.
    /// </summary>
    [Fact]
    public void Simulation_ACallAtABusyBankSetsOutAsAPartyOfThreeToFive()
    {
        const int listeners = 8;
        const int friends = 2;
        const int busyShard = 900;
        const int rollSeed = 7;
        var size = PartyScale.GroupSizeFor(busyShard);
        var rolls = new Random(rollSeed);
        var answered = new bool[listeners];
        var joined = 0;
        var step = LfgCallStep.Recruit;
        var since = TimeSpan.Zero;

        while (step == LfgCallStep.Recruit)
        {
            for (var i = 0; i < listeners && !LfgRules.Full(joined, size); i++)
            {
                var friend = i < friends;

                if (answered[i] || LfgRules.StrangerMustWait(friend, since))
                {
                    continue;
                }

                answered[i] = true;
                joined += LfgRules.MayAnswer(friend, rolls.Next(LfgRules.PercentScale)) ? 1 : 0;
            }

            step = LfgRules.NextStep(joined, size, since, LfgRules.RecruitWindow);
            since += TimeSpan.FromSeconds(1);
        }

        Assert.Equal(LfgCallStep.Muster, step);
        Assert.InRange(joined + 1, PartyScale.MinPartySize, PartyScale.MaxPartySize);
        Assert.True(since <= LfgRules.RecruitWindow + LfgRules.ShortHandedWait);
    }

    [Fact]
    public void FriendsFirst_StrangersWaitAndAnswerLess()
    {
        Assert.False(LfgRules.StrangerMustWait(friend: true, TimeSpan.Zero));
        Assert.True(LfgRules.StrangerMustWait(friend: false, LfgRules.StrangerWait - TimeSpan.FromSeconds(1)));
        Assert.False(LfgRules.StrangerMustWait(friend: false, LfgRules.StrangerWait));
        Assert.True(LfgRules.MayAnswer(friend: true, LfgRules.StrangerAnswerPercent));
        Assert.False(LfgRules.MayAnswer(friend: false, LfgRules.StrangerAnswerPercent));
    }

    [Fact]
    public void Full_CountsTheLeader()
    {
        Assert.False(LfgRules.Full(1, 3));
        Assert.True(LfgRules.Full(2, 3));
    }

    [Fact]
    public void Trip_CountsAfterMinAndEndsAtMax()
    {
        Assert.False(LfgRules.TripCounts(default, Now));
        Assert.False(LfgRules.TripCounts(Now, Now + LfgRules.MinTrip - TimeSpan.FromSeconds(1)));
        Assert.True(LfgRules.TripCounts(Now, Now + LfgRules.MinTrip));
        Assert.False(LfgRules.TripOverdue(default, Now));
        Assert.True(LfgRules.TripOverdue(Now, Now + LfgRules.MaxTrip));
    }

    [Fact]
    public void CallAbandoned_OnlyWhileRecruitingPastEveryWait()
    {
        var window = LfgRules.RecruitWindow;
        var everyWait = window + LfgRules.ShortHandedWait + LfgRules.MusterWait + LfgRules.PruneGap;
        Assert.False(LfgRules.CallAbandoned(false, Now, window, Now + everyWait - TimeSpan.FromSeconds(1)));
        Assert.True(LfgRules.CallAbandoned(false, Now, window, Now + everyWait));
        Assert.False(LfgRules.CallAbandoned(true, Now, window, Now + TimeSpan.FromHours(1)));
    }

    [Fact]
    public void Interruptible_NeverAFightOrAnErrand()
    {
        Assert.True(LfgRules.Interruptible(null));
        Assert.True(LfgRules.Interruptible(SkillKinds.Loiter));
        Assert.True(LfgRules.Interruptible(SkillKinds.GoTo));
        Assert.False(LfgRules.Interruptible(SkillKinds.Hunt));
        Assert.False(LfgRules.Interruptible(SkillKinds.Dungeon));
        Assert.False(LfgRules.Interruptible(SkillKinds.BankDeposit));
    }

    [Fact]
    public void ShoutLine_FillsPlaceAndCount()
    {
        var shout = new TalkSlots { Place = "despise", Count = LfgRules.Needed(3) };
        Assert.Equal("lfg despise anyone?", Talk.Line(TalkCategory.LfgShout, 0, shout));
        Assert.Equal("lfm despise, need 2", Talk.Line(TalkCategory.LfgShout, 1, shout));
        Assert.Equal("Mira inv", Talk.Line(TalkCategory.LfgInvite, 0, new TalkSlots { Name = "Mira" }));
        Assert.Equal(
            "Mira wanna come despise?",
            Talk.Line(TalkCategory.LfgAskPlayer, 0, new TalkSlots { Name = "Mira", Place = "despise" })
        );
        Assert.Equal("Bran is down, follow me", Talk.Line(TalkCategory.PartyTakeLead, 0, new TalkSlots { Name = "Bran" }));
        Assert.Equal("going down", Talk.Line(TalkCategory.PartyDescend, 0, default));
    }

    [Fact]
    public void Needed_AsksForAtLeastOne()
    {
        Assert.Equal(LfgRules.MinNeeded, LfgRules.Needed(1));
        Assert.Equal(3, LfgRules.Needed(4));
    }

    [Fact]
    public void Volunteered_OnlyForThatPlayerInsideTheWindow()
    {
        var character = new SosariaCharacter((Serial)0x7B01) { Name = "Bob" };
        var player = new SosariaCharacter((Serial)0x7B02) { Name = "Mira" };
        var other = new SosariaCharacter((Serial)0x7B03) { Name = "Kerr" };

        Assert.False(LfgBoard.Volunteered(character, player));
        LfgBoard.Volunteer(character, player);
        Assert.True(LfgBoard.Volunteered(character, player));
        Assert.False(LfgBoard.Volunteered(character, other));
        Assert.True(LfgRules.StillVolunteering(Now, Now + LfgRules.VolunteerWindow));
        Assert.False(LfgRules.StillVolunteering(Now, Now + LfgRules.VolunteerWindow + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Fits_AWorkerDoesNotAnswer()
    {
        var worker = new SosariaCharacter((Serial)0x7B04) { Name = "Tam" };
        Assert.False(LfgBoard.Fits(worker));
        Assert.False(LfgBoard.Fits(null));
    }

    [Fact]
    public void InviteCharacter_UsesTheEngineInviteAndAccept()
    {
        var leader = new SosariaCharacter((Serial)0x7B05) { Name = "Bran" };
        var member = new SosariaCharacter((Serial)0x7B06) { Name = "Sela" };

        Assert.True(GameParty.InviteCharacter(leader, member));
        Assert.Same(GameParty.Of(leader), GameParty.Of(member));
        Assert.Same(leader, GameParty.Of(member).Leader);
        Assert.Same(leader, GameParty.LeaderToFollow(member));
        Assert.Empty(GameParty.Of(leader).Candidates);
    }

    [Fact]
    public void InviteCharacter_RefusesSelf()
    {
        var leader = new SosariaCharacter((Serial)0x7B07) { Name = "Bran" };
        Assert.False(GameParty.InviteCharacter(leader, leader));
        Assert.False(GameParty.InviteCharacter(null, leader));
    }

    [Theory]
    [InlineData(SkillKinds.Flee, false, RunDetour.Hold)]
    [InlineData(SkillKinds.Heal, true, RunDetour.Hold)]
    [InlineData(GhostSkill.SkillName, false, RunDetour.Hold)]
    [InlineData(SkillKinds.GoHome, true, RunDetour.End)]
    [InlineData(SkillKinds.Rest, true, RunDetour.End)]
    [InlineData(SkillKinds.GoHome, false, RunDetour.Restart)]
    [InlineData(SkillKinds.Conflict, false, RunDetour.Restart)]
    [InlineData(SkillKinds.BankCrowd, false, RunDetour.Restart)]
    [InlineData(null, false, RunDetour.Restart)]
    public void Detour_AnUnrelatedPickDoesNotEndTheRun(string skillKind, bool hurt, RunDetour detour) =>
        Assert.Equal(detour, LfgRules.Detour(skillKind, hurt));

    [Fact]
    public void HoldOver_AFleeingLeaderIsWaitedForLongerThanAStray()
    {
        Assert.True(LfgRules.HoldGrace > LfgRules.TripGrace);
        Assert.False(LfgRules.HoldOver(default, Now));
        Assert.False(LfgRules.HoldOver(Now, Now + LfgRules.TripGrace));
        Assert.True(LfgRules.HoldOver(Now, Now + LfgRules.HoldGrace));
    }

    [Fact]
    public void SpotCall_ForFightersOfSomeStanding()
    {
        Assert.False(LfgRules.MayCallFromSpot(SkillTier.Novice));
        Assert.True(LfgRules.MayCallFromSpot(LfgRules.MinSpotCallTier));
        Assert.True(LfgRules.MayCallFromSpot(SkillTier.Grandmaster));
    }

    [Fact]
    public void SpotCallGap_ShrinksWithThePeopleOnline_NeverBelowTheFloor()
    {
        const int QuietShard = 100;
        const int BusyShard = 1120;
        const int VastShard = 1_000_000;

        Assert.Equal(LfgRules.SpotCallGapMin, LfgRules.SpotCallGap(QuietShard, 0));
        Assert.Equal(LfgRules.SpotCallGapMax, LfgRules.SpotCallGap(QuietShard, 1));
        Assert.Equal(LfgRules.SpotCallGapMax, LfgRules.SpotCallGap(-1, 2));
        Assert.True(LfgRules.SpotCallGap(BusyShard, 1) < LfgRules.SpotCallGap(QuietShard, 1));
        Assert.Equal(
            LfgRules.SpotCallGapMax / (BusyShard / LfgRules.PopulationPerCallLane),
            LfgRules.SpotCallGap(BusyShard, 1)
        );
        Assert.Equal(LfgRules.MinSpotCallGap, LfgRules.SpotCallGap(VastShard, 0));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(SkillKinds.GoHome, true)]
    [InlineData(SkillKinds.Conflict, true)]
    public void CountsRestart_OnlyWhenAnotherJobTookTheRunsPlace(string skillKind, bool counts) =>
        Assert.Equal(counts, LfgRules.CountsRestart(skillKind));

    [Fact]
    public void RunGoesOn_OnlyWhileYoungWellStockedAndNotSpent()
    {
        var early = LfgRules.MinRun - TimeSpan.FromMinutes(1);

        Assert.True(LfgRules.RunGoesOn(early, healthy: true, suppliesLow: false, packFull: false, restarts: 0));
        Assert.False(LfgRules.RunGoesOn(LfgRules.MinRun, healthy: true, suppliesLow: false, packFull: false, restarts: 0));
        Assert.False(LfgRules.RunGoesOn(early, healthy: false, suppliesLow: false, packFull: false, restarts: 0));
        Assert.False(LfgRules.RunGoesOn(early, healthy: true, suppliesLow: true, packFull: false, restarts: 0));
        Assert.False(LfgRules.RunGoesOn(early, healthy: true, suppliesLow: false, packFull: true, restarts: 0));
        Assert.False(LfgRules.RunGoesOn(early, true, false, false, LfgRules.MaxTripRestarts));
    }

    [Theory]
    [InlineData(true, SkillKinds.Dungeon, true)]
    [InlineData(true, SkillKinds.Hunt, false)]
    [InlineData(false, SkillKinds.Hunt, true)]
    [InlineData(false, SkillKinds.Dungeon, false)]
    [InlineData(true, null, false)]
    public void SameRun_OnlyTheRunsOwnKind(bool dungeonRun, string skillKind, bool same) =>
        Assert.Equal(same, LfgRules.SameRun(dungeonRun, skillKind));

    [Theory]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, true, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, false, false, false)]
    public void GaterWanted_ADungeonCallWithNoGaterTakesOneAtOnce(bool dungeonRun, bool hasGater, bool gates, bool wanted) =>
        Assert.Equal(wanted, LfgRules.GaterWanted(dungeonRun, hasGater, gates));

    [Theory]
    [InlineData(150, 131, true)]
    [InlineData(150, 130, false)]
    [InlineData(0, 10, true)]
    public void RunFits_OnlyAPlaceWithinThePowersReach_SoACallNobodyAnsweredGoesSoloOnlyWhereTheCallerFits(int difficulty, int power, bool fits) =>
        Assert.Equal(fits, LfgRules.RunFits(difficulty, power));

    [Fact]
    public void GroupPower_CountsTheRestAsAFightCountsItsAllies()
    {
        const int Power = 200;

        Assert.Equal(Power, LfgRules.GroupPower(Power, 1));
        Assert.Equal(Power + (int)(Power * 2 * ThreatRating.PartyPowerShare), LfgRules.GroupPower(Power, LfgRules.ExpectedGroup));
        Assert.True(LfgRules.GroupPower(Power, LfgRules.ExpectedGroup) > Power * 2);
    }

    [Fact]
    public void MeetingSpots_ReachPastTheGateAndTheDoor()
    {
        Assert.True(LfgRules.MoongateCallTiles > 0);
        Assert.True(LfgRules.DoorCallTiles >= LfgRules.MoongateCallTiles);
        Assert.True(LfgRules.DungeonShoutPercent > LfgRules.HuntShoutPercent);
    }

    [Fact]
    public void Call_CarriesAcrossTheBankPlaza() =>
        Assert.True(LfgRules.AnswerRange > SosariaAI.Deliberation.Brain.SpeechCarryRange);
}
