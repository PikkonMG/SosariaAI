using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class AmbitionRulesTests
{
    private const string ConnorBackground =
        "Born in Britain. Cuts wood for a living and sells it to the carpenters. Saving for a small boat.";
    private const string ConnorPersonaId = "connor";
    private const string MiraBackground =
        "A miner from the hills west of Britain. Sells ore at the bank and keeps her pick sharp.";
    private const string EmptyBackground = "";
    private const string UnknownPersonaId = "unknown";
    private const int MidBoatProgress = 1500;
    private const int NearDoneProgress = 1500;
    private const int NextSeed = 7;

    [Fact]
    public void SeedFromBackground_ConnorBoat_IsGoldSmallBoat()
    {
        var ambition = AmbitionRules.SeedFromBackground(ConnorBackground);

        Assert.Equal(AmbitionKind.Gold, ambition.Kind);
        Assert.Equal("a small boat", ambition.Target);
        Assert.Equal(AmbitionRules.DefaultBoatGold, ambition.Goal);
        Assert.Equal(0, ambition.Progress);
    }

    [Fact]
    public void SeedFromBackground_SavingFor_IsCaseInsensitive()
    {
        var ambition = AmbitionRules.SeedFromBackground("SAVING FOR A SMALL BOAT. Other text.");

        Assert.Equal(AmbitionKind.Gold, ambition.Kind);
        Assert.Equal("A SMALL BOAT", ambition.Target);
        Assert.Equal(AmbitionRules.DefaultBoatGold, ambition.Goal);
    }

    [Fact]
    public void SeedFromBackground_MiraMiner_IsMiningSkill()
    {
        var ambition = AmbitionRules.SeedFromBackground(MiraBackground);

        Assert.Equal(AmbitionKind.Skill, ambition.Kind);
        Assert.Equal(AmbitionRules.SkillMining, ambition.Target);
        Assert.Equal(AmbitionRules.DefaultSkillGoal, ambition.Goal);
        Assert.Equal(0, ambition.Progress);
    }

    [Fact]
    public void SeedFromBackground_NoMatch_IsNestEgg()
    {
        var ambition = AmbitionRules.SeedFromBackground(EmptyBackground);

        Assert.Equal(AmbitionKind.Gold, ambition.Kind);
        Assert.Equal(AmbitionRules.NestEggTarget, ambition.Target);
        Assert.Equal(AmbitionRules.DefaultNestEggGold, ambition.Goal);
        Assert.Equal(0, ambition.Progress);
    }

    [Fact]
    public void IsComplete_AtBoatGoal_IsTrue()
    {
        var ambition = new Ambition(
            AmbitionKind.Gold,
            "a small boat",
            AmbitionRules.DefaultBoatGold,
            AmbitionRules.DefaultBoatGold
        );

        Assert.True(AmbitionRules.IsComplete(ambition));
    }

    [Fact]
    public void IsNearDone_AtThreeQuarters_IsTrue()
    {
        var ambition = new Ambition(
            AmbitionKind.Gold,
            "a small boat",
            AmbitionRules.DefaultBoatGold,
            NearDoneProgress
        );

        Assert.Equal(AmbitionRules.NearDoneFraction, AmbitionRules.Fraction(ambition));
        Assert.True(AmbitionRules.IsNearDone(ambition));
    }

    [Fact]
    public void Describe_GoldBoat_ContainsBoatAndNumbers()
    {
        var ambition = new Ambition(
            AmbitionKind.Gold,
            "a small boat",
            AmbitionRules.DefaultBoatGold,
            MidBoatProgress
        );

        var text = AmbitionRules.Describe(ambition);

        Assert.Contains("boat", text);
        Assert.Contains(MidBoatProgress.ToString(), text);
        Assert.Contains(AmbitionRules.DefaultBoatGold.ToString(), text);
    }

    [Fact]
    public void NextAfterComplete_ChangesKind()
    {
        var done = new Ambition(
            AmbitionKind.Gold,
            "a small boat",
            AmbitionRules.DefaultBoatGold,
            AmbitionRules.DefaultBoatGold
        );

        var next = AmbitionRules.NextAfterComplete(done, NextSeed, background: null);

        Assert.NotEqual(done.Kind, next.Kind);
    }

    [Fact]
    public void FinalizeSeed_SkillAlreadyMet_PicksAFittingNextWant()
    {
        var fishing = new Ambition(
            AmbitionKind.Skill,
            AmbitionRules.SkillFishing,
            AmbitionRules.DefaultSkillGoal,
            0
        );
        var ready = AmbitionRules.FinalizeSeed(
            fishing,
            AmbitionRules.DefaultSkillGoal,
            "He fishes the river west of Britain.",
            NextSeed
        );

        Assert.NotEqual(AmbitionKind.Skill, ready.Kind);
        Assert.NotEqual(AmbitionKind.Kills, ready.Kind);
        Assert.Equal(0, ready.Progress);
    }

    [Fact]
    public void FinalizeSeed_SkillInProgress_KeepsTheGoal()
    {
        var fishing = new Ambition(
            AmbitionKind.Skill,
            AmbitionRules.SkillFishing,
            AmbitionRules.DefaultSkillGoal,
            0
        );
        var ready = AmbitionRules.FinalizeSeed(fishing, 40, "He fishes.", NextSeed);

        Assert.Equal(AmbitionKind.Skill, ready.Kind);
        Assert.Equal(40, ready.Progress);
    }

    [Fact]
    public void PrefersWork_Gold_IsTrue()
    {
        var ambition = new Ambition(
            AmbitionKind.Gold,
            AmbitionRules.NestEggTarget,
            AmbitionRules.DefaultNestEggGold,
            0
        );

        Assert.True(AmbitionRules.PrefersWork(ambition));
        Assert.False(AmbitionRules.PrefersHunt(ambition));
        Assert.False(AmbitionRules.PrefersTravel(ambition));
    }

    [Fact]
    public void PrefersWork_Skill_IsTrue()
    {
        var ambition = new Ambition(
            AmbitionKind.Skill,
            AmbitionRules.SkillMining,
            AmbitionRules.DefaultSkillGoal,
            0
        );

        Assert.True(AmbitionRules.PrefersWork(ambition));
        Assert.False(AmbitionRules.PrefersHunt(ambition));
    }

    [Theory]
    [InlineData(AmbitionRules.SkillSwords)]
    [InlineData(AmbitionRules.SkillArchery)]
    public void PrefersHunt_FightSkill_IsTrue(string skill)
    {
        var ambition = new Ambition(AmbitionKind.Skill, skill, AmbitionRules.DefaultSkillGoal, 0);

        Assert.True(AmbitionRules.PrefersHunt(ambition));
        Assert.False(AmbitionRules.PrefersWork(ambition));
    }

    [Fact]
    public void PrefersHunt_Kills_IsTrue()
    {
        var ambition = new Ambition(
            AmbitionKind.Kills,
            AmbitionRules.CreatureTroll,
            AmbitionRules.DefaultKillGoal,
            0
        );

        Assert.True(AmbitionRules.PrefersHunt(ambition));
        Assert.False(AmbitionRules.PrefersWork(ambition));
    }

    [Fact]
    public void TalkLine_NearDone_MentionsAlmostThere()
    {
        var start = new Ambition(AmbitionKind.Gold, "a small boat", AmbitionRules.DefaultBoatGold, 0);
        var near = new Ambition(
            AmbitionKind.Gold,
            "a small boat",
            AmbitionRules.DefaultBoatGold,
            (int)(AmbitionRules.DefaultBoatGold * AmbitionRules.NearDoneFraction)
        );
        var done = new Ambition(
            AmbitionKind.Gold,
            "a small boat",
            AmbitionRules.DefaultBoatGold,
            AmbitionRules.DefaultBoatGold
        );

        Assert.Equal("Still saving for a small boat. 2000 gold to go.", AmbitionRules.TalkLine(start));
        Assert.Equal("Almost enough for a small boat now.", AmbitionRules.TalkLine(near));
        Assert.Equal("I finally have enough for a small boat.", AmbitionRules.TalkLine(done));
    }

    // Spoken aloud to players, so it must never read like a log line. The live shard
    // printed "hunting skeleton (0 of 10)" as dialogue before this.
    [Theory]
    [InlineData(AmbitionKind.Kills, "skeleton", 10, 0, "Out after skeletons. 10 still to go.")]
    [InlineData(AmbitionKind.Kills, "troll", 10, 10, "That is 10 trolls down.")]
    [InlineData(AmbitionKind.Skill, AmbitionRules.SkillSwords, 100, 40, "Working on my swordwork every day.")]
    [InlineData(AmbitionKind.Skill, AmbitionRules.SkillLumberjacking, 100, 100, "I have got my woodcutting where I wanted it.")]
    [InlineData(AmbitionKind.Place, AmbitionRules.PlaceMinoc, 1, 0, "One day I want to see Minoc.")]
    [InlineData(AmbitionKind.Place, AmbitionRules.PlaceMinoc, 1, 1, "I finally made it to Minoc.")]
    public void TalkLine_ReadsLikeSpeech(AmbitionKind kind, string target, int goal, int progress, string expected)
    {
        Assert.Equal(expected, AmbitionRules.TalkLine(new Ambition(kind, target, goal, progress)));
    }

    [Fact]
    public void TalkLine_NeverShowsRawCounters()
    {
        var line = AmbitionRules.TalkLine(new Ambition(AmbitionKind.Kills, "skeleton", 10, 3));

        Assert.DoesNotContain("(", line);
        Assert.DoesNotContain(" of ", line);
    }

    [Fact]
    public void WithProgress_ClampsToGoal()
    {
        var ambition = new Ambition(
            AmbitionKind.Gold,
            AmbitionRules.NestEggTarget,
            AmbitionRules.DefaultNestEggGold,
            0
        );

        var over = AmbitionRules.WithProgress(ambition, AmbitionRules.DefaultNestEggGold + 50);
        var under = AmbitionRules.WithProgress(ambition, -5);

        Assert.Equal(AmbitionRules.DefaultNestEggGold, over.Progress);
        Assert.Equal(0, under.Progress);
    }

    [Fact]
    public void MoodHint_TracksProgress()
    {
        var start = new Ambition(AmbitionKind.Gold, AmbitionRules.NestEggTarget, 100, 0);
        var mid = new Ambition(AmbitionKind.Gold, AmbitionRules.NestEggTarget, 100, 40);
        var near = new Ambition(AmbitionKind.Gold, AmbitionRules.NestEggTarget, 100, 75);
        var done = new Ambition(AmbitionKind.Gold, AmbitionRules.NestEggTarget, 100, 100);

        Assert.Equal(AmbitionRules.MoodJustStarted, AmbitionRules.MoodHint(start));
        Assert.Equal(AmbitionRules.MoodWellAlong, AmbitionRules.MoodHint(mid));
        Assert.Equal(AmbitionRules.MoodAlmostThere, AmbitionRules.MoodHint(near));
        Assert.Equal(AmbitionRules.MoodDone, AmbitionRules.MoodHint(done));
    }

    [Fact]
    public void Fraction_NonPositiveGoal_IsZero()
    {
        var ambition = new Ambition(AmbitionKind.None, string.Empty, 0, 10);
        Assert.Equal(0, AmbitionRules.Fraction(ambition));
        Assert.False(AmbitionRules.IsComplete(ambition));
    }

    // The ten shipped backgrounds, word for word. Before this, eight of them fell through to
    // the same "nest egg", which is the repetition the operator complained about.
    [Theory]
    [InlineData("Born in Britain. Cuts wood for a living and sells it to the carpenters. Saving for a small boat.", AmbitionKind.Gold, "a small boat")]
    [InlineData("A miner from the hills west of Britain. Sells ore at the bank and keeps her pick sharp.", AmbitionKind.Skill, AmbitionRules.SkillMining)]
    [InlineData("Fishes the Britain river from the south docks. Sells the catch and talks about the weather.", AmbitionKind.Skill, AmbitionRules.SkillFishing)]
    [InlineData("Walks Britain on errands. Bank, docks, smith. Knows every street and most of the regulars.", AmbitionKind.Place, AmbitionRules.PlaceMinoc)]
    [InlineData("A second woodcutter in Connor's forest. Younger, talks more, still learning the good stands.", AmbitionKind.Skill, AmbitionRules.SkillLumberjacking)]
    [InlineData("Veteran swordsman. Leads a crew through the Britain graveyard. Blunt. Wants a fair fight and no surprises.", AmbitionKind.Kills, AmbitionRules.CreatureSkeleton)]
    [InlineData("Veteran mage. Walks with Bran's graveyard crew. Dry. Cares more for books and fire than for small talk.", AmbitionKind.Kills, AmbitionRules.CreatureSkeleton)]
    [InlineData("Young archer. Still learning the bow. Eager. Walks with Bran's graveyard crew and tries not to miss.", AmbitionKind.Skill, AmbitionRules.SkillArchery)]
    [InlineData("Novice swordsman. Hunts skeletons alone to get better. Hungry for a real fight and a name worth remembering.", AmbitionKind.Skill, AmbitionRules.SkillSwords)]
    [InlineData("Veteran archer. Quiet. Prefers the dark of Despise to the streets of Britain.", AmbitionKind.Kills, AmbitionRules.CreatureTroll)]
    public void SeedFromBackground_ReadsWhatEachShippedPersonaActuallySays(string background, AmbitionKind kind, string target)
    {
        var ambition = AmbitionRules.SeedFromBackground(background);

        Assert.Equal(kind, ambition.Kind);
        Assert.Equal(target, ambition.Target);
    }

    // The shapes the library's want parts take: a thing to own, a trade to master, a
    // creature to hunt, a road to walk. None of them may fall through to the nest egg.
    [Theory]
    [InlineData("Wants a vendor on the busiest street in Britain.", AmbitionKind.Gold, "a vendor on the busiest street in Britain")]
    [InlineData("Hopes to buy back the family farm near Britain.", AmbitionKind.Gold, "the family farm near Britain")]
    [InlineData("Dreams of a quiet house by the Skara Brae shore.", AmbitionKind.Gold, "a quiet house by the Skara Brae shore")]
    [InlineData("Wants to become a grandmaster smith before the next winter.", AmbitionKind.Skill, nameof(Server.SkillName.Blacksmith))]
    [InlineData("Wants to breed the finest horses in Trinsic.", AmbitionKind.Skill, nameof(Server.SkillName.AnimalTaming))]
    [InlineData("Hopes to pull off the perfect heist at the Britain bank.", AmbitionKind.Skill, nameof(Server.SkillName.Stealing))]
    [InlineData("Wants to clear Covetous of harpies once and for all.", AmbitionKind.Kills, "Harpy")]
    [InlineData("Hopes to bring down a balron and live to tell the tale.", AmbitionKind.Kills, "Balron")]
    [InlineData("Wants to see every town's market in a single year.", AmbitionKind.Place, AmbitionRules.PlaceMinoc)]
    [InlineData("Wants to visit Moonglow and its gardens.", AmbitionKind.Place, "Moonglow")]
    public void SeedFromBackground_ReadsTheLibraryWantShapes(string background, AmbitionKind kind, string target)
    {
        var ambition = AmbitionRules.SeedFromBackground(background);

        Assert.Equal(kind, ambition.Kind);
        Assert.Equal(target, ambition.Target);
    }

    [Fact]
    public void SeedFromBackground_ThingFarAfterTheLead_IsNotAThing()
    {
        var ambition = AmbitionRules.SeedFromBackground("Wants to stop and rest and think about a life.");

        Assert.Equal(AmbitionRules.NestEggTarget, ambition.Target);
    }

    [Fact]
    public void SeedFromBackground_VeteranNeverGetsASkillGoalItWouldFinishAtOnce()
    {
        var ambition = AmbitionRules.SeedFromBackground("Veteran archer. Keeps to herself.");

        Assert.NotEqual(AmbitionKind.Skill, ambition.Kind);
    }

    [Fact]
    public void SeedFromBackground_VeteranTrollHunter_IsTrollKills()
    {
        var ambition = AmbitionRules.SeedFromBackground(
            "Veteran swordsman. Hunts trolls on the road to Despise."
        );

        Assert.Equal(AmbitionKind.Kills, ambition.Kind);
        Assert.Equal(AmbitionRules.CreatureTroll, ambition.Target);
        Assert.Equal(AmbitionRules.DefaultKillGoal, ambition.Goal);
    }

    [Fact]
    public void SeedFromBackground_MatchesWholeWordStartsOnly()
    {
        // "mage" sits inside "image"; that must not make someone a mage.
        var ambition = AmbitionRules.SeedFromBackground("Paints a fine image of the town.");

        Assert.Equal(AmbitionRules.NestEggTarget, ambition.Target);
    }

    [Fact]
    public void NextAfterComplete_SendsDifferentCharactersDifferentWays()
    {
        var done = new Ambition(AmbitionKind.Skill, AmbitionRules.SkillMining, AmbitionRules.DefaultSkillGoal, AmbitionRules.DefaultSkillGoal);
        var seen = new System.Collections.Generic.HashSet<string>();

        for (var seed = 0; seed < 20; seed++)
        {
            var next = AmbitionRules.NextAfterComplete(done, seed, background: null);
            seen.Add($"{next.Kind}:{next.Target}");
        }

        Assert.True(seen.Count > 2, $"only {seen.Count} distinct next ambitions across 20 characters");
    }

    [Fact]
    public void NextAfterComplete_IsStableForOneCharacter()
    {
        var done = new Ambition(AmbitionKind.Gold, "a small boat", AmbitionRules.DefaultBoatGold, AmbitionRules.DefaultBoatGold);

        Assert.Equal(AmbitionRules.NextAfterComplete(done, 42, background: null), AmbitionRules.NextAfterComplete(done, 42, background: null));
    }

    private static readonly Ambition UntouchedNestEgg =
        new(AmbitionKind.Gold, AmbitionRules.NestEggTarget, AmbitionRules.DefaultNestEggGold, 0);

    private static readonly Ambition Fishing =
        new(AmbitionKind.Skill, AmbitionRules.SkillFishing, AmbitionRules.DefaultSkillGoal, 0);

    [Fact]
    public void ShouldReseed_UpgradesAnUntouchedFallbackToThePersonasRealGoal()
    {
        Assert.True(AmbitionRules.ShouldReseed(UntouchedNestEgg, Fishing));
    }

    [Fact]
    public void ShouldReseed_NeverThrowsAwayRealProgress()
    {
        var workedOn = UntouchedNestEgg with { Progress = 250 };

        Assert.False(AmbitionRules.ShouldReseed(workedOn, Fishing));
    }

    [Fact]
    public void ShouldReseed_LeavesAnyChosenGoalAlone()
    {
        var boat = new Ambition(AmbitionKind.Gold, "a small boat", AmbitionRules.DefaultBoatGold, 0);

        Assert.False(AmbitionRules.ShouldReseed(boat, Fishing));
    }

    [Fact]
    public void ShouldReseed_DoesNotLoopWhenTheBackgroundHasNothingBetter()
    {
        Assert.False(AmbitionRules.ShouldReseed(UntouchedNestEgg, UntouchedNestEgg));
    }
}
