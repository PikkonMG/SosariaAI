using System;
using System.Collections.Generic;
using System.Linq;
using SosariaAI.Combat;
using SosariaAI.Configuration;

using SosariaAI.Spawning;

namespace SosariaAI.Behaviour;

/// <summary>The purpose a person holds for one job phase.</summary>
public enum JobKind
{
    Bank,
    Hunt,
    Dungeon,
    Travel,
    Craft,
    Shop,
    Tavern,
    Idle
}

/// <summary>
/// Layer one of a life: weighted dice over the person's leanings pick one long job for the
/// phase. Drives, role, ambition, the hour of the person's own day, and remembered danger
/// shape the weights; the phase seed rolls the dice. The weights read only slow facts, so
/// the job holds for the phase and changes on a big event: a death, a flee, a PK report,
/// gear bought, the person's night. Pure. No model. No world objects.
/// </summary>
public static class JobRules
{
    public const int RollSalt = 0x10B;

    public const double BankBase = 0.5;
    public const double BankGreed = 0.6;
    public const double BankCaution = 0.4;

    public const double HuntFighterBase = 0.5;
    public const double HuntFighterValor = 1.2;
    public const double HuntWorkerValor = 0.3;

    /// <summary>
    /// In 1999 the dungeon was where a fighter hunted: Despise, Shame and Destard held the
    /// crowds, the woods only the new. A fighter's delve weighs about as much as its hunt.
    /// </summary>
    public const double DungeonFighterBase = 0.5;
    public const double DungeonValor = 1.2;
    public const double DungeonCaution = 0.4;
    public const double DungeonFloor = 0.2;

    public const double TravelBase = 0.3;
    public const double TravelBoldness = 0.5;

    public const double CraftBase = 0.4;
    public const double CraftGreed = 1.0;
    public const double WorkerCraftBoost = 1.6;

    /// <summary>
    /// A crafter lives by its trade: the station is its main work.
    /// A run with no such lean logged 2264 bank picks to 437 craft picks.
    /// At three times the lean, crafters of a two-hour run spent a quarter of their time in the
    /// craft job and as much in the bank job; the craft job now holds about five phases in six,
    /// and the bank, the shops and the inn fill the rest.
    /// </summary>
    public const double CrafterCraftBoost = 8.0;

    public const double ShopBase = 0.25;
    public const double ShopGearBonus = 0.8;

    public const double TavernBase = 0.35;
    public const double TavernEase = 0.4;

    public const double IdleBase = 0.2;

    public const double AmbitionBoost = 1.5;
    public const double AmbitionTravelBoost = 2.5;

    public const double NightActiveCut = 0.3;
    public const double NightTownBoost = 1.5;
    public const double EveningTavernBoost = 1.6;
    public const double EveningCraftCut = 0.6;

    public const double RanRecentlyCut = 0.4;
    public const double PkReportCut = 0.3;
    public const double DangerTravelCut = 0.5;

    /// <summary>Away from home, another trip at once is rare: most people head home first.</summary>
    public const double AwayTravelCut = 0.2;

    /// <summary>
    /// Away from home, hunt grounds, dungeons and work sites belong to the home town and
    /// are out of reach from here. A town job walks the person home first.
    /// </summary>
    public const double AwayOutingCut = 0.1;

    public const double Full = 1.0;

    public const double TendencyFloor = 0.5;
    public const double TendencySpan = 1.5;
    public const double BankCrowdBoost = 1.8;
    public const double SuppliesShopBoost = 4.0;
    public const double SuppliesOutingCut = 0.2;

    public const string BankPlan = "job-bank";
    public const string ShopPlan = "job-shop";
    public const string TavernPlan = "job-tavern";
    public const string IdlePlan = "job-idle";
    public const string TravelPlan = "job-travel";

    private static readonly JobKind[] AllJobs =
    [
        JobKind.Bank, JobKind.Hunt, JobKind.Dungeon, JobKind.Travel,
        JobKind.Craft, JobKind.Shop, JobKind.Tavern, JobKind.Idle
    ];

    /// <summary>The name a job goes by in goal targets, logs and commands.</summary>
    public static string NameOf(JobKind job) => job.ToString().ToLowerInvariant();

