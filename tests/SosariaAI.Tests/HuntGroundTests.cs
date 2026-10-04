using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A fighter hunts near its own home, at a place its power can handle, and looks for
/// the prey its ambition names first. A veteran leaves novice ground to the novices.
/// </summary>
public class HuntGroundTests
{
    private const int Leash = 400;
    private const int FighterPower = 80;
    private const int Seed = 7;
    private const int BranRosterIndex = 5;
    private const string HuntKind = "Hunt";
    private const string Skeleton = "Skeleton";
    private const string Troll = "Troll";
    private const string Ettin = "Ettin";
    private const string Rat = "Rat";
    private const string Chest = "TreasureChestLevel2";
    private const string Guildmaster = "RangerGuildmaster";
    private const int EasyDifficulty = 24;
    private const int FairDifficulty = 74;
    private const int HardDifficulty = 160;
    private const int VeteranPower = 250;
    private const int GraveyardDifficulty = 110;
    private const int TrollWoodsDifficulty = 250;
    private const SkillTier Tier = SkillTier.Novice;
    private static readonly Point3D Home = new(1840, 2750, 0);
    private static readonly Point3D NearHome = new(1900, 2800, 0);
    private static readonly Point3D AlsoNearHome = new(1750, 2700, 0);
    private static readonly Point3D FarAway = new(1384, 1492, 0);

    /// <summary>Open woods a short walk south of the Yew moongate, far from Trinsic and the Den.</summary>
    private static readonly Point3D ByYewGate = new(790, 800, 0);

    [Fact]
    public void Pick_IgnoresAPlaceBeyondTheLeash()
    {
        var catalog = Catalog(Place(Skeleton, FarAway, EasyDifficulty), Place(Ettin, NearHome, FairDifficulty));

        var ground = HuntGround.Pick(catalog, Home, Leash, FighterPower, Tier, Skeleton, Seed, IsMonster);

        Assert.Equal(Ettin, ground.Role);
    }

    [Fact]
    public void Pick_ADenHome_ReachesGroundByTheMoongates()
    {
        // Buccaneer's Den is an island with only its town's rats on it: a red there steps
        // through the Den's gate, so ground by the Yew gate is within its leash.
        var catalog = Catalog(Place(Ettin, ByYewGate, FairDifficulty));

        var ground = HuntGround.Pick(catalog, PkRules.BucsDenHaven, Leash, FighterPower, Tier, prey: null, Seed, IsMonster);

        Assert.Equal(Ettin, ground?.Role);
        Assert.NotNull(HuntGround.GatesOutOf(PkRules.BucsDenHaven));
    }

    [Fact]
    public void Pick_AHomeWalkedOutOf_KeepsItsLeash()
    {
        // Only the Den is left by its gate: a Trinsic fighter keeps to the ground near Trinsic.
        var catalog = Catalog(Place(Ettin, ByYewGate, FairDifficulty));

        Assert.Null(HuntGround.Pick(catalog, Home, Leash, FighterPower, Tier, prey: null, Seed, IsMonster));
        Assert.Null(HuntGround.GatesOutOf(Home));
    }

    [Fact]
    public void Pick_PrefersTheAmbitionPrey()
    {
        var catalog = Catalog(Place(Ettin, NearHome, FairDifficulty), Place(Skeleton, AlsoNearHome, EasyDifficulty));

        var ground = HuntGround.Pick(catalog, Home, Leash, FighterPower, Tier, "skeleton", Seed, IsMonster);

        Assert.Equal(Skeleton, ground.Role);
    }

    [Fact]
    public void Pick_SkipsAPlaceTooHardForThePower()
    {
        var catalog = Catalog(Place(Troll, NearHome, HardDifficulty), Place(Skeleton, AlsoNearHome, EasyDifficulty));

        var ground = HuntGround.Pick(catalog, Home, Leash, FighterPower, Tier, "troll", Seed, IsMonster);

        Assert.Equal(Skeleton, ground.Role);
    }

    [Fact]
    public void Pick_SkipsAPlaceWithNoKnownDifficulty()
    {
        var catalog = Catalog(Place(Chest, NearHome, difficulty: 0));

        Assert.Null(HuntGround.Pick(catalog, Home, Leash, FighterPower, Tier, prey: null, Seed, IsMonster));
    }

