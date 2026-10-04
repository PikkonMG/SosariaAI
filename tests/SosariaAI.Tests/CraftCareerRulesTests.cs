using System.Collections.Generic;
using System.Linq;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class CraftCareerRulesTests
{
    private const int Copies = 2000;
    private const int CopiesPerTemplate = 400;
    private const double GathererShareLow = 0.40;
    private const double GathererShareHigh = 0.60;
    private const double CraftersPerGathererLow = 1.5;
    private const double CraftersPerGathererHigh = 3.0;
    private const double MinerShareOfWorkersLow = 0.05;
    private const string WorkRoutine = "work";
    private const string LoiterRoutine = "loiter";
    private const string TavernRoutine = "tavern";

    private static string CopyId(int i) => $"Felucca:connor#{i}";

    [Fact]
    public void RollCareer_IsStablePerId()
    {
        for (var i = 1; i <= 50; i++)
        {
            Assert.Equal(CraftCareerRules.RollCareer(CopyId(i)), CraftCareerRules.RollCareer(CopyId(i)));
        }
    }

    [Fact]
    public void RollCareer_AboutHalfGather_AndEveryTradeHasPeople()
    {
        var careers = Enumerable.Range(1, Copies).Select(i => CraftCareerRules.RollCareer(CopyId(i))).ToList();
        var gatherers = careers.Count(career => career == null) / (double)Copies;

        Assert.InRange(gatherers, GathererShareLow, GathererShareHigh);

        foreach (var career in CraftCareerRules.Careers)
        {
            Assert.Contains(career.Kind, careers);
        }
    }

    /// <summary>
    /// The live shard spawned no miner in 1098 people: the career roll moved in a straight line
    /// with the job roll, so a worker copy rolled only 10 of the 50 gatherer points and never an
    /// alchemist, a scribe, a bowyer or a tinker.
    /// </summary>
    [Fact]
    public void RollCareer_AmongWorkers_HalfGather_AndEveryTradeHasPeople()
    {
        var careers = Enumerable.Range(1, Copies * 3)
            .Select(CopyId)
            .Where(id => PersonMaker.RollJob(id) == PersonJobs.Worker)
            .Select(CraftCareerRules.RollCareer)
            .ToList();
        var gatherers = careers.Count(career => career == null) / (double)careers.Count;

        Assert.InRange(gatherers, GathererShareLow, GathererShareHigh);

        foreach (var career in CraftCareerRules.Careers)
        {
            Assert.Contains(career.Kind, careers);
        }
    }

    [Fact]
    public void Compose_DefaultRoster_KeepsMinersAndLumberjacksBesideTheCrafters()
    {
        var roster = CharactersFile.CreateDefault().Facets[FacetNames.Felucca].Roster;
        var classes = new List<PersonClass>();

        foreach (var slot in roster)
        {
            for (var i = 1; i <= CopiesPerTemplate; i++)
            {
                var id = $"Felucca:{slot.Id}#{i}";
                var person = PersonMaker.Compose(id, slot, roster, null, EraBand.T2A);

                if (PersonJobs.Of(person.Build) == PersonJobs.Worker)
                {
                    classes.Add(PersonProfileRules.Roll(id, person, Server.Expansion.T2A).Class);
                }
            }
        }

        var miners = classes.Count(personClass => personClass == PersonClass.Miner);
        var gatherers = miners + classes.Count(personClass => personClass == PersonClass.Lumberjack);
        var crafters = classes.Count(personClass => CraftCareerRules.TradeOf(personClass, _ => true) != null);

        Assert.True(miners >= classes.Count * MinerShareOfWorkersLow, $"{miners} miners of {classes.Count} workers");
        Assert.InRange(crafters / (double)gatherers, CraftersPerGathererLow, CraftersPerGathererHigh);

        foreach (var career in CraftCareerRules.Careers)
        {
            Assert.Contains(CraftCareerRules.ClassOf(career.Kind).Value, classes);
        }
    }

    [Fact]
    public void Apply_TurnsAWoodcutterIntoATailor_WithoutTouchingTheTemplate()
    {
        var template = Woodcutter();
        var composed = new CharacterDefinition
        {
            Id = template.Id,
            Routines = template.Routines,
            Choices = template.Choices
        };

        CraftCareerRules.Apply(composed, SkillKinds.Tailor, CopyId(1));

        Assert.False(composed.Routines.ContainsKey(WorkRoutine));
        Assert.True(composed.Routines.ContainsKey(LoiterRoutine));
        Assert.True(composed.Routines.ContainsKey(TavernRoutine));
        Assert.Equal(SkillKinds.Tailor, composed.Routines[CraftCareerRules.CraftRoutineId][0].Skill);
        Assert.True(composed.UsesSkill(SkillKinds.Tailor));
        Assert.False(composed.UsesSkill(SkillKinds.Lumberjack));
        Assert.DoesNotContain(composed.Choices, choice => choice.Routine == WorkRoutine);
        Assert.Contains(composed.Choices, choice => choice.Routine == CraftCareerRules.CraftRoutineId);

        Assert.True(template.Routines.ContainsKey(WorkRoutine));
        Assert.False(template.Routines.ContainsKey(CraftCareerRules.CraftRoutineId));
        Assert.Contains(template.Choices, choice => choice.Routine == WorkRoutine);
    }

    [Fact]
    public void Apply_NoCareer_LeavesTheGathererAlone()
    {
        var composed = Woodcutter();
        var routines = composed.Routines;

        CraftCareerRules.Apply(composed, null, CopyId(1));
        CraftCareerRules.Apply(composed, SkillKinds.Mine, CopyId(1));

        Assert.Same(routines, composed.Routines);
    }

    [Fact]
    public void CraftRoutine_WorksSellsRestocksAndBanks()
    {
        var steps = CraftCareerRules.CraftRoutine(SkillKinds.Tailor, WorkSites.BritainTown).Select(step => step.Skill).ToList();

        Assert.Equal(
            [SkillKinds.Tailor, SkillKinds.VendorSell, SkillKinds.VendorBuy, SkillKinds.BankDeposit, SkillKinds.BankShop, SkillKinds.Decide],
            steps
        );
    }

    [Fact]
    public void CraftRoutine_ASmithMinesItsOwnOre_AndWalksItToTown()
    {
        var routine = CraftCareerRules.CraftRoutine(SkillKinds.Smith, WorkSites.MinocTown);
        var mine = routine.Single(step => step.Skill == SkillKinds.Mine);

        Assert.Equal(
            [
                SkillKinds.Smith, SkillKinds.Mine, SkillKinds.GoTo, SkillKinds.VendorSell, SkillKinds.VendorBuy,
                SkillKinds.BankDeposit, SkillKinds.BankShop, SkillKinds.Decide
            ],
            routine.Select(step => step.Skill)
        );
        Assert.Equal(WorkSites.MinocHills.Harvest, mine.Area.ToRectangle());
        Assert.Equal(CharactersFile.DefaultBankSpot, routine.Single(step => step.Skill == SkillKinds.GoTo).Target);
    }

    [Theory]
    [InlineData(SkillKinds.Carpentry)]
    [InlineData(SkillKinds.Fletch)]
    public void CraftRoutine_AWoodworkerCutsItsOwnLogs(string trade)
    {
        var routine = CraftCareerRules.CraftRoutine(trade, WorkSites.YewTown);

        Assert.Equal(WorkSites.YewWood.Harvest, routine.Single(step => step.Skill == SkillKinds.Lumberjack).Area.ToRectangle());
        Assert.DoesNotContain(routine, step => step.Skill == SkillKinds.Mine);
    }

    [Fact]
    public void Apply_ASmithCopyHomesAtAForge_AndMinesTheSiteNearestIt()
    {
        for (var i = 1; i <= 40; i++)
        {
            var id = CopyId(i);
            var smith = Woodcutter();
            CraftCareerRules.Apply(smith, SkillKinds.Smith, id);

            var home = WorkSites.HomeFor(smith.Spawn, id, WorkSites.PrimaryWork(smith));
            var mine = smith.Routines[CraftCareerRules.CraftRoutineId].Single(step => step.Skill == SkillKinds.Mine);

            Assert.Contains(CraftCareerRules.HomesFor(SkillKinds.Smith), site => site.Home == home);
            Assert.Equal(WorkSites.NearestSite(SkillKinds.Mine, home).Harvest, mine.Area.ToRectangle());
            Assert.Equal(SkillKinds.Smith, CraftCareerRules.CareerOf(smith));
            Assert.Equal(PersonClass.Smith, PersonProfileRules.Roll(id, smith, Server.Expansion.T2A).Class);
        }
    }

    [Fact]
    public void TradeOf_MapsCrafterClassesThatRunTheirTrade()
    {
        Assert.Equal(SkillKinds.Tailor, CraftCareerRules.TradeOf(PersonClass.Tailor, _ => true));
        Assert.Equal(SkillKinds.Fletch, CraftCareerRules.TradeOf(PersonClass.Bowyer, _ => true));
        Assert.Equal(SkillKinds.Inscription, CraftCareerRules.TradeOf(PersonClass.Scribe, _ => true));
        Assert.Null(CraftCareerRules.TradeOf(PersonClass.Tailor, _ => false));
        Assert.Null(CraftCareerRules.TradeOf(PersonClass.Lumberjack, _ => true));
        Assert.Null(CraftCareerRules.TradeOf(PersonClass.Warrior, _ => true));
    }

    [Fact]
    public void TradeByKind_IsTheTradesOwnRules()
    {
        Assert.Same(SmithRules.Trade, CraftCareerRules.TradeByKind(SkillKinds.Smith));
        Assert.Same(TailorRules.Trade, CraftCareerRules.TradeByKind(SkillKinds.Tailor));
        Assert.Null(CraftCareerRules.TradeByKind(SkillKinds.Cook));
        Assert.Null(CraftCareerRules.TradeByKind(null));
    }

    [Fact]
    public void HomesFor_SmithsNeverHomeInYew_WhichHasNoForge()
    {
        var smithHomes = CraftCareerRules.HomesFor(SkillKinds.Smith);

        Assert.NotEmpty(smithHomes);
        Assert.DoesNotContain(smithHomes, site => site.Home == WorkSites.YewTown);
        Assert.Empty(CraftCareerRules.HomesFor(SkillKinds.Mine));

        foreach (var career in CraftCareerRules.Careers)
        {
            Assert.NotEmpty(CraftCareerRules.HomesFor(career.Kind));
            Assert.False(string.IsNullOrWhiteSpace(CraftCareerRules.DescriptionOf(career.Kind)));
        }
    }

    [Fact]
    public void Crafter_HomesInATownWithItsStation()
    {
        var tailor = Woodcutter();
        CraftCareerRules.Apply(tailor, SkillKinds.Tailor, CopyId(1));

        Assert.Equal(SkillKinds.Tailor, WorkSites.PrimaryWork(tailor));

        for (var i = 1; i <= 40; i++)
        {
            var home = WorkSites.HomeFor(tailor.Spawn, CopyId(i), WorkSites.PrimaryWork(tailor));
            Assert.Contains(CraftCareerRules.HomesFor(SkillKinds.Tailor), site => site.Home == home);
        }
    }

    [Fact]
    public void PickWorker_PureCraftersGetTheirOwnClass()
    {
        Assert.Equal(PersonClass.Smith, ClassOf(SkillKinds.Smith));
        Assert.Equal(PersonClass.Tailor, ClassOf(SkillKinds.Tailor));
        Assert.Equal(PersonClass.Carpenter, ClassOf(SkillKinds.Carpentry));
        Assert.Equal(PersonClass.Bowyer, ClassOf(SkillKinds.Fletch));
        Assert.Equal(PersonClass.Alchemist, ClassOf(SkillKinds.Alchemy));
        Assert.Equal(PersonClass.Scribe, ClassOf(SkillKinds.Inscription));
        Assert.Equal(PersonClass.Tinker, ClassOf(SkillKinds.Tinker));

        foreach (var career in CraftCareerRules.Careers)
        {
            Assert.True(PersonClassRules.IsCrafter(ClassOf(career.Kind)));
            Assert.Equal(ClassOf(career.Kind), CraftCareerRules.ClassOf(career.Kind));
        }
    }

    [Fact]
    public void PickWorker_AGathererWhoMinesIsAMiner_NotASmith()
    {
        var miner = new CharacterDefinition
        {
            Id = "mira",
            Build = new BuildDefinition { Role = PersonJobs.Worker },
            Routines = new Dictionary<string, List<SkillStepDefinition>>
            {
                [WorkRoutine] = [new() { Skill = SkillKinds.Mine }, new() { Skill = SkillKinds.Smith }]
            }
        };

        for (var i = 1; i <= 40; i++)
        {
            Assert.Null(CraftCareerRules.CareerOf(miner));
            Assert.Equal(PersonClass.Miner, PersonProfileRules.Roll(CopyId(i), miner, Server.Expansion.T2A).Class);
        }
    }

    [Fact]
    public void Compose_WorkerCopies_TakeUpCraftsAndSomeStayGatherers()
    {
        var fighter = new CharacterDefinition
        {
            Id = "bran",
            Build = new BuildDefinition { Preset = "swordsman" },
            Routines = new Dictionary<string, List<SkillStepDefinition>> { ["hunt"] = [new() { Skill = SkillKinds.Hunt }] }
        };
        List<CharacterDefinition> roster = [Woodcutter(), fighter];
        var crafters = 0;
        var gatherers = 0;

        for (var i = 1; i <= 300; i++)
        {
            var id = CopyId(i);
            var person = PersonMaker.Compose(id, roster[0], roster, null, EraBand.T2A);

            if (PersonJobs.Of(person.Build) != PersonJobs.Worker)
            {
                Assert.False(person.Routines.ContainsKey(CraftCareerRules.CraftRoutineId));
                continue;
            }

            var career = CraftCareerRules.RollCareer(id);

            if (career == null)
            {
                gatherers++;
                Assert.True(person.UsesSkill(SkillKinds.Lumberjack));
            }
            else
            {
                crafters++;
                Assert.True(person.UsesSkill(career));
                Assert.Equal(
                    CraftCareerRules.TradeByKind(career).GatherKind == SkillKinds.Lumberjack,
                    person.UsesSkill(SkillKinds.Lumberjack)
                );
            }
        }

        Assert.True(crafters > 0 && gatherers > 0, $"{crafters} crafters, {gatherers} gatherers");
    }

    private static PersonClass ClassOf(string trade)
    {
        var person = Woodcutter();
        CraftCareerRules.Apply(person, trade, CopyId(7));
        return PersonClassRules.Pick(
            PersonJobs.Worker,
            null,
            person.UsesSkill,
            CraftCareerRules.CareerOf(person),
            CopyId(7),
            0,
            Server.Expansion.T2A
        );
    }

    private static CharacterDefinition Woodcutter() =>
        new()
        {
            Id = "connor",
            Build = new BuildDefinition { Role = PersonJobs.Worker },
            Routines = new Dictionary<string, List<SkillStepDefinition>>
            {
                [WorkRoutine] =
                [
                    new() { Skill = SkillKinds.Lumberjack },
                    new() { Skill = SkillKinds.Fletch },
                    new() { Skill = SkillKinds.VendorSell },
                    new() { Skill = SkillKinds.Decide }
                ],
                [LoiterRoutine] = [new() { Skill = SkillKinds.Loiter }, new() { Skill = SkillKinds.Decide }],
                [TavernRoutine] = [new() { Skill = SkillKinds.Tavern }, new() { Skill = SkillKinds.Cook }]
            },
            Choices =
            [
                new ChoiceDefinition { Routine = WorkRoutine, Weight = CraftCareerRules.CraftChoiceWeight },
                new ChoiceDefinition { Routine = LoiterRoutine, Weight = 1 }
            ]
        };
}