    public static bool TryParse(string target, out JobKind job)
    {
        for (var i = 0; i < AllJobs.Length; i++)
        {
            if (string.Equals(target, NameOf(AllJobs[i]), StringComparison.OrdinalIgnoreCase))
            {
                job = AllJobs[i];
                return true;
            }
        }

        job = JobKind.Idle;
        return false;
    }

    public static bool IsJob(string target) => TryParse(target, out _);

    /// <summary>A job done in the home town: out of town, the person walks home first.</summary>
    public static bool IsTownJob(string target) =>
        TryParse(target, out var job) && job is JobKind.Bank or JobKind.Shop or JobKind.Tavern or JobKind.Idle;

    public static bool IsTravel(string target) => TryParse(target, out var job) && job == JobKind.Travel;

    public static GoalKind GoalOf(JobKind job) =>
        job switch
        {
            JobKind.Bank or JobKind.Shop => GoalKind.Trade,
            JobKind.Hunt => GoalKind.Hunt,
            JobKind.Dungeon => GoalKind.Dungeon,
            JobKind.Craft => GoalKind.Work,
            _ => GoalKind.Leisure
        };

    public static Goal GoalFor(JobKind job) => new(GoalOf(job), NameOf(job));

    /// <summary>
    /// A job the person has the skills for. With no skill list every job counts: the goal
    /// picker runs before the catalog, and the loop settles the job once it has the list.
    /// </summary>
    public static bool IsDoable(JobKind job, IReadOnlyCollection<string> skills)
    {
        if (skills == null)
        {
            return true;
        }

        return job switch
        {
            JobKind.Bank => Has(skills, SkillKinds.BankShop) || Has(skills, SkillKinds.BankDeposit),
            JobKind.Hunt => Has(skills, SkillKinds.Hunt),
            JobKind.Dungeon => Has(skills, SkillKinds.Dungeon),
            JobKind.Travel => Has(skills, SkillKinds.Travel),
            JobKind.Craft => HasWork(skills),
            JobKind.Shop => Has(skills, SkillKinds.VendorBuy) || Has(skills, SkillKinds.UpgradeGear) ||
                            Has(skills, SkillKinds.VendorSell) || Has(skills, SkillKinds.Browse),
            JobKind.Tavern => Has(skills, SkillKinds.Tavern) || Has(skills, SkillKinds.Visit),
            _ => true
        };
    }

    public static double Weight(JobKind job, Situation situation)
    {
        var needs = situation?.Needs ?? new NeedsSnapshot();
        var drives = needs.Drives ?? PersonaDrives.Neutral;
        var fighter = situation?.Role is CharacterRole.Fighter;
        var weight = Leaning(job, situation, needs, drives, fighter) * TendencyFactor(job, situation?.Tendencies) *
                     SupplyFactor(job, situation);
        return weight * DayFactor(job, needs.DayPart, IsCrafter(situation)) * DangerFactor(job, situation) *
               HomeFactor(job, situation);
    }

    /// <summary>A person whose class lives by a station trade it runs.</summary>
    public static bool IsCrafter(Situation situation) => !string.IsNullOrWhiteSpace(situation?.CraftTrade);

    /// <summary>
    /// The job for this phase. The same seed and the same slow facts give the same job,
    /// so the job holds until the phase ends or a big event moves the weights. Jobs in
    /// <paramref name="excluded"/> are out of the roll: their plan has nothing to run now.
    /// </summary>
    public static JobKind Roll(
        Situation situation,
        int seed,
        IReadOnlyCollection<string> skills,
        IReadOnlyCollection<JobKind> excluded = null
    )
    {
        var weights = RollWeights(situation, skills, excluded, out var total);

        if (total <= 0)
        {
            return JobKind.Idle;
        }

        var roll = ChoiceSeed.Unit(seed, RollSalt) * total;

        for (var i = 0; i < AllJobs.Length; i++)
        {
            roll -= weights[i];

            if (roll < 0 && weights[i] > 0)
            {
                return AllJobs[i];
            }
        }

        return JobKind.Idle;
    }