    [Fact]
    public void Pick_SkipsAPlaceWhoseSpawnIsNoEnemy()
    {
        var catalog = Catalog(Place(Guildmaster, NearHome, EasyDifficulty), Place(Ettin, AlsoNearHome, FairDifficulty));

        var ground = HuntGround.Pick(catalog, Home, Leash, FighterPower, Tier, prey: null, Seed, IsMonster);

        Assert.Equal(Ettin, ground.Role);
    }

    [Fact]
    public void Pick_NothingInReach_IsNull()
    {
        var catalog = Catalog(Place(Skeleton, FarAway, EasyDifficulty));

        Assert.Null(HuntGround.Pick(catalog, Home, Leash, FighterPower, Tier, Skeleton, Seed, IsMonster));
    }

    [Fact]
    public void AtPlace_MovesTheHuntWithoutItsCrew()
    {
        var authored = new SkillStepDefinition
        {
            Skill = SkillKinds.Hunt,
            Target = FarAway,
            Area = AreaDefinition.FromName(CharactersFile.AreaTrollWoods),
            Minutes = CharactersFile.TrollHuntMinutes,
            Party = "hunters"
        };

        var moved = HuntGround.AtPlace(authored, Place(Ettin, NearHome, FairDifficulty), keepsCrew: false);

        Assert.Null(moved.Party);
        Assert.Equal(NearHome, moved.Target);
        Assert.True(moved.Area.ToRectangle().Contains(NearHome));
        Assert.Equal(authored.Minutes, moved.Minutes);
        Assert.Equal(FarAway, authored.Target);
    }

    [Fact]
    public void ActionCatalog_HomeFarFromTheAuthoredHunt_HuntsAtTheGround()
    {
        var bran = CharactersFile.CreateDefault().Facets[FacetNames.Felucca].Roster[BranRosterIndex];
        var ground = Place(Ettin, NearHome, FairDifficulty);

        var actions = ActionCatalog.From(bran, catalog: null, new HuntHome(Home, Leash, ground));

        Assert.All(
            actions.FindAll(a => a.SkillKind == SkillKinds.Hunt),
            hunt => Assert.Equal(NearHome, hunt.Step.Target)
        );
    }

    [Fact]
    public void ActionCatalog_HomeNearTheAuthoredHunt_KeepsTheAuthoredPlace()
    {
        var bran = CharactersFile.CreateDefault().Facets[FacetNames.Felucca].Roster[BranRosterIndex];
        var ground = Place(Ettin, NearHome, FairDifficulty);

        var actions = ActionCatalog.From(bran, catalog: null, new HuntHome(CharactersFile.DefaultBankSpot, Leash, ground));

        Assert.Contains(actions, a => a.SkillKind == SkillKinds.Hunt && a.Step.Target == CharactersFile.GraveyardGoPoint);
    }

    [Fact]
    public void Pick_TownLifeIsNeverAHunt()
    {
        var catalog = Catalog(Place(Rat, NearHome, EasyDifficulty), Place(Ettin, AlsoNearHome, FairDifficulty));

        var ground = HuntGround.Pick(catalog, Home, Leash, FighterPower, Tier, prey: null, Seed, IsMonster);

        Assert.Equal(Ettin, ground.Role);
    }

    [Fact]
    public void Pick_PrefersLivePreyOverAnEmptyField()
    {
        var catalog = Catalog(Place(Skeleton, NearHome, EasyDifficulty), Place(Ettin, AlsoNearHome, FairDifficulty));

        var ground = HuntGround.Pick(
            catalog,
            Home,
            Leash,
            FighterPower,
            Tier,
            prey: null,
            Seed,
            IsMonster,
            place => place.Arrival == AlsoNearHome ? GroundState.Live : GroundState.Empty
        );

        Assert.Equal(Ettin, ground.Role);
    }

    [Fact]
    public void Pick_AnEmptyFieldBeatsNothing_AClosedOneNever()
    {
        var catalog = Catalog(Place(Skeleton, NearHome, EasyDifficulty), Place(Ettin, AlsoNearHome, FairDifficulty));

        var ground = HuntGround.Pick(
            catalog,
            Home,
            Leash,
            FighterPower,
            Tier,
            prey: null,
            Seed,
            IsMonster,
            place => place.Arrival == AlsoNearHome ? GroundState.Closed : GroundState.Empty
        );

        Assert.Equal(Skeleton, ground.Role);
        Assert.Null(HuntGround.Pick(catalog, Home, Leash, FighterPower, Tier, null, Seed, IsMonster, _ => GroundState.Closed));
    }

