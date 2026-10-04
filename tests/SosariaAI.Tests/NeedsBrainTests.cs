using System;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class NeedsBrainTests
{
    private const int GraveyardRequiredPower = 40;
    private const int DespiseRequiredPower = 120;
    private const int PowerBelowDespise = 50;
    private const int PowerAboveDespise = 130;

    private static readonly ChoiceDefinition Town = new() { Routine = "town", Weight = 1, Description = "rest" };
    private static readonly ChoiceDefinition Graveyard = new() { Routine = "graveyard", Weight = 1, Description = "hunt" };
    private static readonly ChoiceDefinition Despise = new() { Routine = "despise", Weight = 1, Description = "dungeon" };
    private static readonly ChoiceDefinition GraveyardGated = new()
    {
        Routine = "graveyard",
        Weight = 1,
        Description = "hunt",
        RequiredPower = GraveyardRequiredPower
    };
    private static readonly ChoiceDefinition DespiseGated = new()
    {
        Routine = "despise",
        Weight = 1,
        Description = "dungeon",
        RequiredPower = DespiseRequiredPower
    };

    [Fact]
    public void Pick_Hurt_ChoosesRest()
    {
        var pick = NeedsBrain.Pick(
            [Town, Graveyard, Despise],
            new NeedsSnapshot
            {
                HitsFraction = 0.2,
                Drives = new PersonaDrives(0.4, 0.8, 0.3, isCustom: true)
            },
            seed: 1
        );

        Assert.Equal("town", pick);
    }

    [Fact]
    public void Pick_PoorBraveHealthy_ChoosesHunt()
    {
        var pick = NeedsBrain.Pick(
            [Town, Graveyard, Despise],
            new NeedsSnapshot
            {
                HitsFraction = 1,
                GoldBanked = 0,
                GoldCarried = 10,
                Drives = new PersonaDrives(greed: 0.7, caution: 0.2, valor: 0.9, isCustom: true)
            },
            seed: 2
        );

        Assert.Equal("graveyard", pick);
    }

    [Fact]
    public void Pick_PartyForming_ChoosesPartyTrip()
    {
        var pick = NeedsBrain.Pick(
            [Town, Graveyard, Despise],
            new NeedsSnapshot
            {
                HitsFraction = 0.9,
                PartyForming = true,
                Drives = new PersonaDrives(0.4, 0.4, 0.4, isCustom: true)
            },
            seed: 3
        );

        Assert.NotEqual("town", pick);
        Assert.Contains(pick, (string[])["graveyard", "despise"]);
    }

    [Fact]
    public void Pick_SeededTieBreak_IsDeterministic()
    {
        var needs = new NeedsSnapshot
        {
            HitsFraction = 0.9,
            Drives = new PersonaDrives(0.5, 0.5, 0.5, isCustom: true)
        };

        var first = NeedsBrain.Pick([Town, Graveyard], needs, seed: 42);
        var second = NeedsBrain.Pick([Town, Graveyard], needs, seed: 42);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Pick_NoDrivesNoSignals_UsesFallback()
    {
        var pick = NeedsBrain.Pick(
            [new ChoiceDefinition { Routine = "town", Weight = 1 }, new ChoiceDefinition { Routine = "graveyard", Weight = 9 }],
            new NeedsSnapshot(),
            seed: 1,
            fallbackNext: _ => 0
        );

        Assert.Equal("town", pick);
    }

    [Fact]
    public void ElapsedSince_Unset_IsMaxValue()
    {
        var now = new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(TimeSpan.MaxValue, NeedsBrain.ElapsedSince(default, now));
        Assert.Equal(TimeSpan.FromHours(2), NeedsBrain.ElapsedSince(now.AddHours(-2), now));
    }

    [Fact]
    public void Pick_PowerBelowDungeon_ChoosesHunt()
    {
        var pick = NeedsBrain.Pick(
            [Town, GraveyardGated, DespiseGated],
            new NeedsSnapshot
            {
                HitsFraction = 1,
                GoldBanked = 0,
                GoldCarried = 10,
                Power = PowerBelowDespise,
                Drives = new PersonaDrives(greed: 0.7, caution: 0.2, valor: 0.9, isCustom: true)
            },
            seed: 2
        );

        Assert.Equal("graveyard", pick);
    }

    [Fact]
    public void Pick_PowerMeetsDungeon_ChoosesDungeon()
    {
        var pick = NeedsBrain.Pick(
            [Town, GraveyardGated, DespiseGated],
            new NeedsSnapshot
            {
                HitsFraction = 1,
                GoldBanked = NeedsBrain.PoorGoldThreshold,
                Power = PowerAboveDespise,
                Drives = new PersonaDrives(greed: 0.3, caution: 0.2, valor: 0.9, isCustom: true)
            },
            seed: 4
        );

        Assert.Equal("despise", pick);
    }

    [Fact]
    public void Pick_PowerInTrainingBand_ChoosesHunt()
    {
        var pick = NeedsBrain.Pick(
            [Town, GraveyardGated, DespiseGated],
            new NeedsSnapshot
            {
                HitsFraction = NeedsBrain.HealthyHitsFraction,
                Power = (int)(DespiseRequiredPower * NeedsBrain.TrainingBand),
                Drives = new PersonaDrives(greed: 0.2, caution: 0.8, valor: 0.3, isCustom: true)
            },
            seed: 5
        );

        Assert.Equal("graveyard", pick);
    }

    [Fact]
    public void Score_UnmetRequiredPower_IsNearZero()
    {
        var score = NeedsBrain.Score(
            DespiseGated,
            new NeedsSnapshot { Power = PowerBelowDespise }
        );

        Assert.Equal(NeedsBrain.UnmetPowerScore, score);
    }

    [Fact]
    public void Score_NullOrZeroRequiredPower_UsesRoutineScore()
    {
        var needs = new NeedsSnapshot
        {
            HitsFraction = 1,
            Drives = new PersonaDrives(greed: 0.7, caution: 0.2, valor: 0.9, isCustom: true)
        };
        var zeroRequired = new ChoiceDefinition { Routine = "despise", RequiredPower = 0 };

        Assert.Equal(NeedsBrain.Score(RoutineFamily.Hunt, needs), NeedsBrain.Score(Graveyard, needs));
        Assert.Equal(NeedsBrain.Score(RoutineFamily.Dungeon, needs), NeedsBrain.Score(zeroRequired, needs));
    }

    [Fact]
    public void Score_MetRequiredPower_UsesRoutineScore()
    {
        var needs = new NeedsSnapshot
        {
            HitsFraction = 1,
            Power = PowerAboveDespise,
            Drives = new PersonaDrives(greed: 0.3, caution: 0.2, valor: 0.9, isCustom: true)
        };

        Assert.Equal(NeedsBrain.Score(RoutineFamily.Dungeon, needs), NeedsBrain.Score(DespiseGated, needs));
    }

    [Fact]
    public void Score_AuthoredRequiredPower_WinsOverCatalog()
    {
        var catalog = HighDespiseCatalog();
        var needs = new NeedsSnapshot { Power = DespiseRequiredPower };

        Assert.Equal(NeedsBrain.Score(RoutineFamily.Dungeon, needs), NeedsBrain.Score(DespiseGated, needs, catalog));
    }

    [Fact]
    public void Score_CatalogDifficulty_GatesWhenChoiceHasNoAuthoredPower()
    {
        var catalog = HighDespiseCatalog();
        var ungated = new ChoiceDefinition { Routine = "despise", Weight = 1 };
        var needs = new NeedsSnapshot { Power = DespiseRequiredPower };

        Assert.Equal(NeedsBrain.UnmetPowerScore, NeedsBrain.Score(ungated, needs, catalog));
    }

    [Fact]
    public void Pick_WeakHunterWithParty_ChoosesGraveyard()
    {
        var pick = NeedsBrain.Pick(
            [Town, GraveyardGated],
            new NeedsSnapshot
            {
                HitsFraction = NeedsBrain.HealthyHitsFraction,
                Power = 25,
                AlliesPower = 220,
                Drives = new PersonaDrives(greed: 0.2, caution: 0.3, valor: 0.8, isCustom: true)
            },
            seed: 6
        );

        Assert.Equal("graveyard", pick);
    }

    private static DestinationCatalog HighDespiseCatalog() =>
        new(
            [
                new Destination
                {
                    Name = "Despise Entrance",
                    Kind = "Dungeon",
                    Role = "Despise",
                    Node = "hub",
                    Aliases = ["despise"],
                    Difficulty = DespiseRequiredPower + 80
                }
            ]
        );
}