    /// <summary>The chance the roll lands on <paramref name="job"/>, from zero to one: its weight over all open jobs.</summary>
    public static double Odds(JobKind job, Situation situation, IReadOnlyCollection<string> skills)
    {
        var weights = RollWeights(situation, skills, excluded: null, out var total);
        return total <= 0 ? 0 : weights[Array.IndexOf(AllJobs, job)] / total;
    }

    /// <summary>The weight of each job in the roll, zero for one the person cannot do or that is excluded.</summary>
    private static double[] RollWeights(
        Situation situation,
        IReadOnlyCollection<string> skills,
        IReadOnlyCollection<JobKind> excluded,
        out double total
    )
    {
        var weights = new double[AllJobs.Length];
        total = 0.0;

        for (var i = 0; i < AllJobs.Length; i++)
        {
            var open = IsDoable(AllJobs[i], skills) && excluded?.Contains(AllJobs[i]) != true;
            weights[i] = open ? Math.Max(0, Weight(AllJobs[i], situation)) : 0;
            total += weights[i];
        }

        return weights;
    }

    /// <summary>Whether a plan belongs to this job. A job plan serves only its own job.</summary>
    public static bool PlanServes(JobKind job, string planId) =>
        job switch
        {
            JobKind.Bank => planId == BankPlan,
            JobKind.Shop => planId == ShopPlan,
            JobKind.Tavern => planId == TavernPlan,
            JobKind.Idle => planId == IdlePlan,
            JobKind.Travel => planId == TravelPlan,
            JobKind.Hunt => planId == GoalPlanRules.HuntTrip,
            JobKind.Dungeon => planId == GoalPlanRules.DungeonTrip,
            JobKind.Craft => GoalPlanRules.IsWorkRecipe(planId),
            _ => false
        };

    private static double Leaning(
        JobKind job,
        Situation situation,
        NeedsSnapshot needs,
        PersonaDrives drives,
        bool fighter
    ) =>
        job switch
        {
            JobKind.Bank => BankBase + drives.Greed * BankGreed + drives.Caution * BankCaution,
            JobKind.Hunt => (fighter ? HuntFighterBase + drives.Valor * HuntFighterValor : drives.Valor * HuntWorkerValor) *
                            (needs.AmbitionWantsHunt ? AmbitionBoost : Full),
            // The shard's pull keeps about a quarter of the fighters on a dungeon run.
            JobKind.Dungeon => fighter
                ? Math.Max(
                      DungeonFloor,
                      DungeonFighterBase + drives.Valor * DungeonValor - drives.Caution * DungeonCaution
                  ) *
                  (needs.AmbitionWantsHunt ? AmbitionBoost : Full) *
                  (situation?.DungeonDemand ?? DungeonShareRules.Neutral)
                : 0,
            JobKind.Travel => (TravelBase + (Full - drives.Caution) * TravelBoldness) *
                              (needs.AmbitionWantsTravel ? AmbitionTravelBoost : Full),
            JobKind.Craft => (CraftBase + drives.Greed * CraftGreed) *
                             (fighter ? Full : WorkerCraftBoost) *
                             (IsCrafter(situation) ? CrafterCraftBoost : Full) *
                             (needs.AmbitionWantsWork ? AmbitionBoost : Full),
            JobKind.Shop => ShopBase + (situation?.CanUpgradeGear == true ? ShopGearBonus : 0),
            JobKind.Tavern => TavernBase + (Full - drives.Greed) * TavernEase,
            _ => IdleBase
        };

    /// <summary>
    /// The person's own leaning toward each kind of activity. Neutral leaves the weight as
    /// it was; a strong leaning up to doubles it; none at all halves it.
    /// </summary>
    public static double TendencyFactor(JobKind job, ActivityTendencies tendencies)
    {
        if (tendencies == null)
        {
            return Full;
        }

        var lean = job switch
        {
            JobKind.Bank or JobKind.Shop => tendencies.Banking,
            JobKind.Hunt or JobKind.Dungeon => tendencies.Adventuring,
            JobKind.Travel => tendencies.Travel,
            JobKind.Craft => tendencies.Crafting,
            _ => tendencies.Idling
        };

        return TendencyFloor + lean * TendencySpan;
    }