    [Fact]
    public void Pick_LooksAtOneSpawnerSpotOnce()
    {
        var catalog = Catalog(Place(Skeleton, NearHome, EasyDifficulty), Place(Ettin, NearHome, FairDifficulty));
        var looks = 0;

        HuntGround.Pick(catalog, Home, Leash, FighterPower, Tier, null, Seed, IsMonster, _ =>
        {
            looks++;
            return GroundState.Live;
        });

        Assert.Equal(1, looks);
    }

    [Fact]
    public void Pick_LooksAtNoMoreSpotsThanTheBudget()
    {
        const int Spots = HuntGround.MaxGroundLooks * 2;
        var places = new Destination[Spots];

        for (var i = 0; i < Spots; i++)
        {
            places[i] = Place(Skeleton, new Point3D(Home.X + i + 1, Home.Y, 0), EasyDifficulty);
        }

        var looks = 0;

        HuntGround.Pick(Catalog(places), Home, Leash, FighterPower, Tier, null, Seed, IsMonster, _ =>
        {
            looks++;
            return GroundState.Empty;
        });

        Assert.Equal(HuntGround.MaxGroundLooks, looks);
    }

    [Fact]
    public void Pick_AVeteranLeavesNoviceGroundToTheNovices()
    {
        // The Britain graveyard drew decked-out veterans killing zombies next to the novices.
        var catalog = Catalog(Place(Skeleton, NearHome, GraveyardDifficulty), Place(Troll, AlsoNearHome, TrollWoodsDifficulty));

        Assert.Equal(Troll, HuntGround.Pick(catalog, Home, Leash, VeteranPower, SkillTier.Grandmaster, null, Seed, IsMonster).Role);
        Assert.Equal(Skeleton, HuntGround.Pick(catalog, Home, Leash, FighterPower + FighterPower / 4, SkillTier.Journeyman, null, Seed, IsMonster).Role);
    }

    [Fact]
    public void Pick_AVeteranStillHuntsNoviceGround_WhenNothingElseIsNear()
    {
        var catalog = Catalog(Place(Skeleton, NearHome, GraveyardDifficulty), Place(Troll, FarAway, TrollWoodsDifficulty));

        Assert.Equal(Skeleton, HuntGround.Pick(catalog, Home, Leash, VeteranPower, SkillTier.Grandmaster, null, Seed, IsMonster).Role);
    }

    [Fact]
    public void Pick_TheAmbitionPreyIsHuntedEvenFarBelow()
    {
        var catalog = Catalog(Place(Skeleton, NearHome, GraveyardDifficulty), Place(Troll, AlsoNearHome, TrollWoodsDifficulty));

        Assert.Equal(Skeleton, HuntGround.Pick(catalog, Home, Leash, VeteranPower, SkillTier.Grandmaster, "skeleton", Seed, IsMonster).Role);
    }

    [Fact]
    public void Reach_TakesOnSpotsAboveThePower_ButNotPastTheReach()
    {
        Assert.True(HuntGround.InReach(FighterPower + 1, FighterPower));
        Assert.True(HuntGround.InReach(HuntGround.Reach(FighterPower), FighterPower));
        Assert.False(HuntGround.InReach(HuntGround.Reach(FighterPower) + 1, FighterPower));
        Assert.False(HuntGround.InReach(0, FighterPower));
        Assert.False(HuntGround.InReach(EasyDifficulty, 0));
        Assert.Equal(0, HuntGround.PowerBar(0));
        Assert.True(HuntGround.InReach(TrollWoodsDifficulty, HuntGround.PowerBar(TrollWoodsDifficulty)));
    }

    [Fact]
    public void FarBelow_RisesWithTheTier()
    {
        Assert.False(HuntGround.FarBelow(GraveyardDifficulty, VeteranPower, SkillTier.Novice));
        Assert.True(HuntGround.FarBelow(GraveyardDifficulty, VeteranPower, SkillTier.Grandmaster));
        Assert.False(HuntGround.FarBelow(TrollWoodsDifficulty, VeteranPower, SkillTier.Grandmaster));
        Assert.True(HuntGround.FarBelowShare(SkillTier.Grandmaster) < HuntGround.IdealShare(SkillTier.Novice) * 2);
    }

