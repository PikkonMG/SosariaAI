using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class SkillFactoryTests
{
    private const int ExpectedHuntThreatAverageDamage = 8;
    private const string UnknownSkillName = "NoSuchSkill";

    [Fact]
    public void Create_Arrive_StaysAWhile()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Arrive },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<ArrivalStay>(skill);
        Assert.Equal(SkillKinds.Arrive, skill.Name);
    }

    [Fact]
    public void Create_Travel_IsATownTripNamedTravel()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Travel, Target = new Point3D(2500, 560, 0) },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<TownTrip>(skill);
        Assert.Equal(SkillKinds.Travel, skill.Name);
    }

    [Fact]
    public void Create_TravelWithoutTarget_Throws()
    {
        Assert.Throws<FormatException>(
            () => SkillFactory.Create(
                new SkillStepDefinition { Skill = SkillKinds.Travel },
                null,
                FacetNames.Felucca
            )
        );
    }

    [Fact]
    public void Create_UpgradeGear_ReturnsUpgradeGearSkill()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.UpgradeGear },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<UpgradeGearSkill>(skill);
        Assert.Equal(SkillKinds.UpgradeGear, skill.Name);
    }

    [Fact]
    public void Create_DungeonAtACatalogHall_ReturnsADungeonTrip()
    {
        var hall = new Point3D(5395, 126, 0);

        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Dungeon, Target = hall, Area = HuntGround.AreaAround(hall) },
            null,
            FacetNames.Felucca
        );

        var trip = Assert.IsType<DungeonTripSkill>(skill);
        Assert.Equal(SkillKinds.Dungeon, trip.Name);
        Assert.False(trip.ReachedInside);
    }

    [Fact]
    public void Create_DungeonWithoutATarget_Throws() =>
        Assert.Throws<FormatException>(() => SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Dungeon },
            new FacetContent(),
            FacetNames.Felucca
        ));

    [Fact]
    public void Create_DefaultDespiseRoutine_ReturnsADungeonTrip()
    {
        var felucca = CharactersFile.CreateDefault().Facets[FacetNames.Felucca];
        var step = felucca.Roster[5].Routines["despise"][0];

        Assert.Equal(CharactersFile.DespiseEntryway, step.Target);
        Assert.IsType<DungeonTripSkill>(SkillFactory.Create(step, felucca, FacetNames.Felucca));
    }

    [Fact]
    public void Create_Tavern_DefaultsDestination()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Tavern },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<TavernSkill>(skill);
        Assert.Equal(SkillKinds.Tavern, skill.Name);
    }

    [Fact]
    public void Create_Visit_DefaultsDestination()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Visit },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<VisitSkill>(skill);
        Assert.Equal(SkillKinds.Visit, skill.Name);
    }

    [Fact]
    public void Create_Sightsee_DefaultsDestination()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Sightsee },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<SightseeSkill>(skill);
        Assert.Equal(SkillKinds.Sightsee, skill.Name);
    }

    [Fact]
    public void Create_Loiter_UsesIdleDefaults()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Loiter },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<LoiterSkill>(skill);
        Assert.Equal(SkillKinds.Loiter, skill.Name);
    }

    [Fact]
    public void Create_House_ReturnsHouseSkill()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.House },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<HouseSkill>(skill);
        Assert.Equal(SkillKinds.House, skill.Name);
    }

    [Fact]
    public void Create_VendorSell_ReturnsVendorSellSkill()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.VendorSell },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<VendorSellSkill>(skill);
        Assert.Equal(SkillKinds.VendorSell, skill.Name);
    }

    [Fact]
    public void Create_PartyHunt_WalksToTheGraveyard()
    {
        var file = CharactersFile.CreateDefault();
        var felucca = file.Facets[FacetNames.Felucca];
        var step = felucca.Roster[5].Routines["graveyard"][0];
        var skill = SkillFactory.Create(step, felucca, FacetNames.Felucca);

        Assert.Equal(SkillKinds.Hunt, step.Skill);
        Assert.Equal(CharactersFile.GraveyardGoPoint, step.Target);
        Assert.Equal(CharactersFile.PartyGraveyardCrew, step.Party);
        Assert.IsType<HuntSkill>(skill);
        Assert.Equal(SkillKinds.Hunt, skill.Name);
    }

    [Fact]
    public void Create_TrollHunt_WalksToTheTrollWoods()
    {
        var file = CharactersFile.CreateDefault();
        var felucca = file.Facets[FacetNames.Felucca];
        var step = felucca.Roster[10].Routines["trolls"][10];
        var skill = SkillFactory.Create(step, felucca, FacetNames.Felucca);

        Assert.Equal(SkillKinds.Hunt, step.Skill);
        Assert.Equal(CharactersFile.TrollGoPoint, step.Target);
        Assert.IsType<HuntSkill>(skill);
    }

    [Fact]
    public void Create_GoToWithNoGoal_Throws() =>
        Assert.Throws<FormatException>(() =>
            SkillFactory.Create(new SkillStepDefinition { Skill = SkillKinds.GoTo }, null, FacetNames.Felucca)
        );

    [Fact]
    public void Create_Heal_ReturnsHealSkill()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Heal },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<HealSkill>(skill);
        Assert.Equal(SkillKinds.Heal, skill.Name);
    }

    [Fact]
    public void Create_Alchemy_ReturnsAlchemySkill()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Alchemy },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<AlchemySkill>(skill);
        Assert.Equal(SkillKinds.Alchemy, skill.Name);
    }

    [Fact]
    public void Create_Inscription_ReturnsInscriptionSkill()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Inscription },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<InscriptionSkill>(skill);
        Assert.Equal(SkillKinds.Inscription, skill.Name);
    }

    [Fact]
    public void Create_Peace_ReturnsPracticeSession()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Peace },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<PracticeSession>(skill);
        Assert.Equal(SkillKinds.Peace, skill.Name);
    }

    [Fact]
    public void Create_Track_ReturnsPracticeSession()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Track },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<PracticeSession>(skill);
        Assert.Equal(SkillKinds.Track, skill.Name);
    }

    [Fact]
    public void Create_Gate_ReturnsGateSkill()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Gate },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<GateSkill>(skill);
        Assert.Equal(SkillKinds.Gate, skill.Name);
    }

    [Fact]
    public void Create_Recall_ReturnsRecallSkill()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Recall },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<RecallSkill>(skill);
        Assert.Equal(SkillKinds.Recall, skill.Name);
    }

    [Fact]
    public void Create_Resist_ReturnsPracticeSession()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Resist },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<PracticeSession>(skill);
        Assert.Equal(SkillKinds.Resist, skill.Name);
    }

    [Fact]
    public void Create_Mage_ReturnsPracticeSession()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Mage },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<PracticeSession>(skill);
        Assert.Equal(SkillKinds.Mage, skill.Name);
    }

    [Fact]
    public void Create_Stealth_ReturnsPracticeSession()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Stealth },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<PracticeSession>(skill);
        Assert.Equal(SkillKinds.Stealth, skill.Name);
    }

    [Fact]
    public void Create_RemoveTrap_ReturnsPracticeSession()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.RemoveTrap },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<PracticeSession>(skill);
        Assert.Equal(SkillKinds.RemoveTrap, skill.Name);
    }

    [Fact]
    public void Create_Beg_ReturnsPracticeSession()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Beg },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<PracticeSession>(skill);
        Assert.Equal(SkillKinds.Beg, skill.Name);
    }

    [Fact]
    public void Create_Provoke_ReturnsPracticeSession()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Provoke },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<PracticeSession>(skill);
        Assert.Equal(SkillKinds.Provoke, skill.Name);
    }

    [Fact]
    public void Create_Discord_ReturnsPracticeSession()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Discord },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<PracticeSession>(skill);
        Assert.Equal(SkillKinds.Discord, skill.Name);
    }

    [Fact]
    public void Create_Spirit_ReturnsPracticeSession()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Spirit },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<PracticeSession>(skill);
        Assert.Equal(SkillKinds.Spirit, skill.Name);
    }

    [Fact]
    public void Create_Vet_ReturnsPracticeSession()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Vet },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<PracticeSession>(skill);
        Assert.Equal(SkillKinds.Vet, skill.Name);
    }

    [Fact]
    public void Create_Lore_ReturnsPracticeSession()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Lore },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<PracticeSession>(skill);
        Assert.Equal(SkillKinds.Lore, skill.Name);
    }

    [Fact]
    public void Create_Cartography_ReturnsCartographySkill()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Cartography },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<CartographySkill>(skill);
        Assert.Equal(SkillKinds.Cartography, skill.Name);
    }

    [Fact]
    public void Create_Meditate_ReturnsPracticeSession()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Meditate },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<PracticeSession>(skill);
        Assert.Equal(SkillKinds.Meditate, skill.Name);
    }

    [Fact]
    public void Create_Tinker_ReturnsTinkerSkill()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Tinker },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<TinkerSkill>(skill);
        Assert.Equal(SkillKinds.Tinker, skill.Name);
    }

    [Fact]
    public void Create_Fletch_ReturnsFletchSkill()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Fletch },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<FletchSkill>(skill);
        Assert.Equal(SkillKinds.Fletch, skill.Name);
    }

    [Fact]
    public void Create_Cook_ReturnsCookSkill()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Cook },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<CookSkill>(skill);
        Assert.Equal(SkillKinds.Cook, skill.Name);
    }

    [Fact]
    public void Create_Carpentry_ReturnsCarpentrySkill()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Carpentry },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<CarpentrySkill>(skill);
        Assert.Equal(SkillKinds.Carpentry, skill.Name);
    }

    [Fact]
    public void Create_Poison_ReturnsPracticeSession()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Poison },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<PracticeSession>(skill);
        Assert.Equal(SkillKinds.Poison, skill.Name);
    }

    [Fact]
    public void Create_Tailor_ReturnsTailorSkill()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Tailor },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<TailorSkill>(skill);
        Assert.Equal(SkillKinds.Tailor, skill.Name);
    }

    [Fact]
    public void Create_Snoop_ReturnsSnoopSkill()
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = SkillKinds.Snoop },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<SnoopSkill>(skill);
        Assert.Equal(SkillKinds.Snoop, skill.Name);
    }

    [Theory]
    [MemberData(nameof(PracticeSkillTests.TableKinds), MemberType = typeof(PracticeSkillTests))]
    public void Create_TablePracticeKind_ReturnsAPracticeSessionNamedForIt(string kind)
    {
        var skill = SkillFactory.Create(
            new SkillStepDefinition { Skill = kind },
            null,
            FacetNames.Felucca
        );

        Assert.IsType<PracticeSession>(skill);
        Assert.Equal(kind, skill.Name);
    }

    [Fact]
    public void Create_UnknownSkill_ThrowsFormatException() =>
        Assert.Throws<FormatException>(() =>
            SkillFactory.Create(
                new SkillStepDefinition { Skill = UnknownSkillName },
                null,
                FacetNames.Felucca
            )
        );

    [Fact]
    public void HuntThreatAverageDamage_IsEight() =>
        Assert.Equal(ExpectedHuntThreatAverageDamage, HuntSkill.HuntThreatAverageDamage);

    [Fact]
    public void TryAcquire_NullCharacter_IsFalse() =>
        Assert.False(HuntSkill.TryAcquire(null, () => true));
}