    /// <summary>
    /// A free spot in the bank crowd draws people to the bank. Low arrows, reagents or
    /// bandages it can buy, a lost axe, pick or pole, or a pantry short of what its pets eat
    /// send a person shopping; low supplies it can buy keep it out of the dungeon until it has them.
    /// </summary>
    public static double SupplyFactor(JobKind job, Situation situation)
    {
        if (situation == null)
        {
            return Full;
        }

        return job switch
        {
            JobKind.Bank when situation.BankCrowdOpen => BankCrowdBoost,
            JobKind.Shop when situation.SuppliesLow || situation.NeedsTool || situation.NeedsPetFood => SuppliesShopBoost,
            JobKind.Hunt or JobKind.Dungeon when situation.SuppliesLow => SuppliesOutingCut,
            _ => Full
        };
    }

    // A crafter's shop stands in town under the guards, so evening and night do not keep it
    // from the station: the late-night smith at the forge was a fixture of every shard.
    private static double DayFactor(JobKind job, string dayPart, bool crafter)
    {
        if (crafter && job == JobKind.Craft)
        {
            return Full;
        }

        if (string.Equals(dayPart, nameof(DayPart.Night), StringComparison.Ordinal))
        {
            return job switch
            {
                JobKind.Hunt or JobKind.Dungeon or JobKind.Craft or JobKind.Travel => NightActiveCut,
                JobKind.Tavern or JobKind.Idle => NightTownBoost,
                _ => Full
            };
        }

        if (string.Equals(dayPart, nameof(DayPart.Evening), StringComparison.Ordinal))
        {
            return job switch
            {
                JobKind.Tavern => EveningTavernBoost,
                JobKind.Craft => EveningCraftCut,
                _ => Full
            };
        }

        return Full;
    }

    private static double DangerFactor(JobKind job, Situation situation)
    {
        if (situation == null)
        {
            return Full;
        }

        var factor = Full;
        var outing = job is JobKind.Hunt or JobKind.Dungeon;

        if (outing && (situation.DiedRecently || situation.AvoidHuntPlace))
        {
            factor *= MemoryChoiceRules.HuntCut;
        }

        if (situation.RecentRuns > 0 && (outing || job == JobKind.Travel))
        {
            factor *= RanRecentlyCut;
        }

        if (situation.HasPkReport && (outing || job == JobKind.Travel))
        {
            factor *= PkReportCut;
        }

        if (job == JobKind.Travel && situation.DangerSpots is { Count: > 0 })
        {
            factor *= DangerTravelCut;
        }

        if (outing || job == JobKind.Travel)
        {
            factor *= situation.PlaceDanger;
        }

        return factor;
    }

    /// <summary>
    /// Away from home, outings belong to the home town, except the dungeon a fighter stands
    /// in: a restart put it back there, or its run is not over, and it fights on.
    /// </summary>
    private static double HomeFactor(JobKind job, Situation situation)
    {
        if (job == JobKind.Dungeon && situation?.InDungeon == true)
        {
            return DungeonShareRules.InDungeonJobBoost;
        }

        if (situation?.BeyondLeash != true)
        {
            return Full;
        }

        return job switch
        {
            JobKind.Travel => AwayTravelCut,
            JobKind.Hunt or JobKind.Dungeon or JobKind.Craft => AwayOutingCut,
            _ => Full
        };
    }

    private static bool IsWorkSkill(string skillKind) =>
        skillKind is SkillKinds.Mine or SkillKinds.Lumberjack or SkillKinds.Fish or SkillKinds.Boat
            or SkillKinds.Smith or SkillKinds.Alchemy or SkillKinds.Tailor or SkillKinds.Tinker
            or SkillKinds.Carpentry or SkillKinds.Fletch or SkillKinds.Inscription or SkillKinds.Cook
            or SkillKinds.Mage or SkillKinds.Cartography;

    private static bool HasWork(IReadOnlyCollection<string> skills)
    {
        foreach (var skill in skills)
        {
            if (IsWorkSkill(skill))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Has(IReadOnlyCollection<string> skills, string kind)
    {
        foreach (var skill in skills)
        {
            if (string.Equals(skill, kind, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