    [Fact]
    public void Fit_PeaksAtTheTiersIdeal_AndIsNothingOutOfReach()
    {
        var ideal = (int)System.Math.Round(VeteranPower * HuntGround.PowerReach * HuntGround.IdealShare(SkillTier.Grandmaster));

        Assert.Equal(1.0, HuntGround.Fit(ideal, VeteranPower, SkillTier.Grandmaster), precision: 2);
        Assert.True(HuntGround.Fit(TrollWoodsDifficulty / 2, VeteranPower, SkillTier.Grandmaster) < HuntGround.Fit(ideal, VeteranPower, SkillTier.Grandmaster));
        Assert.Equal(HuntGround.FarBelowFit, HuntGround.Fit(EasyDifficulty, VeteranPower, SkillTier.Grandmaster));
        Assert.Equal(0.0, HuntGround.Fit(HuntGround.Reach(VeteranPower) + 1, VeteranPower, SkillTier.Grandmaster));
    }

    [Fact]
    public void ForHome_AVeteranLeavesTheAuthoredGraveyard_ACrewKeepsIt()
    {
        var graveyard = Place(Skeleton, NearHome, GraveyardDifficulty);
        var ground = Place(Troll, AlsoNearHome, TrollWoodsDifficulty);
        var hunt = new HuntHome(Home, Leash, ground, Power: VeteranPower, Tier: SkillTier.Grandmaster, Catalog: Catalog(graveyard, ground));
        var authored = new SkillStepDefinition { Skill = SkillKinds.Hunt, Target = NearHome, Minutes = CharactersFile.GraveyardHuntMinutes };
        var crew = new SkillStepDefinition { Skill = SkillKinds.Hunt, Target = NearHome, Party = CharactersFile.PartyGraveyardCrew };
        var novice = hunt with { Power = FighterPower, Tier = SkillTier.Novice };

        Assert.Equal(AlsoNearHome, HuntGround.ForHome(authored, hunt).Target);
        Assert.Equal(NearHome, HuntGround.ForHome(crew, hunt).Target);
        Assert.Equal(NearHome, HuntGround.ForHome(authored, novice).Target);
        Assert.Equal(GraveyardDifficulty, HuntGround.DifficultyAt(hunt.Catalog, NearHome));
        Assert.Equal(0, HuntGround.DifficultyAt(hunt.Catalog, FarAway));
    }

    [Fact]
    public void StateOf_GuardsOrADryRunClose_PreyMakesItLive()
    {
        const int SomePrey = 3;

        Assert.Equal(GroundState.Closed, HuntGround.StateOf(guarded: true, dry: false, SomePrey));
        Assert.Equal(GroundState.Closed, HuntGround.StateOf(guarded: false, dry: true, SomePrey));
        Assert.Equal(GroundState.Live, HuntGround.StateOf(guarded: false, dry: false, SomePrey));
        Assert.Equal(GroundState.Empty, HuntGround.StateOf(guarded: false, dry: false, preyCount: 0));
    }

    [Fact]
    public void AreaAround_CoversTheSpawnerWalk()
    {
        var area = HuntGround.AreaAround(NearHome).ToRectangle();

        Assert.True(area.Contains(new Point3D(NearHome.X + HuntGround.AreaRadius, NearHome.Y - HuntGround.AreaRadius, 0)));
        Assert.Equal(NearHome.X, area.X + area.Width / 2);
    }

    private static bool IsMonster(string role) => role is Skeleton or Troll or Ettin or Chest or Rat;

    private static DestinationCatalog Catalog(params Destination[] places) => new(places);

    private static Destination Place(string role, Point3D at, int difficulty) =>
        new()
        {
            Name = $"{role} {at.X}-{at.Y}",
            Kind = HuntKind,
            Role = role,
            X = at.X,
            Y = at.Y,
            Z = at.Z,
            Difficulty = difficulty
        };

    [Fact]
    public void FitNote_ShowsThePowerAgainstThePlace_OrNothingForAnUnknownOne()
    {
        Assert.Equal($" (fits power {FighterPower} vs ground {FairDifficulty})", HuntGround.FitNote(FighterPower, FairDifficulty, HuntGround.GroundWord));
        Assert.Equal($" (power {FighterPower} below ground {HardDifficulty})", HuntGround.FitNote(FighterPower, HardDifficulty, HuntGround.GroundWord));
        Assert.Equal(string.Empty, HuntGround.FitNote(FighterPower, 0, HuntGround.GroundWord));
    }
}
