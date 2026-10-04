using System.Collections.Generic;
using System.Linq;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class ActionScorerTests
{
    private const int PackedBankSeed = 2;
    private const int MiraRosterIndex = 1;
    private const int BranRosterIndex = 5;
    private const int PackedMiraPower = 31;
    private const double PackedFillFraction = 0.9;
    private const int BarredFighterPower = 80;
    private const int BarredHuntSeed = 4;
    private const string OffPlanWhy = "off the current plan";
    private const int HallBar = 90;
    [Fact]
    public void Rank_PackedWorkerOutOfTown_DoesNotPickSell()
    {
        var file = CharactersFile.CreateDefault();
        var mira = file.Facets[FacetNames.Felucca].Roster[1];
        var catalog = ActionCatalog.From(mira, catalog: null);
        var situation = new Situation
        {
            Needs = new NeedsSnapshot
            {
                HitsFraction = 1,
                PackFillFraction = 0.9,
                Power = 31,
                AmbitionWantsWork = true,
                Drives = new PersonaDrives(greed: 0.8, caution: 0.4, valor: 0.2, isCustom: true)
            },
            Location = CharactersFile.MiraSpawn,
            InTownRegion = false,
            Role = CharacterRole.Worker,
            HasHarvestGoods = true
        };
        var goal = GoalRules.Pick(situation);
        var result = ActionScorer.Rank(catalog, situation, goal, seed: 7);

        Assert.Equal(GoalKind.Trade, goal.Kind);
        Assert.NotEqual(SkillKinds.VendorSell, result.Winner.SkillKind);
        Assert.True(ActionPlaceRules.IsTownWalk(FindStep(catalog, result.Winner.Id)));
    }

    [Fact]
    public void Rank_PackedWorkerInTown_PicksSellNotBank()
    {
        var catalog = MiraCatalog();
        var situation = PackedMiraInTown();
        var goal = GoalRules.Pick(situation);
        var result = ActionScorer.Rank(catalog, situation, goal, seed: PackedBankSeed);

        Assert.Equal(GoalKind.Trade, goal.Kind);
        Assert.Equal(SkillKinds.VendorSell, result.Winner.SkillKind);
        Assert.NotEqual(SkillKinds.BankDeposit, result.Winner.SkillKind);
    }

    [Fact]
    public void Rank_PackedWorkerInTown_PicksSellAfterRecentSell()
    {
        var catalog = MiraCatalog();
        var sellId = catalog.Find(a => a.SkillKind == SkillKinds.VendorSell).Id.Value;
        var situation = PackedMiraInTown(sellId, [SkillKinds.VendorSell]);
        var goal = GoalRules.Pick(situation);
        var result = ActionScorer.Rank(catalog, situation, goal, seed: PackedBankSeed);

        Assert.Equal(GoalKind.Trade, goal.Kind);
        Assert.Equal(SkillKinds.VendorSell, result.Winner.SkillKind);
        Assert.NotEqual(SkillKinds.BankDeposit, result.Winner.SkillKind);
    }

    [Fact]
    public void Rank_CanBuyHouse_PicksHouse()
    {
        var file = CharactersFile.CreateDefault();
        var connor = file.Facets[FacetNames.Felucca].Roster[0];
        var catalog = ActionCatalog.From(connor, catalog: null);
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, PackFillFraction = 0.1, Power = 40 },
            Location = CharactersFile.DefaultBankSpot,
            InTownRegion = true,
            Role = CharacterRole.Worker,
            CanBuyHouse = true
        };
        var goal = GoalRules.Pick(situation);
        var result = ActionScorer.Rank(catalog, situation, goal, seed: 2);

        Assert.Equal(GoalKind.Work, goal.Kind);
        Assert.Equal(SkillKinds.House, result.Winner.SkillKind);
        Assert.Contains("house", result.WinnerReason);
    }

    [Fact]
    public void Rank_LastWalkFailed_AllowsNewTravelAndSelfRoutingWork()
    {
        var file = CharactersFile.CreateDefault();
        var connor = file.Facets[FacetNames.Felucca].Roster[0];
        var catalog = ActionCatalog.From(connor, catalog: null);
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, PackFillFraction = 0.1, Power = 40 },
            Location = CharactersFile.DefaultSpawn,
            InTownRegion = true,
            Role = CharacterRole.Worker,
            LastWalkFailed = true
        };
        var result = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Work, null), seed: 3);

        Assert.Contains(result.Ranked, a => a.SkillKind == SkillKinds.Lumberjack && ActionScorer.IsEligible(a));
        Assert.Contains(result.Ranked, a => a.SkillKind == SkillKinds.GoTo && ActionScorer.IsEligible(a));
    }

    [Fact]
    public void Rank_BlockedSellAction_DoesNotPickSell()
    {
        var file = CharactersFile.CreateDefault();
        var mira = file.Facets[FacetNames.Felucca].Roster[1];
        var catalog = ActionCatalog.From(mira, catalog: null);
        var sellId = catalog.Find(a => a.SkillKind == SkillKinds.VendorSell).Id.Value;
        var situation = new Situation
        {
            Needs = new NeedsSnapshot
            {
                HitsFraction = 1,
                PackFillFraction = 0.9,
                Power = 31,
                Drives = new PersonaDrives(0.8, 0.4, 0.2, isCustom: true)
            },
            Location = CharactersFile.DefaultBankSpot,
            InTownRegion = true,
            Role = CharacterRole.Worker,
            HasHarvestGoods = true,
            BlockedActionId = sellId,
            BlockedCount = RepeatFailure.Limit
        };
        var result = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Trade, null), seed: 2);
        Assert.NotEqual(SkillKinds.VendorSell, result.Winner.SkillKind);
    }

    [Fact]
    public void Rank_BlockedSellKind_BlocksAnotherVendorSellAction()
    {
        var file = CharactersFile.CreateDefault();
        var mira = file.Facets[FacetNames.Felucca].Roster[1];
        var catalog = ActionCatalog.From(mira, catalog: null);
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, PackFillFraction = 0.9, Power = 31 },
            Location = CharactersFile.DefaultBankSpot,
            InTownRegion = true,
            Role = CharacterRole.Worker,
            HasHarvestGoods = true,
            BlockedActionId = "old-shop:VendorSell:99",
            BlockedCount = RepeatFailure.Limit
        };

        var result = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Trade, null), seed: 2);

        Assert.DoesNotContain(result.Ranked, action =>
            action.SkillKind == SkillKinds.VendorSell && ActionScorer.IsEligible(action));
    }

    [Fact]
    public void Rank_MultipleCoolingSkills_BlocksAlternatingFailureLoop()
    {
        var file = CharactersFile.CreateDefault();
        var character = file.Facets[FacetNames.Felucca].Roster[1];
        var catalog = ActionCatalog.From(character, catalog: null);
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, PackFillFraction = 0.9, Power = 31 },
            Location = CharactersFile.DefaultBankSpot,
            InTownRegion = true,
            Role = CharacterRole.Worker,
            HasHarvestGoods = true,
            BlockedSkillKinds = [SkillKinds.VendorSell, SkillKinds.Tavern, SkillKinds.Dungeon]
        };

        var result = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Work, null), seed: 2);

        Assert.DoesNotContain(result.Ranked, action =>
            situation.BlockedSkillKinds.Contains(action.SkillKind) && ActionScorer.IsEligible(action));
    }

    [Fact]
    public void Rank_SkillWithNothingHereToUseItOn_NeverWins()
    {
        // Cooks with no heat, tamers with no beast and beggars with no one near were
        // picked at the Britain bank, failed at their start, and were picked again.
        var file = CharactersFile.CreateDefault();
        var character = file.Facets[FacetNames.Felucca].Roster[1];
        var catalog = ActionCatalog.From(character, catalog: null);
        var open = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 31 },
            Location = CharactersFile.DefaultBankSpot,
            InTownRegion = true,
            Role = CharacterRole.Worker
        };
        var goal = new Goal(GoalKind.Work, null);
        var winner = ActionScorer.Rank(catalog, open, goal, seed: 2).Winner.SkillKind;
        var gated = open with { UnmetSkillKinds = [winner] };

        var result = ActionScorer.Rank(catalog, gated, goal, seed: 2);

        Assert.NotNull(winner);
        Assert.DoesNotContain(result.Ranked, action => action.SkillKind == winner && ActionScorer.IsEligible(action));
    }

    [Fact]
    public void Rank_AfterRepeatedEscapes_PicksGoHomeInsteadOfFlee()
    {
        var file = CharactersFile.CreateDefault();
        var worker = file.Facets[FacetNames.Felucca].Roster[0];
        var catalog = ActionCatalog.From(worker, catalog: null);
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 20 },
            Location = new Point3D(1300, 1400, 0),
            Role = CharacterRole.Worker,
            MustFlee = true,
            RecentRuns = FleeRules.RunsBeforeBlockingFlee
        };

        var result = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Flee, null), seed: 3);

        Assert.NotEqual(SkillKinds.Flee, result.Winner.SkillKind);
    }

    [Fact]
    public void Rank_FighterAtGraveyard_PicksHuntNotBank()
    {
        var file = CharactersFile.CreateDefault();
        var bran = file.Facets[FacetNames.Felucca].Roster[5];
        var catalog = ActionCatalog.From(bran, catalog: null);
        var situation = new Situation
        {
            Needs = new NeedsSnapshot
            {
                HitsFraction = 1,
                Power = 80,
                AlliesPower = 80,
                AmbitionWantsHunt = true,
                Drives = new PersonaDrives(0.4, 0.3, 0.8, isCustom: true)
            },
            Location = CharactersFile.GraveyardGoPoint,
            InTownRegion = false,
            Role = CharacterRole.Fighter,
            HasHarvestGoods = false
        };
        var result = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Hunt, null), seed: 4);

        Assert.Equal(SkillKinds.Hunt, result.Winner.SkillKind);
        Assert.NotEqual(SkillKinds.BankDeposit, result.Winner.SkillKind);
    }

    [Fact]
    public void Rank_JustFinishedAction_IsCutSoAnotherWins()
    {
        var file = CharactersFile.CreateDefault();
        var bran = file.Facets[FacetNames.Felucca].Roster[5];
        var catalog = ActionCatalog.From(bran, catalog: null);
        var huntId = catalog.Find(a => a.SkillKind == SkillKinds.Hunt).Id.Value;
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 80, AlliesPower = 80 },
            Location = CharactersFile.GraveyardGoPoint,
            Role = CharacterRole.Fighter,
            CurrentActionId = huntId
        };
        var result = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Hunt, null), seed: 4);
        Assert.NotEqual(SkillKinds.Hunt, result.Winner.SkillKind);
    }

    [Fact]
    public void Rank_PartyFormingAtBank_PicksHunt()
    {
        var file = CharactersFile.CreateDefault();
        var bran = file.Facets[FacetNames.Felucca].Roster[BranRosterIndex];
        var catalog = ActionCatalog.From(bran, catalog: null);
        var situation = new Situation
        {
            Needs = new NeedsSnapshot
            {
                HitsFraction = 1,
                Power = 80,
                AlliesPower = 80,
                PartyForming = true,
                AmbitionWantsHunt = true,
                Drives = new PersonaDrives(0.4, 0.3, 0.8, isCustom: true)
            },
            Location = CharactersFile.DefaultBankSpot,
            InTownRegion = true,
            Role = CharacterRole.Fighter,
            HasHarvestGoods = false
        };
        var result = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Hunt, null), seed: 4);

        Assert.Equal(SkillKinds.Hunt, result.Winner.SkillKind);
    }

    [Fact]
    public void Rank_JustFinishedFollow_DoesNotPickAnotherFollow()
    {
        var file = CharactersFile.CreateDefault();
        var sela = file.Facets[FacetNames.Felucca].Roster[6];
        var catalog = ActionCatalog.From(sela, catalog: null);
        var followId = catalog.Find(a => a.SkillKind == SkillKinds.Follow).Id.Value;
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 80, AlliesPower = 80 },
            Location = CharactersFile.DefaultBankSpot,
            InTownRegion = true,
            Role = CharacterRole.Fighter,
            CurrentActionId = followId
        };
        var result = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Hunt, null), seed: 4);
        Assert.NotEqual(SkillKinds.Follow, result.Winner.SkillKind);
    }

    [Fact]
    public void Rank_RecentLeisure_PicksAThirdKind()
    {
        var file = CharactersFile.CreateDefault();
        var connor = file.Facets[FacetNames.Felucca].Roster[0];
        var catalog = ActionCatalog.From(connor, catalog: null);
        var sightseeId = catalog.Find(a => a.SkillKind == SkillKinds.Sightsee).Id.Value;
        var situation = new Situation
        {
            Needs = new NeedsSnapshot
            {
                HitsFraction = 1,
                Power = 40,
                AmbitionWantsTravel = true,
                DayPart = DayPart.Work.ToString()
            },
            Location = CharactersFile.DefaultBankSpot,
            InTownRegion = true,
            Role = CharacterRole.Worker,
            CurrentActionId = sightseeId,
            RecentSkillKinds = [SkillKinds.Loiter]
        };
        var result = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Leisure, null), seed: 1);
        Assert.NotEqual(SkillKinds.Loiter, result.Winner.SkillKind);
        Assert.NotEqual(SkillKinds.Sightsee, result.Winner.SkillKind);
        Assert.False(string.IsNullOrWhiteSpace(result.WinnerReason));
    }

    [Fact]
    public void Rank_LeisureOutsideTown_PicksAThirdKind()
    {
        var file = CharactersFile.CreateDefault();
        var walker = file.Facets[FacetNames.Felucca].Roster[3];
        var catalog = ActionCatalog.From(walker, catalog: null);
        var loiterId = catalog.Find(a => a.SkillKind == SkillKinds.Loiter).Id.Value;
        var situation = new Situation
        {
            Needs = new NeedsSnapshot
            {
                HitsFraction = 1,
                Power = 40,
                AmbitionWantsTravel = true
            },
            Location = new Point3D(5488, 673, 20),
            InTownRegion = false,
            Role = CharacterRole.Worker,
            CurrentActionId = loiterId,
            RecentSkillKinds = [SkillKinds.Sightsee]
        };
        var result = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Leisure, null), seed: 1);
        Assert.NotEqual(SkillKinds.Loiter, result.Winner.SkillKind);
        Assert.NotEqual(SkillKinds.Sightsee, result.Winner.SkillKind);
    }

    [Fact]
    public void Rank_OutsideTown_DoesNotPickTavern()
    {
        var file = CharactersFile.CreateDefault();
        var connor = file.Facets[FacetNames.Felucca].Roster[0];
        var catalog = ActionCatalog.From(connor, catalog: null);
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40, AmbitionWantsTravel = true },
            Location = new Point3D(1293, 1312, 0),
            InTownRegion = false,
            Role = CharacterRole.Worker
        };
        var result = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Leisure, null), seed: 8);
        Assert.NotEqual(SkillKinds.Tavern, result.Winner.SkillKind);
        Assert.NotEqual(SkillKinds.Visit, result.Winner.SkillKind);
        Assert.NotEqual(SkillKinds.UpgradeGear, result.Winner.SkillKind);
        Assert.NotEqual(SkillKinds.BankDeposit, result.Winner.SkillKind);
    }

    [Fact]
    public void Rank_Top3_HasAtMostThree()
    {
        var file = CharactersFile.CreateDefault();
        var bran = file.Facets[FacetNames.Felucca].Roster[5];
        var catalog = ActionCatalog.From(bran, catalog: null);
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 80, AlliesPower = 80 },
            InTownRegion = true,
            Role = CharacterRole.Fighter
        };
        var result = ActionScorer.Rank(catalog, situation, GoalRules.Pick(situation), seed: 1);

        Assert.True(result.Top3.Count <= ActionScorer.TopCount);
        Assert.False(string.IsNullOrWhiteSpace(result.WinnerReason));
    }

    [Fact]
    public void Rank_MustFlee_PicksFleeNotUpgradeGear()
    {
        var file = CharactersFile.CreateDefault();
        var olin = file.Facets[FacetNames.Felucca].Roster[0];
        var catalog = ActionCatalog.From(olin, catalog: null);
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40 },
            Location = CharactersFile.DefaultBankSpot,
            InTownRegion = true,
            Role = CharacterRole.Worker,
            MustFlee = true
        };
        var result = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Flee, null), seed: 1);

        Assert.Equal(SkillKinds.Flee, result.Winner.SkillKind);
        Assert.NotEqual(SkillKinds.UpgradeGear, result.Winner.SkillKind);
        Assert.Contains("threat", result.WinnerReason);
    }

    [Fact]
    public void Rank_MustFleeAndFleeBlocked_PicksGoHomeNotDungeon()
    {
        var file = CharactersFile.CreateDefault();
        var bran = file.Facets[FacetNames.Felucca].Roster[BranRosterIndex];
        var catalog = ActionCatalog.From(bran, catalog: null);
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40 },
            Location = new Point3D(1327, 1096, 5),
            Role = CharacterRole.Worker,
            MustFlee = true,
            BlockedActionId = "flee:Flee:0",
            BlockedCount = RepeatFailure.Limit,
            BeyondLeash = true,
            DistanceFromHome = 600
        };
        var result = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Flee, null), seed: 1);

        Assert.False(string.IsNullOrWhiteSpace(result.Winner.SkillKind));
        Assert.NotEqual(SkillKinds.Flee, result.Winner.SkillKind);
        Assert.NotEqual(SkillKinds.Dungeon, result.Winner.SkillKind);
        Assert.NotEqual(SkillKinds.Hunt, result.Winner.SkillKind);
        Assert.True(
            result.Winner.SkillKind is SkillKinds.GoHome or SkillKinds.IdleWander,
            result.Winner.SkillKind
        );
    }

    [Fact]
    public void Rank_Recover_DoesNotPickHunt()
    {
        var file = CharactersFile.CreateDefault();
        var bran = file.Facets[FacetNames.Felucca].Roster[BranRosterIndex];
        var catalog = ActionCatalog.From(bran, catalog: null);
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 0.5, Power = 68 },
            Location = CharactersFile.GraveyardGoPoint,
            Role = CharacterRole.Fighter
        };
        var result = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Recover, null), seed: 4);

        Assert.NotEqual(SkillKinds.Hunt, result.Winner.SkillKind);
        Assert.NotEqual(SkillKinds.Dungeon, result.Winner.SkillKind);
        Assert.NotEqual(SkillKinds.Follow, result.Winner.SkillKind);
    }

    [Fact]
    public void Rank_BeyondLeash_PicksGoHome()
    {
        var file = CharactersFile.CreateDefault();
        var connor = file.Facets[FacetNames.Felucca].Roster[0];
        var catalog = ActionCatalog.From(connor, catalog: null);
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40 },
            Location = new Point3D(2728, 893, 0),
            Role = CharacterRole.Worker,
            BeyondLeash = true,
            DistanceFromHome = 900
        };
        var result = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Work, null), seed: 2);

        Assert.Equal(SkillKinds.GoHome, result.Winner.SkillKind);
    }

    [Fact]
    public void Rank_NoGold_DoesNotPickUpgradeGear()
    {
        var file = CharactersFile.CreateDefault();
        var connor = file.Facets[FacetNames.Felucca].Roster[0];
        var catalog = ActionCatalog.From(connor, catalog: null);
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40, CanShop = false },
            Location = CharactersFile.DefaultBankSpot,
            InTownRegion = true,
            Role = CharacterRole.Worker,
            Gold = 0,
            CanUpgradeGear = false
        };
        var result = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Recover, null), seed: 2);

        Assert.NotEqual(SkillKinds.UpgradeGear, result.Winner.SkillKind);
    }

    [Fact]
    public void Rank_NothingToBank_NeverPicksABankTrip()
    {
        // A woodcutter with an empty pack walked to the bank and "deposited 0 items"
        // about twelve times in five minutes.
        var catalog = MiraCatalog();
        var situation = EmptyMiraInTown(nothingToBank: true);
        var result = ActionScorer.Rank(catalog, situation, GoalRules.Pick(situation), seed: PackedBankSeed);

        Assert.Contains(result.Ranked, a => a.SkillKind == SkillKinds.BankDeposit);
        Assert.All(
            result.Ranked,
            a =>
            {
                if (a.SkillKind == SkillKinds.BankDeposit)
                {
                    Assert.False(ActionScorer.IsEligible(a));
                }
            }
        );
    }

    [Fact]
    public void Rank_SomethingToBank_KeepsTheBankTripOpen()
    {
        var catalog = MiraCatalog();
        var situation = EmptyMiraInTown(nothingToBank: false);
        var result = ActionScorer.Rank(catalog, situation, GoalRules.Pick(situation), seed: PackedBankSeed);

        Assert.Contains(result.Ranked, a => a.SkillKind == SkillKinds.BankDeposit && ActionScorer.IsEligible(a));
    }

    private static Situation EmptyMiraInTown(bool nothingToBank) =>
        new()
        {
            Needs = new NeedsSnapshot
            {
                HitsFraction = 1,
                Power = PackedMiraPower,
                AmbitionWantsWork = true,
                Drives = new PersonaDrives(greed: 0.8, caution: 0.4, valor: 0.2, isCustom: true)
            },
            Location = CharactersFile.DefaultBankSpot,
            InTownRegion = true,
            Role = CharacterRole.Worker,
            NothingToBank = nothingToBank
        };

    private static List<ActionCandidate> MiraCatalog()
    {
        var file = CharactersFile.CreateDefault();
        return ActionCatalog.From(file.Facets[FacetNames.Felucca].Roster[MiraRosterIndex], catalog: null);
    }

    private static Situation PackedMiraInTown(string currentActionId = null, string[] recentSkillKinds = null) =>
        new()
        {
            Needs = new NeedsSnapshot
            {
                HitsFraction = 1,
                PackFillFraction = PackedFillFraction,
                Power = PackedMiraPower,
                AmbitionWantsWork = true,
                Drives = new PersonaDrives(greed: 0.8, caution: 0.4, valor: 0.2, isCustom: true)
            },
            Location = CharactersFile.DefaultBankSpot,
            InTownRegion = true,
            Role = CharacterRole.Worker,
            HasHarvestGoods = true,
            CurrentActionId = currentActionId,
            RecentSkillKinds = recentSkillKinds
        };

    private static SkillStepDefinition FindStep(List<ActionCandidate> catalog, ActionId id)
    {
        for (var i = 0; i < catalog.Count; i++)
        {
            if (catalog[i].Id.Value == id.Value)
            {
                return catalog[i].Step;
            }
        }

        return null;
    }

    [Fact]
    public void Rank_AllIneligible_HasNoWinner()
    {
        var candidates = new List<ActionCandidate>
        {
            new()
            {
                Id = new ActionId("town:Flee:0"),
                SkillKind = SkillKinds.Flee,
                RoutineId = "town",
                Step = new SkillStepDefinition { Skill = SkillKinds.Flee }
            }
        };
        var situation = new Situation { Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40 }, MustFlee = false };
        var result = ActionScorer.Rank(candidates, situation, new Goal(GoalKind.Work, null), seed: 1);

        Assert.Single(result.Ranked);
        Assert.False(ActionScorer.IsEligible(result.Ranked[0]));
        Assert.Null(result.Winner.Id.Value);
        Assert.Equal("no action", result.WinnerReason);
    }

    [Fact]
    public void Rank_RecoverAfterRepeatedRuns_PicksGoHomeNotWander()
    {
        // After three flees, wander at the same mouth began IdleWander every few
        // seconds. Walk home, even inside the leash.
        var candidates = new List<ActionCandidate>
        {
            new()
            {
                Id = new ActionId("town:IdleWander:0"),
                SkillKind = SkillKinds.IdleWander,
                RoutineId = "town",
                Step = new SkillStepDefinition { Skill = SkillKinds.IdleWander }
            },
            new()
            {
                Id = new ActionId("work:GoTo:0"),
                SkillKind = SkillKinds.GoTo,
                RoutineId = "work",
                Step = new SkillStepDefinition { Skill = SkillKinds.GoTo }
            },
            new()
            {
                Id = new ActionId("home:GoHome:0"),
                SkillKind = SkillKinds.GoHome,
                RoutineId = "home",
                Step = new SkillStepDefinition { Skill = SkillKinds.GoHome }
            },
            new()
            {
                Id = new ActionId("flee:Flee:0"),
                SkillKind = SkillKinds.Flee,
                RoutineId = "flee",
                Step = new SkillStepDefinition { Skill = SkillKinds.Flee }
            }
        };
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 0.4, Power = 20 },
            Location = new Point3D(1375, 1954, 0),
            InTownRegion = false,
            Role = CharacterRole.Worker,
            KeepsAwayFromParty = true,
            RecentRuns = FleeRules.RunsBeforeGoingHome,
            BeyondLeash = false,
            DistanceFromHome = 260
        };
        var result = ActionScorer.Rank(candidates, situation, new Goal(GoalKind.Recover, null), seed: 1);

        Assert.Equal(SkillKinds.GoHome, result.Winner.SkillKind);
        Assert.All(
            result.Ranked,
            a =>
            {
                if (a.SkillKind is SkillKinds.IdleWander or SkillKinds.GoTo)
                {
                    Assert.False(ActionScorer.IsEligible(a));
                }
            }
        );
    }

    [Fact]
    public void Rank_RecoverAfterRepeatedRuns_InTown_AllowsWander()
    {
        var candidates = new List<ActionCandidate>
        {
            new()
            {
                Id = new ActionId("town:IdleWander:0"),
                SkillKind = SkillKinds.IdleWander,
                RoutineId = "town",
                Step = new SkillStepDefinition { Skill = SkillKinds.IdleWander }
            },
            new()
            {
                Id = new ActionId("home:GoHome:0"),
                SkillKind = SkillKinds.GoHome,
                RoutineId = "home",
                Step = new SkillStepDefinition { Skill = SkillKinds.GoHome }
            }
        };
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 0.4, Power = 20 },
            Location = CharactersFile.DefaultBankSpot,
            InTownRegion = true,
            Role = CharacterRole.Worker,
            KeepsAwayFromParty = true,
            RecentRuns = FleeRules.RunsBeforeGoingHome,
            BeyondLeash = false
        };
        var result = ActionScorer.Rank(candidates, situation, new Goal(GoalKind.Recover, null), seed: 1);

        Assert.Equal(SkillKinds.IdleWander, result.Winner.SkillKind);
        Assert.All(
            result.Ranked,
            a =>
            {
                if (a.SkillKind == SkillKinds.GoHome)
                {
                    Assert.False(ActionScorer.IsEligible(a));
                }
            }
        );
    }

    [Fact]
    public void Rank_RanFromThePartyTooOften_NeverFollows()
    {
        // Three party members ran from the graveyard, walked back to the leader, and ran
        // again, every twelve seconds for an hour.
        var file = CharactersFile.CreateDefault();
        var sela = file.Facets[FacetNames.Felucca].Roster[6];
        var catalog = ActionCatalog.From(sela, catalog: null);
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 80, AlliesPower = 80 },
            Location = CharactersFile.DefaultBankSpot,
            Role = CharacterRole.Fighter,
            KeepsAwayFromParty = true,
            RecentRuns = FleeRules.RunsBeforeLeavingTrip
        };
        var result = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Hunt, null), seed: 4);

        Assert.Contains(result.Ranked, a => a.SkillKind == SkillKinds.Follow);
        Assert.All(
            result.Ranked,
            a =>
            {
                if (a.SkillKind == SkillKinds.Follow)
                {
                    Assert.False(ActionScorer.IsEligible(a));
                }
            }
        );
    }

    [Fact]
    public void Rank_BeyondLeash_GoHomeFailedRepeatedly_StillGoesHome()
    {
        // A block on going home left everything else forbidden, and two characters stood
        // in Despise for good. The moongate fallback never got its turn.
        var file = CharactersFile.CreateDefault();
        var connor = file.Facets[FacetNames.Felucca].Roster[0];
        var catalog = ActionCatalog.From(connor, catalog: null);
        var homeId = catalog.Find(a => a.SkillKind == SkillKinds.GoHome).Id.Value;
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40 },
            Location = new Point3D(5503, 570, 51),
            Role = CharacterRole.Worker,
            BeyondLeash = true,
            DistanceFromHome = 4000,
            BlockedActionId = homeId,
            BlockedCount = RepeatFailure.Limit
        };
        var result = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Work, null), seed: 2);

        Assert.Equal(SkillKinds.GoHome, result.Winner.SkillKind);
    }

    [Fact]
    public void Rank_BeyondLeash_GoHomeCoolingDown_WaitsOutTheCooldown()
    {
        // The streak above has no clock, so going home stays out of it; the cooldown has
        // one. Exempt, four walkers in Felucca dungeons started GoHome again six seconds
        // after "leaves GoHome for 5 minutes", for an hour.
        var file = CharactersFile.CreateDefault();
        var connor = file.Facets[FacetNames.Felucca].Roster[0];
        var catalog = ActionCatalog.From(connor, catalog: null);
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40 },
            Location = new Point3D(5503, 570, 51),
            Role = CharacterRole.Worker,
            BeyondLeash = true,
            DistanceFromHome = 4000,
            BlockedSkillKinds = [SkillKinds.GoHome]
        };
        var result = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Work, null), seed: 2);

        Assert.NotEqual(SkillKinds.GoHome, result.Winner.SkillKind);
        Assert.DoesNotContain(result.Ranked, action => action.SkillKind == SkillKinds.GoHome && ActionScorer.IsEligible(action));
    }

    [Fact]
    public void Unavailable_VendorSellWithNoBuyerInReach_SaysSo()
    {
        var file = CharactersFile.CreateDefault();
        var character = file.Facets[FacetNames.Felucca].Roster[1];
        var sell = ActionCatalog.From(character, catalog: null).Find(action => action.SkillKind == SkillKinds.VendorSell);
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 31 },
            Location = CharactersFile.DefaultBankSpot,
            InTownRegion = true,
            Role = CharacterRole.Worker
        };

        Assert.NotNull(sell);
        Assert.Equal("no shop in reach buys the goods", ActionScorer.Unavailable(sell, situation, catalog: null));
        Assert.Null(ActionScorer.Unavailable(sell, situation with { HasLootGoods = true }, catalog: null));
    }

    [Fact]
    public void Rank_ActiveMinerPlan_PicksMineNotTavern()
    {
        var file = CharactersFile.CreateDefault();
        var mira = file.Facets[FacetNames.Felucca].Roster[1];
        var catalog = ActionCatalog.From(mira, catalog: null);
        var plan = GoalPlanRules.Start(
            new Goal(GoalKind.Work, GoalRules.NoTarget),
            new Situation { Needs = new NeedsSnapshot { HitsFraction = 1 }, Role = CharacterRole.Worker },
            GoalPlanRules.SkillKindsFrom(catalog),
            seed: 0
        );
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, PackFillFraction = 0.1, Power = 31 },
            Location = CharactersFile.MiraSpawn,
            Role = CharacterRole.Worker,
            CurrentActionId = catalog.Find(a => a.SkillKind == SkillKinds.Mine).Id.Value,
            RecentSkillKinds = [SkillKinds.Mine]
        };
        var result = ActionScorer.Rank(
            catalog,
            situation,
            new Goal(GoalKind.Work, GoalRules.NoTarget),
            seed: 7,
            catalog: null,
            plan
        );

        Assert.Equal(SkillKinds.Mine, result.Winner.SkillKind);
        Assert.Equal(plan.Id, result.Plan.Id);
    }

    [Fact]
    public void Rank_DiedRecently_CutsHunt()
    {
        var file = CharactersFile.CreateDefault();
        var bran = file.Facets[FacetNames.Felucca].Roster[5];
        var catalog = ActionCatalog.From(bran, catalog: null);
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 80, AmbitionWantsHunt = true },
            Location = CharactersFile.DefaultBankSpot,
            Role = CharacterRole.Fighter,
            DiedRecently = true
        };
        var withDeath = ActionScorer.Rank(catalog, situation, new Goal(GoalKind.Hunt, null), seed: 4);
        Assert.Contains(
            withDeath.Ranked,
            a => a.SkillKind == SkillKinds.Hunt && a.Why.Contains("died recently")
        );
    }

    [Theory]
    [InlineData(SkillKinds.Hunt, DayPart.Night, DayShapeRules.NightHuntCut)]
    [InlineData(SkillKinds.Dungeon, DayPart.Night, DayShapeRules.NightHuntCut)]
    [InlineData(SkillKinds.Hunt, DayPart.Evening, DayShapeRules.EveningHuntCut)]
    [InlineData(SkillKinds.Hunt, DayPart.Work, DayShapeRules.WorkActiveBoost)]
    [InlineData(SkillKinds.VendorSell, DayPart.Work, DayShapeRules.WorkActiveBoost)]
    public void Rank_TheHourOfTheDayShapesTheOuting(string skillKind, DayPart part, double weight)
    {
        // The scorer turned each family into a routine word ("graveyard", "despise", "sell")
        // that the day shape never matched, so a night hunt scored as high as a noon one.
        const int seed = 3;
        var candidate = Candidate(skillKind);
        var goal = new Goal(GoalKind.Hunt, GoalRules.NoTarget);
        var jitter = NeedsBrain.Jitter(seed, candidate.Id.Value);

        double ScoreAt(string dayPart) =>
            ActionScorer.Rank(
                [candidate],
                new Situation
                {
                    Needs = new NeedsSnapshot { HitsFraction = 1, DayPart = dayPart },
                    Role = CharacterRole.Fighter,
                    InTownRegion = true,
                    HasHarvestGoods = true
                },
                goal,
                seed
            ).Ranked[0].Score - jitter;

        Assert.Equal(ScoreAt(dayPart: null) * weight, ScoreAt(part.ToString()), precision: 6);
    }

    [Fact]
    public void Score_HuntStepCannotRun_PlanDoesNotWaitOnIt()
    {
        var bran = CharactersFile.CreateDefault().Facets[FacetNames.Felucca].Roster[BranRosterIndex];
        var result = GoalLoop.Score(
            bran,
            FighterBarredFromHunt(currentActionId: null, recentSkillKinds: []),
            new Goal(GoalKind.Hunt, GoalRules.NoTarget),
            BarredHuntSeed,
            catalog: null
        );

        Assert.Contains(
            result.Ranked,
            a => a.SkillKind == SkillKinds.Hunt && !ActionScorer.IsEligible(a)
        );
        Assert.False(GoalPlanRules.MatchesCurrent(result.Plan, SkillKinds.Hunt));
    }

    [Fact]
    public void Score_HuntStepCannotRun_OtherActionsAreNotCutAsOffPlan()
    {
        var bran = CharactersFile.CreateDefault().Facets[FacetNames.Felucca].Roster[BranRosterIndex];
        var result = GoalLoop.Score(
            bran,
            FighterBarredFromHunt(currentActionId: null, recentSkillKinds: []),
            new Goal(GoalKind.Hunt, GoalRules.NoTarget),
            BarredHuntSeed,
            catalog: null
        );

        Assert.DoesNotContain(result.Ranked, a => a.Why == OffPlanWhy);
    }

    private static Situation FighterBarredFromHunt(string currentActionId, string[] recentSkillKinds) =>
        new()
        {
            Needs = new NeedsSnapshot
            {
                HitsFraction = 1,
                Power = BarredFighterPower,
                AmbitionWantsHunt = true,
                Drives = new PersonaDrives(greed: 0.4, caution: 0.3, valor: 0.8, isCustom: true)
            },
            Location = CharactersFile.DefaultBankSpot,
            InTownRegion = true,
            Role = CharacterRole.Fighter,
            AvoidHuntPlace = true,
            NothingToBank = true,
            CurrentActionId = currentActionId,
            RecentSkillKinds = recentSkillKinds
        };

    [Fact]
    public void Rank_TripToDangerSpot_IsIneligible()
    {
        // The walk destination itself sits where the character just fled.
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40 },
            Location = new Point3D(100, 100, 0),
            DangerSpots = [new Point3D(190, 100, 0)]
        };
        var result = ActionScorer.Rank(DangerTripCandidates(), situation, new Goal(GoalKind.Work, null), seed: 1);
        var trip = result.Ranked.First(a => a.SkillKind == SkillKinds.GoTo);

        Assert.False(ActionScorer.IsEligible(trip));
        Assert.Equal("the way there is still dangerous", trip.Why);
    }

    [Fact]
    public void Rank_TripAcrossDangerSpot_IsIneligible()
    {
        // The destination is safe; the straight path passes the remembered spot.
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40 },
            Location = new Point3D(100, 100, 0),
            DangerSpots = [new Point3D(145, 100, 0)]
        };
        var result = ActionScorer.Rank(DangerTripCandidates(), situation, new Goal(GoalKind.Work, null), seed: 1);
        var trip = result.Ranked.First(a => a.SkillKind == SkillKinds.GoTo);

        Assert.False(ActionScorer.IsEligible(trip));
    }

    [Fact]
    public void Rank_TripFarFromDangerSpot_StaysEligible()
    {
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40 },
            Location = new Point3D(100, 100, 0),
            DangerSpots = [new Point3D(300, 300, 0)]
        };
        var result = ActionScorer.Rank(DangerTripCandidates(), situation, new Goal(GoalKind.Work, null), seed: 1);
        var trip = result.Ranked.First(a => a.SkillKind == SkillKinds.GoTo);

        Assert.True(ActionScorer.IsEligible(trip));
    }

    [Fact]
    public void Rank_GoHome_NeverBlockedByDanger()
    {
        // Escape walks stay open no matter where the remembered danger sits.
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40 },
            Location = new Point3D(100, 100, 0),
            BeyondLeash = true,
            DangerSpots = [new Point3D(190, 100, 0)]
        };
        var result = ActionScorer.Rank(
            new List<ActionCandidate> { DangerTripCandidates()[1] },
            situation,
            new Goal(GoalKind.Work, null),
            seed: 1
        );

        Assert.True(ActionScorer.IsEligible(result.Ranked[0]));
    }

    [Fact]
    public void Unavailable_GoHomeWhileAlreadyHome_IsRefused()
    {
        // A model plan can order "go home" while the character is already there.
        // Without this gate the step commits every second forever.
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40 },
            Location = new Point3D(605, 888, 0),
            BeyondLeash = false,
            RecentRuns = 0
        };

        Assert.Equal(
            "already near home",
            ActionScorer.Unavailable(DangerTripCandidates()[1], situation, catalog: null)
        );
    }

    [Theory]
    [InlineData(SkillKinds.Mount, "no mount in reach")]
    [InlineData(SkillKinds.BuyMount, "cannot buy a mount")]
    public void Unavailable_MountStepThatCannotWork_IsRefused(string skillKind, string reason)
    {
        // Mount failed at once with no horse near, and a poor buyer walked to the stable
        // and back: over a thousand failed mount steps in ninety minutes.
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40 },
            Location = new Point3D(100, 100, 0),
            CanMount = false,
            CanBuyMount = false
        };
        var candidate = new ActionCandidate
        {
            Id = new ActionId($"life:{skillKind}:0"),
            SkillKind = skillKind,
            RoutineId = "life",
            Step = new SkillStepDefinition { Skill = skillKind }
        };

        Assert.Equal(reason, ActionScorer.Unavailable(candidate, situation, catalog: null));
    }

    [Theory]
    [InlineData(CharacterRole.Worker, "a worker does not delve")]
    [InlineData(CharacterRole.Fighter, null)]
    public void Unavailable_Dungeon_OnlyForFighters(CharacterRole role, string reason)
    {
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 90 },
            Location = new Point3D(100, 100, 0),
            Role = role
        };
        var candidate = new ActionCandidate
        {
            Id = new ActionId("delve:Dungeon:0"),
            SkillKind = SkillKinds.Dungeon,
            RoutineId = "delve",
            Step = new SkillStepDefinition { Skill = SkillKinds.Dungeon }
        };

        Assert.Equal(reason, ActionScorer.Unavailable(candidate, situation, catalog: null, new Goal(GoalKind.Dungeon, null)));
    }

    [Theory]
    [InlineData(HallBar + 10, null)]
    [InlineData(0, "below required power")]
    public void Unavailable_Delve_HeldToTheHealthyPower(int healthyPower, string reason)
    {
        // The hall was picked for the fighter once healed; a scratch took it away again.
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 0.8, Power = HallBar - 10, HealthyPower = healthyPower },
            Location = new Point3D(100, 100, 0),
            Role = CharacterRole.Fighter
        };
        var candidate = new ActionCandidate
        {
            Id = new ActionId(ActionCatalog.CatalogDelveId),
            SkillKind = SkillKinds.Dungeon,
            RoutineId = ActionCatalog.CatalogDelveRoutine,
            Step = new SkillStepDefinition { Skill = SkillKinds.Dungeon },
            RequiredPower = HallBar
        };

        Assert.Equal(reason, ActionScorer.Unavailable(candidate, situation, catalog: null, new Goal(GoalKind.Dungeon, null)));
    }

    [Theory]
    [InlineData(false, "nothing to buy in reach")]
    [InlineData(true, null)]
    public void Unavailable_ShoppingTrip_OnlyWithAnErrandInReach(bool errand, string reason)
    {
        // Six hundred shopping trips in half an hour walked to a counter with nothing to buy
        // there, or no gold to pay, and failed.
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 90 },
            Location = new Point3D(100, 100, 0),
            Role = CharacterRole.Fighter,
            InTownRegion = true,
            HasShoppingErrand = errand
        };
        var candidate = new ActionCandidate
        {
            Id = new ActionId(ActionCatalog.RestockId),
            SkillKind = SkillKinds.VendorBuy,
            RoutineId = ActionCatalog.RestockRoutine,
            Step = new SkillStepDefinition { Skill = SkillKinds.VendorBuy }
        };

        Assert.Equal(reason, ActionScorer.Unavailable(candidate, situation, catalog: null, JobRules.GoalFor(JobKind.Shop)));
    }

    [Fact]
    public void Unavailable_MountWithAHorseInReach_IsNull()
    {
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40 },
            Location = new Point3D(100, 100, 0),
            CanMount = true
        };
        var candidate = new ActionCandidate
        {
            Id = new ActionId("life:Mount:0"),
            SkillKind = SkillKinds.Mount,
            RoutineId = "life",
            Step = new SkillStepDefinition { Skill = SkillKinds.Mount }
        };

        Assert.Null(ActionScorer.Unavailable(candidate, situation, catalog: null));
    }

    [Fact]
    public void Unavailable_EligibleStep_IsNull()
    {
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40 },
            Location = new Point3D(100, 100, 0)
        };

        Assert.Null(ActionScorer.Unavailable(DangerTripCandidates()[0], situation, catalog: null));
    }

    [Fact]
    public void Rank_TripToUnreachableGoal_IsIneligible()
    {
        // The router proved no node sits near the destination; the scorer stops
        // every skill that aims there, not just the one that failed.
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40 },
            Location = new Point3D(100, 100, 0),
            UnreachableGoals = [new Point3D(200, 100, 0)]
        };
        var result = ActionScorer.Rank(DangerTripCandidates(), situation, new Goal(GoalKind.Work, null), seed: 1);
        var trip = result.Ranked.First(a => a.SkillKind == SkillKinds.GoTo);

        Assert.False(ActionScorer.IsEligible(trip));
        Assert.Equal("no way to get there", trip.Why);
    }

    [Fact]
    public void Rank_TripFarFromUnreachableGoal_StaysEligible()
    {
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40 },
            Location = new Point3D(100, 100, 0),
            UnreachableGoals = [new Point3D(300, 300, 0)]
        };
        var result = ActionScorer.Rank(DangerTripCandidates(), situation, new Goal(GoalKind.Work, null), seed: 1);
        var trip = result.Ranked.First(a => a.SkillKind == SkillKinds.GoTo);

        Assert.True(ActionScorer.IsEligible(trip));
    }

    [Fact]
    public void Rank_GoHome_NeverBlockedByUnreachable()
    {
        // Escape walks stay open: going home is how a stranded character recovers.
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40 },
            Location = new Point3D(100, 100, 0),
            BeyondLeash = true,
            UnreachableGoals = [new Point3D(50, 100, 0)]
        };
        var result = ActionScorer.Rank(
            new List<ActionCandidate> { DangerTripCandidates()[1] },
            situation,
            new Goal(GoalKind.Work, null),
            seed: 1
        );

        Assert.True(ActionScorer.IsEligible(result.Ranked[0]));
    }

    [Fact]
    public void Unavailable_FarFromHome_TripsStayOpen()
    {
        // The leash forbade every action but going home, so a trip to another town or a
        // far hunt ground ended the moment the person got there.
        var hunt = Candidate(SkillKinds.Hunt);
        var situation = FarAway();

        Assert.Null(ActionScorer.Unavailable(hunt, situation, catalog: null, JobRules.GoalFor(JobKind.Hunt)));
        Assert.Null(ActionScorer.Unavailable(Candidate(SkillKinds.Tavern), situation, catalog: null, JobRules.GoalFor(JobKind.Travel)));
    }

    [Fact]
    public void Unavailable_House_AnOwnerGoesHomeOnlyForItsVendor()
    {
        // Owners never placed a vendor in a house they already had: the house routine was
        // closed to anyone who owned one, so no player vendor stood on the shard.
        var owner = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = FullHits, Power = 40 },
            Location = new Point3D(840, 331, 0),
            HasHouse = true,
            HasVendor = true
        };

        Assert.Equal("already has a house", ActionScorer.Unavailable(Candidate(SkillKinds.House), owner, catalog: null));
        Assert.Null(ActionScorer.Unavailable(Candidate(SkillKinds.House), owner with { HasVendor = false }, catalog: null));
        Assert.Null(ActionScorer.Unavailable(Candidate(SkillKinds.House), owner with { VendorErrand = true }, catalog: null));
    }

    private const double FullHits = 1.0;
    private const double ScratchedHits = 0.9;

    [Theory]
    [InlineData(FullHits, "hits are full")]
    [InlineData(ScratchedHits, null)]
    public void Unavailable_Rest_OnlyWhileHitsAreMissing(double hitsFraction, string reason)
    {
        // A rest ends the moment the hits are full: picked at full hits, a red rested 150
        // times in five minutes on one tile, each rest over in the second it began.
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = hitsFraction, Power = 40 },
            Location = new Point3D(840, 331, 0)
        };

        Assert.Equal(reason, ActionScorer.Unavailable(Candidate(SkillKinds.Rest), situation, catalog: null));
    }

    [Fact]
    public void Unavailable_FarFromHome_IdlingWaitsUntilHomeOutOfTown()
    {
        var idle = JobRules.GoalFor(JobKind.Idle);

        Assert.Equal(
            "idles near home only",
            ActionScorer.Unavailable(Candidate(SkillKinds.Loiter), FarAway() with { InTownRegion = false }, catalog: null, idle)
        );
        Assert.Null(ActionScorer.Unavailable(Candidate(SkillKinds.Loiter), FarAway(), catalog: null, idle));
    }

    [Fact]
    public void Unavailable_GoHome_InAFarTown_StaysThere()
    {
        // A traveller who came to see Trinsic walked straight home to Yew before anything
        // else; a hundred and fifty people at a time were on their way home.
        Assert.Equal(
            "stays in the town it is in",
            ActionScorer.Unavailable(Candidate(SkillKinds.GoHome), FarAway(), catalog: null, JobRules.GoalFor(JobKind.Tavern))
        );
        Assert.Null(
            ActionScorer.Unavailable(
                Candidate(SkillKinds.GoHome),
                FarAway() with { InTownRegion = false },
                catalog: null,
                JobRules.GoalFor(JobKind.Tavern)
            )
        );
    }

    [Fact]
    public void Unavailable_GoHome_EscapesAThreatWhenTheFleeIsCoolingDown()
    {
        // With the flee cooling down and a threat in sight every action was barred, and the
        // fallback wander failed at the threat every four seconds for an hour.
        var cornered = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40 },
            Location = new Point3D(4546, 1323, 8),
            MustFlee = true,
            BlockedSkillKinds = [SkillKinds.Flee],
            DistanceFromHome = 150
        };

        Assert.Null(ActionScorer.Unavailable(Candidate(SkillKinds.GoHome), cornered, catalog: null, new Goal(GoalKind.Flee, null)));
        Assert.Equal(
            "a threat is in sight",
            ActionScorer.Unavailable(
                Candidate(SkillKinds.GoHome),
                cornered with { BlockedSkillKinds = [] },
                catalog: null,
                new Goal(GoalKind.Flee, null)
            )
        );
    }

    [Theory]
    [InlineData(false, "no place in the bank crowd")]
    [InlineData(true, null)]
    public void Unavailable_BankCrowd_OnlyWithAPlaceThatFits(bool open, string reason)
    {
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1 },
            InTownRegion = true,
            BankCrowdOpen = open
        };

        Assert.Equal(
            reason,
            ActionScorer.Unavailable(Candidate(SkillKinds.BankCrowd), situation, catalog: null, JobRules.GoalFor(JobKind.Bank))
        );
    }

    [Fact]
    public void Rank_Meditate_OnlyForACasterLowOnMana()
    {
        var candidates = new List<ActionCandidate> { Candidate(SkillKinds.Meditate), Candidate(SkillKinds.Tavern) };
        var rested = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1 },
            InTownRegion = true,
            IsCaster = true,
            ManaFraction = 1
        };
        var drained = rested with { ManaFraction = SelfCareRules.MeditateBelowManaFraction / 2 };
        var goal = JobRules.GoalFor(JobKind.Tavern);

        Assert.Equal("mana is fine", ActionScorer.Unavailable(candidates[0], rested, catalog: null, goal));
        Assert.Equal("mana is fine", ActionScorer.Unavailable(candidates[0], drained with { IsCaster = false }, catalog: null, goal));
        Assert.Equal(SkillKinds.Meditate, ActionScorer.Rank(candidates, drained, goal, seed: 1).Winner.SkillKind);
    }

    [Fact]
    public void Rank_FarFromHomeOnATrip_DoesNotPullHome()
    {
        var wild = FarAway() with { InTownRegion = false };
        var candidates = new List<ActionCandidate> { Candidate(SkillKinds.GoHome), Candidate(SkillKinds.Tavern) };
        var onTrip = ActionScorer.Rank(candidates, wild, JobRules.GoalFor(JobKind.Travel), seed: 1);
        var noTrip = ActionScorer.Rank(candidates, wild, JobRules.GoalFor(JobKind.Tavern), seed: 1);
        var homeOnTrip = onTrip.Ranked.First(a => a.SkillKind == SkillKinds.GoHome).Score;
        var homeNoTrip = noTrip.Ranked.First(a => a.SkillKind == SkillKinds.GoHome).Score;

        Assert.True(homeNoTrip > homeOnTrip);
    }

    [Fact]
    public void Unavailable_GoHome_OpensForATownJobOutOfTown()
    {
        var outOfTown = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1 },
            InTownRegion = false,
            BeyondLeash = false
        };
        var home = Candidate(SkillKinds.GoHome);

        Assert.Null(ActionScorer.Unavailable(home, outOfTown, catalog: null, JobRules.GoalFor(JobKind.Bank)));
        Assert.Equal(
            "already near home",
            ActionScorer.Unavailable(home, outOfTown with { InTownRegion = true }, catalog: null, JobRules.GoalFor(JobKind.Bank))
        );
        Assert.Equal(
            "already near home",
            ActionScorer.Unavailable(home, outOfTown, catalog: null, JobRules.GoalFor(JobKind.Hunt))
        );
    }

    [Fact]
    public void Unavailable_Travel_OnlyOnATripAndUntilThere()
    {
        var bank = new Point3D(2500, 560, 0);
        var trip = new ActionCandidate
        {
            Id = new ActionId(TownTripRules.IdPrefix + "Minoc Bank"),
            SkillKind = SkillKinds.Travel,
            RoutineId = TownTripRules.RoutineId,
            Step = new SkillStepDefinition { Skill = SkillKinds.Travel, Target = bank }
        };
        var atHome = new Situation { Needs = new NeedsSnapshot { HitsFraction = 1 }, Location = CharactersFile.DefaultBankSpot };

        Assert.Equal("not on a trip", ActionScorer.Unavailable(trip, atHome, catalog: null, JobRules.GoalFor(JobKind.Bank)));
        Assert.Null(ActionScorer.Unavailable(trip, atHome, catalog: null, JobRules.GoalFor(JobKind.Travel)));
        Assert.Equal(
            "already there",
            ActionScorer.Unavailable(trip, atHome with { Location = bank }, catalog: null, JobRules.GoalFor(JobKind.Travel))
        );
    }

    [Fact]
    public void Rank_Arrive_OnlyAsTheJobsStep()
    {
        var candidates = new List<ActionCandidate> { Candidate(SkillKinds.Arrive), Candidate(SkillKinds.Tavern) };
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1 },
            InTownRegion = true,
            Location = CharactersFile.DefaultBankSpot
        };
        var goal = JobRules.GoalFor(JobKind.Bank);
        var free = ActionScorer.Rank(candidates, situation, goal, seed: 1);
        var bankSteps = GoalPlanRules.Restore(JobRules.BankPlan, 0, 0).Steps;
        var arriveStep = bankSteps.ToList().FindIndex(step => step.SkillKind == SkillKinds.Arrive);
        var planned = ActionScorer.Rank(candidates, situation, goal, seed: 1, plan: GoalPlanRules.Restore(JobRules.BankPlan, arriveStep, 0));

        Assert.False(ActionScorer.IsEligible(free.Ranked.First(a => a.SkillKind == SkillKinds.Arrive)));
        Assert.Equal(SkillKinds.Arrive, planned.Winner.SkillKind);
    }

    [Fact]
    public void Rank_PartyFollow_WinsOnlyInARealParty()
    {
        var follow = new ActionCandidate
        {
            Id = new ActionId(ActionCatalog.PartyFollowId),
            SkillKind = SkillKinds.Follow,
            RoutineId = "party",
            Step = new SkillStepDefinition { Skill = SkillKinds.Follow }
        };
        var candidates = new List<ActionCandidate> { follow, Candidate(SkillKinds.Tavern) };
        var alone = new Situation { Needs = new NeedsSnapshot { HitsFraction = 1 }, InTownRegion = true };
        var partied = alone with { FollowsPartyLeader = true };
        var goal = JobRules.GoalFor(JobKind.Tavern);

        var soloResult = ActionScorer.Rank(candidates, alone, goal, seed: 1);
        var partyResult = ActionScorer.Rank(candidates, partied, goal, seed: 1);

        Assert.False(ActionScorer.IsEligible(soloResult.Ranked.First(a => a.Id.Value == ActionCatalog.PartyFollowId)));
        Assert.Equal(ActionCatalog.PartyFollowId, partyResult.Winner.Id.Value);
    }

    [Fact]
    public void Rank_CrewFollow_WaitsWhileTheCrewIsBrokenUp()
    {
        var crewFollow = Candidate(SkillKinds.Follow);
        var partyFollow = new ActionCandidate
        {
            Id = new ActionId(ActionCatalog.PartyFollowId),
            SkillKind = SkillKinds.Follow,
            RoutineId = "party",
            Step = new SkillStepDefinition { Skill = SkillKinds.Follow }
        };
        var candidates = new List<ActionCandidate> { crewFollow, partyFollow, Candidate(SkillKinds.Tavern) };
        var gathered = new Situation { Needs = new NeedsSnapshot { HitsFraction = 1 }, InTownRegion = true, FollowsPartyLeader = true };
        var brokenUp = gathered with { CrewDisbanded = true };
        var goal = JobRules.GoalFor(JobKind.Tavern);

        var gatheredResult = ActionScorer.Rank(candidates, gathered, goal, seed: 1);
        var brokenUpResult = ActionScorer.Rank(candidates, brokenUp, goal, seed: 1);
        var brokenUpCrew = brokenUpResult.Ranked.First(a => a.Id == crewFollow.Id);

        Assert.True(ActionScorer.IsEligible(gatheredResult.Ranked.First(a => a.Id == crewFollow.Id)));
        Assert.False(ActionScorer.IsEligible(brokenUpCrew));
        Assert.Equal(ActionScorer.CrewDisbandedWhy, brokenUpCrew.Why);
        Assert.True(ActionScorer.IsEligible(brokenUpResult.Ranked.First(a => a.Id == partyFollow.Id)));
    }

    private static Situation FarAway() =>
        new()
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, Power = 40 },
            Location = new Point3D(2500, 560, 0),
            InTownRegion = true,
            Role = CharacterRole.Fighter,
            BeyondLeash = true,
            DistanceFromHome = 1200
        };

    [Fact]
    public void Unavailable_RedOutOfTownInsideTheLeash_RidesBackToTheDen()
    {
        // A red farming the ground by the Den stood inside the leash and out of town: going
        // home was "already near home", and its bank step outside town had no way to start.
        var red = OutOfTownNearHome(DispositionKind.Outlaw);

        Assert.Null(ActionScorer.Unavailable(Candidate(SkillKinds.GoHome), red, catalog: null, PkSeek));
    }

    [Fact]
    public void Unavailable_BlueSeekerOutOfTownInsideTheLeash_StaysOut() =>
        Assert.Equal(
            "already near home",
            ActionScorer.Unavailable(Candidate(SkillKinds.GoHome), OutOfTownNearHome(DispositionKind.Lawful), null, PkSeek)
        );

    [Fact]
    public void Unavailable_RedInTheDen_HasNoWalkHome() =>
        Assert.NotNull(
            ActionScorer.Unavailable(
                Candidate(SkillKinds.GoHome),
                OutOfTownNearHome(DispositionKind.Outlaw) with { InTownRegion = true },
                null,
                PkSeek
            )
        );

    [Fact]
    public void Unavailable_HurtRed_DoesNotRideOutToTheHotSpots() =>
        Assert.Equal(
            "recovering",
            ActionScorer.Unavailable(Candidate(SkillKinds.Conflict), OutOfTownNearHome(DispositionKind.Outlaw), null, Recovering)
        );

    [Fact]
    public void Unavailable_HurtBlueWithAPkReport_StillAnswersIt() =>
        Assert.Null(
            ActionScorer.Unavailable(
                Candidate(SkillKinds.Conflict),
                OutOfTownNearHome(DispositionKind.Lawful) with { HasPkReport = true },
                null,
                Recovering
            )
        );

    private static readonly Goal PkSeek = new(GoalKind.Pk, GoalRules.NoTarget);
    private static readonly Goal Recovering = new(GoalKind.Recover, GoalRules.NoTarget);

    private static Situation OutOfTownNearHome(DispositionKind disposition) =>
        new()
        {
            Needs = new NeedsSnapshot { HitsFraction = 1 },
            Location = new Point3D(2650, 2250, 0),
            InTownRegion = false,
            BeyondLeash = false,
            Role = CharacterRole.Fighter,
            Disposition = disposition
        };

    private static ActionCandidate Candidate(string skillKind)
    {
        var step = new SkillStepDefinition { Skill = skillKind };
        return new ActionCandidate
        {
            Id = ActionId.From(skillKind.ToLowerInvariant(), step, 0),
            SkillKind = skillKind,
            RoutineId = skillKind.ToLowerInvariant(),
            Step = step
        };
    }

    private static List<ActionCandidate> DangerTripCandidates()
    {
        var goTo = new SkillStepDefinition { Skill = SkillKinds.GoTo, Target = new Point3D(200, 100, 0) };
        var goHome = new SkillStepDefinition { Skill = SkillKinds.GoHome, Target = new Point3D(50, 100, 0) };
        return
        [
            new ActionCandidate
            {
                Id = ActionId.From("errands", goTo, 0),
                SkillKind = SkillKinds.GoTo,
                RoutineId = "errands",
                Step = goTo
            },
            new ActionCandidate
            {
                Id = ActionId.From("errands", goHome, 1),
                SkillKind = SkillKinds.GoHome,
                RoutineId = "errands",
                Step = goHome
            }
        ];
    }

    [Fact]
    public void DungeonScore_ScalesWithTheShardsPull()
    {
        var needs = new NeedsSnapshot { HitsFraction = 1 };
        var pulled = new Situation { Needs = needs, DungeonDemand = DungeonShareRules.MaxDemand };

        Assert.Equal(2 * DungeonShareRules.MaxDemand, ActionScorer.DungeonScore(2, pulled, needs), precision: 6);
        Assert.Equal(2, ActionScorer.DungeonScore(2, null, needs), precision: 6);
    }

    [Fact]
    public void DungeonScore_AWellFighterInADungeonFightsOn()
    {
        var healthy = new NeedsSnapshot { HitsFraction = 1 };
        var hurt = new NeedsSnapshot { HitsFraction = 0.4 };
        var inside = new Situation { Needs = healthy, InDungeon = true };

        Assert.Equal(1 + DungeonShareRules.InDungeonScoreBoost, ActionScorer.DungeonScore(1, inside, healthy), precision: 6);
        Assert.Equal(1, ActionScorer.DungeonScore(1, inside with { Needs = hurt }, hurt), precision: 6);
        Assert.Equal(1, ActionScorer.DungeonScore(1, inside with { SuppliesLow = true }, healthy), precision: 6);
    }
}
