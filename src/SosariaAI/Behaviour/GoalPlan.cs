using System;
using System.Collections.Generic;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

public readonly record struct PlanStep(string SkillKind, string Why);

/// <summary>
/// One lasting job: ordered steps, why they run, and how far the character has got.
/// Scoring still picks the next skill. This object keeps the chain from being forgotten.
/// The steps are the whole recipe and the index counts in it, so the id and index a save
/// keeps restore the same step.
/// </summary>
public sealed class GoalPlan
{
    public string Id { get; init; }

    public GoalKind Kind { get; init; }

    public string Purpose { get; init; }

    public IReadOnlyList<PlanStep> Steps { get; init; } = [];

    public int Index { get; init; }

    public int StepFailures { get; init; }

    public bool IsComplete => Steps == null || Index >= Steps.Count;

    public string CurrentSkill => IsComplete ? null : Steps[Index].SkillKind;

    public string CurrentWhy => IsComplete ? null : Steps[Index].Why;
}

/// <summary>
/// Builds, keeps, and advances a goal plan. Free. No model.
/// </summary>
public static class GoalPlanRules
{
    public const int MaxStepTries = RepeatFailure.Limit;
    public const double PlanBoost = 8.0;
    public const double OffPlanCut = 0.05;

    public const string MinerWork = "miner-work";
    public const string WoodWork = "wood-work";
    public const string FishWork = "fish-work";
    public const string CraftWork = "craft-work";
    public const string TradeWork = "trade-work";
    public const string HuntTrip = "hunt-trip";
    public const string DungeonTrip = "dungeon-trip";
    public const string HouseBuy = "house-buy";
    public const string BoatOwn = "boat-own";
    public const string TreasureHunt = "treasure-hunt";
    public const string MageCast = "mage-cast";
    public const string SellBank = "sell-bank";
    public const string Recover = "recover";
    public const string Leisure = "leisure";
    public const string PlaceTrip = "place-trip";
    public const string Ghost = "ghost";
    public const string Flee = "flee";
    public const string Conflict = "conflict";
    public const string RedConflictRun = "red-run-conflict";
    public const string RedDungeonRun = "red-run-dungeon";
    public const string RedHuntRun = "red-run-hunt";
    public const string LeaveDen = "leave-den";
    public const string WaitWork = "wait-for-work";

    public const string MinerPurpose = "mine ore, smelt, sell, bank, buy tools, and smith";
    public const string WoodPurpose = "cut wood, sell, and bank";
    public const string FishPurpose = "fish, sell, and bank";
    public const string CraftPurpose = "stand at the shop and make goods";
    public const string TradePurpose = "work the trade at its station, sell, and bank";
    public const string HuntPurpose = "prepare, hunt, heal, and bank the take";
    public const string DungeonPurpose = "enter with allies, fight, and come home";
    public const string HousePurpose = "buy a house and place a vendor";
    public const string BoatPurpose = "own a boat and reuse it";
    public const string TreasurePurpose = "decode a map, walk to the chest, and dig";
    public const string MagePurpose = "cast lawful magery and recover mana";
    public const string SellPurpose = "sell goods and bank gold";
    public const string RecoverPurpose = "heal, rest, or go home";
    public const string LeisurePurpose = "visit the tavern or walk the town";
    public const string PlaceTripPurpose = "travel to the place the ambition names";
    public const string GhostPurpose = "walk as a ghost to a healer";
    public const string FleePurpose = "run from a threat, then resume work";
    public const string ConflictPurpose = "seek player conflict where it happened";
    public const string LeaveDenPurpose = "leave the reds' town for home";
    public const string WaitWorkPurpose = "wander until a job turns up";

    /// <summary>
    /// An outing restocks first when it has a shopping errand: 304 of 407 dungeon stays ended
    /// "its supplies ran low" in the first minute, the crawler back out as soon as it was in.
    /// </summary>
    public const string BuySuppliesWhy = "buy supplies before the outing";
    public const string RedRunPurpose = "ride out on a run, then bank, heal, restock and hang about the Den";
    public const string BankJobPurpose = "spend the time at the bank: bank, trade, and practise";
    public const string ShopJobPurpose = "go shopping: sell, buy gear and supplies";
    public const string TavernJobPurpose = "spend the time at the inn with friends";
    public const string IdleJobPurpose = "take it easy round town";
    public const string TravelJobPurpose = "travel to another town and spend the time there";

    /// <summary>
    /// The craft job: work the trade at its station, then sell and bank. A person runs the
    /// craft steps it has the skill for: all eight trades share one station skill.
    /// </summary>
    private static readonly PlanStep[] CraftSteps =
    [
        Step(SkillKinds.Smith, "smith arms"),
        Step(SkillKinds.Alchemy, "brew a potion"),
        Step(SkillKinds.Tailor, "sew cloth"),
        Step(SkillKinds.Tinker, "make tools"),
        Step(SkillKinds.Carpentry, "work wood"),
        Step(SkillKinds.Fletch, "make arrows"),
        Step(SkillKinds.Inscription, "copy a book"),
        Step(SkillKinds.Cook, "cook food"),
        Step(SkillKinds.VendorSell, "sell goods"),
        Step(SkillKinds.BankDeposit, "bank gold")
    ];

    /// <summary>
    /// A crafter's day: its own trade at the station, then sell and bank. No cooking: a smith
    /// whose craft plan also held the cook step spent its evening at the inn's hearth. A person
    /// runs only the trade steps it has, so every crafter works its own trade. A smith, carpenter
    /// or bowyer first digs or cuts its own stock, a step that can run only when it is low and
    /// cannot buy any (<see cref="SkillReadiness"/>); the sale after the trade smelts the ore.
    /// </summary>
    private static readonly PlanStep[] TradeSteps =
    [
        Step(SkillKinds.Mine, "dig its own ore"),
        Step(SkillKinds.Lumberjack, "cut its own logs"),
        .. Array.FindAll(CraftSteps, step => step.SkillKind != SkillKinds.Cook)
    ];

    /// <summary>
    /// A red's way back from a run (see <see cref="RedGangRunRules"/>): home to the Den, where no
    /// guard comes, the wounds seen to, the loot pawned and banked (a red that lost its kit
    /// takes the spare out of the box there, see <see cref="SpareKitRules"/>), the gear it still
    /// lacks bought at the Den's smith, and the reagents and bandages bought for the next run.
    /// Steps with nothing to do are skipped.
    /// </summary>
    private static readonly PlanStep[] RedReturnSteps =
    [
        Step(SkillKinds.GoHome, "ride back to the Den"),
        Step(SkillKinds.Heal, "bind wounds"),
        Step(SkillKinds.Rest, "rest hits in the Den"),
        Step(SkillKinds.VendorSell, "pawn the loot"),
        Step(SkillKinds.BankDeposit, "bank the loot"),
        Step(SkillKinds.UpgradeGear, "buy the kit it lacks"),
        Step(SkillKinds.VendorBuy, "restock reagents and bandages")
    ];

    /// <summary>A red's time in the Den between runs: its gang mates ride out with it from here.</summary>
    private static readonly PlanStep[] RedHangOutSteps =
    [
        Step(SkillKinds.Loiter, "hang about the Den"),
        Step(SkillKinds.Tavern, "drink with the gang"),
        Step(SkillKinds.Practice, "pass the time before the next run")
    ];

    /// <summary>The step of a red's run that rides back to the Den: the one after the outing.</summary>
    public const int RedRideHomeStep = 1;

    /// <summary>The first step of a red's run spent hanging about the Den.</summary>
    public static readonly int RedHangOutStep = RedRideHomeStep + RedReturnSteps.Length;

    /// <summary>
    /// Every plan's recipe, written once and found by the plan's id. A started plan and a
    /// restored one hold the same steps. A fitted recipe passes over the steps the person has
    /// no skill for; a fixed one keeps them all, and a step that cannot run is skipped when
    /// scored. A town or travel job walks somewhere real, does the thing there, stays a while,
    /// then takes up what the place offers; when its plan ends before the phase does, it
    /// starts again from the top.
    /// </summary>
    private static readonly Dictionary<string, PlanRecipe> Recipes = new(StringComparer.Ordinal)
    {
        [Ghost] = Fixed(GoalKind.Ghost, GhostPurpose, [Step(GhostSkill.SkillName, GhostPurpose)]),
        [Flee] = Fixed(GoalKind.Flee, FleePurpose, [Step(SkillKinds.Flee, FleePurpose)]),
        [LeaveDen] = Fixed(GoalKind.Leisure, LeaveDenPurpose, [Step(SkillKinds.GoHome, LeaveDenPurpose)]),
        [Conflict] = Fitted(GoalKind.Pk, ConflictPurpose, [Step(SkillKinds.Conflict, ConflictPurpose)]),
        [RedConflictRun] = RedRun(Step(SkillKinds.Conflict, "patrol or lie in wait with the gang")),
        [RedDungeonRun] = RedRun(Step(SkillKinds.Dungeon, "delve for loot and prey")),
        [RedHuntRun] = RedRun(Step(SkillKinds.Hunt, "farm monsters in the open")),
        [Recover] = Fitted(
            GoalKind.Recover,
            RecoverPurpose,
            [
                Step(SkillKinds.Heal, "bind wounds"),
                Step(SkillKinds.Meditate, "recover mana"),
                Step(SkillKinds.Rest, "rest hits"),
                Step(SkillKinds.GoHome, "walk home")
            ]
        ),
        [JobRules.BankPlan] = Fixed(
            GoalKind.Trade,
            BankJobPurpose,
            [
                Step(SkillKinds.GoHome, "walk back to town"),
                Step(SkillKinds.BankDeposit, "bank the take"),
                Step(SkillKinds.BankShop, "trade at the bank"),
                Step(SkillKinds.BankCrowd, "take a place in the bank crowd"),
                Step(SkillKinds.Arrive, "stand about at the bank"),
                Step(SkillKinds.Practice, "practise at the bank")
            ]
        ),
        [JobRules.ShopPlan] = Fixed(
            GoalKind.Trade,
            ShopJobPurpose,
            [
                Step(SkillKinds.GoHome, "walk back to town"),
                Step(SkillKinds.VendorSell, "sell goods"),
                Step(SkillKinds.UpgradeGear, "buy better gear"),
                Step(SkillKinds.VendorBuy, "buy supplies"),
                Step(SkillKinds.Browse, "look over the wares"),
                Step(SkillKinds.BankDeposit, "bank the change")
            ]
        ),
        [JobRules.TavernPlan] = Fixed(
            GoalKind.Leisure,
            TavernJobPurpose,
            [
                Step(SkillKinds.GoHome, "walk back to town"),
                Step(SkillKinds.Tavern, "drink at the inn"),
                Step(SkillKinds.Visit, "see a friend"),
                Step(SkillKinds.Practice, "pass the time")
            ]
        ),
        [JobRules.IdlePlan] = Fixed(
            GoalKind.Leisure,
            IdleJobPurpose,
            [
                Step(SkillKinds.GoHome, "walk back to town"),
                Step(SkillKinds.Loiter, "stand about"),
                Step(SkillKinds.Practice, "practise a skill"),
                Step(SkillKinds.Sightsee, "look at a sight"),
                Step(SkillKinds.IdleWander, "wander")
            ]
        ),
        [JobRules.TravelPlan] = Fixed(
            GoalKind.Leisure,
            TravelJobPurpose,
            [
                Step(SkillKinds.Travel, "travel to another town"),
                Step(SkillKinds.Arrive, "look around the town"),
                Step(SkillKinds.BankShop, "trade at the bank"),
                Step(SkillKinds.Tavern, "stop at the inn"),
                Step(SkillKinds.VendorBuy, "look at the shops"),
                Step(SkillKinds.Visit, "meet the locals"),
                Step(SkillKinds.Practice, "pass the time")
            ]
        ),
        [PlaceTrip] = Fixed(GoalKind.Leisure, PlaceTripPurpose, [Step(SkillKinds.Sightsee, PlaceTripPurpose)]),
        [Leisure] = Fitted(
            GoalKind.Leisure,
            LeisurePurpose,
            [
                Step(SkillKinds.Tavern, "drink and talk"),
                Step(SkillKinds.Visit, "see a friend"),
                Step(SkillKinds.Sightsee, "walk a landmark"),
                Step(SkillKinds.Loiter, "stand and watch")
            ]
        ),
        [HuntTrip] = Fitted(
            GoalKind.Hunt,
            HuntPurpose,
            [
                Step(SkillKinds.UpgradeGear, "buy kit"),
                Step(SkillKinds.VendorBuy, BuySuppliesWhy),
                Step(SkillKinds.Hunt, "fight lawful foes"),
                Step(SkillKinds.Heal, "heal after the fight"),
                Step(SkillKinds.VendorSell, "pawn the take"),
                Step(SkillKinds.BankDeposit, "bank the take")
            ]
        ),
        [DungeonTrip] = Fitted(
            GoalKind.Dungeon,
            DungeonPurpose,
            [
                Step(SkillKinds.VendorBuy, BuySuppliesWhy),
                Step(SkillKinds.Follow, "join allies"),
                Step(SkillKinds.Dungeon, "fight in the dungeon"),
                Step(SkillKinds.Heal, "heal after the fight"),
                Step(SkillKinds.VendorSell, "pawn the take"),
                Step(SkillKinds.BankDeposit, "bank the take")
            ]
        ),
        [SellBank] = Fitted(
            GoalKind.Trade,
            SellPurpose,
            [
                Step(SkillKinds.VendorSell, "smelt and sell"),
                Step(SkillKinds.BankDeposit, "bank gold"),
                Step(SkillKinds.VendorBuy, "buy supplies")
            ]
        ),
        [HouseBuy] = Fitted(
            GoalKind.Work,
            HousePurpose,
            [
                Step(SkillKinds.House, "place a cottage"),
                Step(SkillKinds.PlayerVendor, "place and stock a vendor")
            ]
        ),
        [TreasureHunt] = Fitted(
            GoalKind.Work,
            TreasurePurpose,
            [
                Step(SkillKinds.Cartography, TreasurePurpose),
                Step(SkillKinds.BankDeposit, "bank the chest")
            ]
        ),
        [TradeWork] = Fitted(GoalKind.Work, TradePurpose, TradeSteps),
        [CraftWork] = Fitted(GoalKind.Work, CraftPurpose, CraftSteps),
        [MinerWork] = Fitted(
            GoalKind.Work,
            MinerPurpose,
            [
                Step(SkillKinds.Mine, "dig ore"),
                Step(SkillKinds.VendorSell, "smelt and sell"),
                Step(SkillKinds.BankDeposit, "bank gold"),
                Step(SkillKinds.VendorBuy, "buy a pick"),
                Step(SkillKinds.Smith, "smith a dagger")
            ]
        ),
        [WoodWork] = Fitted(
            GoalKind.Work,
            WoodPurpose,
            [
                Step(SkillKinds.Lumberjack, "cut trees"),
                Step(SkillKinds.VendorSell, "sell logs"),
                Step(SkillKinds.BankDeposit, "bank gold"),
                Step(SkillKinds.VendorBuy, "buy a hatchet")
            ]
        ),
        [FishWork] = Fitted(
            GoalKind.Work,
            FishPurpose,
            [
                Step(SkillKinds.Fish, "catch fish"),
                Step(SkillKinds.VendorSell, "sell fish"),
                Step(SkillKinds.BankDeposit, "bank gold"),
                Step(SkillKinds.VendorBuy, "buy a pole")
            ]
        ),
        [BoatOwn] = Fitted(
            GoalKind.Work,
            BoatPurpose,
            [
                Step(SkillKinds.Boat, "own or reuse a boat"),
                Step(SkillKinds.Fish, "fish from the water")
            ]
        ),
        [MageCast] = Fitted(
            GoalKind.Work,
            MagePurpose,
            [
                Step(SkillKinds.Mage, "cast a lawful spell"),
                Step(SkillKinds.Meditate, "recover mana")
            ]
        ),
        [WaitWork] = Fitted(GoalKind.Work, WaitWorkPurpose, [Step(SkillKinds.IdleWander, "wait for a job")])
    };

    public static bool IsInterrupt(Situation situation) =>
        situation is { MustFlee: true } or { IsGhost: true } ||
        (situation?.Needs?.HitsFraction ?? 1) < RecoveryRules.RecoverBelowHitsFraction;

    public static bool IsInterruptSkill(string skillKind) =>
        skillKind is SkillKinds.Flee or SkillKinds.Heal or SkillKinds.GoHome
            or GhostSkill.SkillName or SkillKinds.Rest or SkillKinds.Meditate;

    public static bool MatchesCurrent(GoalPlan plan, string skillKind) =>
        plan is { IsComplete: false } && StepAccepts(plan.CurrentSkill, skillKind);

    /// <summary>
    /// Whether a skill fills a plan step. A <see cref="SkillKinds.Practice"/> step takes any
    /// practice skill, so each person practises what it knows.
    /// </summary>
    public static bool StepAccepts(string stepKind, string skillKind)
    {
        if (string.IsNullOrWhiteSpace(stepKind) || string.IsNullOrWhiteSpace(skillKind))
        {
            return false;
        }

        return skillKind.Equals(stepKind, StringComparison.OrdinalIgnoreCase) ||
               stepKind == SkillKinds.Practice && PracticeRules.IsPractice(skillKind);
    }

    /// <summary>The recipes a work job runs. A work job also sells and banks its own goods.</summary>
    public static bool IsWorkRecipe(string planId) =>
        planId is MinerWork or WoodWork or FishWork or CraftWork or TradeWork or BoatOwn or MageCast or TreasureHunt
            or SellBank;

    /// <summary>
    /// The step an outing exists for. The heal and bank steps around it are filler: with the
    /// fight itself barred, a dungeon job still "ran" its bank step and walked people to the
    /// bank, home and round town all phase long "for Dungeon". Null for a plan with no core.
    /// </summary>
    public static string CoreSkill(string planId) =>
        planId switch
        {
            HuntTrip or RedHuntRun => SkillKinds.Hunt,
            DungeonTrip or RedDungeonRun => SkillKinds.Dungeon,
            RedConflictRun => SkillKinds.Conflict,
            _ => null
        };

    /// <summary>A red's run: one outing, then the Den.</summary>
    public static bool IsRedRun(string planId) => planId is RedConflictRun or RedDungeonRun or RedHuntRun;

    /// <summary>
    /// A red between runs, past its bank and its restock, hanging about the Den: a gang mate
    /// riding out takes it along.
    /// </summary>
    public static bool IsHangingOut(GoalPlan plan) =>
        plan is { IsComplete: false } && IsRedRun(plan.Id) && plan.Index >= RedHangOutStep;

    public static bool ServesPlan(GoalPlan plan, ActionCandidate candidate)
    {
        if (candidate == null || plan == null || plan.IsComplete)
        {
            return true;
        }

        if (MatchesCurrent(plan, candidate.SkillKind) || IsInterruptSkill(candidate.SkillKind))
        {
            return true;
        }

        return IsTownStep(plan.CurrentSkill) && ActionPlaceRules.IsTownWalk(candidate.Step);
    }

    /// <summary>
    /// A step that cannot run never reports an outcome, so the plan would wait on it for
    /// good and cut every other action as off the plan. Move past it. A town step away
    /// from town is not skipped: the town walk serves it. An interrupt blocks every step
    /// for a short time only, so the plan keeps its place.
    /// </summary>
    public static GoalPlan SkipUnavailable(GoalPlan plan, Situation situation, Func<string, bool> canRun)
    {
        if (plan == null || plan.IsComplete || canRun == null || IsInterrupt(situation))
        {
            return plan;
        }

        var index = plan.Index;

        while (index < plan.Steps.Count &&
               !WaitsForTownWalk(plan.Steps[index].SkillKind, situation) &&
               !canRun(plan.Steps[index].SkillKind))
        {
            index++;
        }

        return index == plan.Index ? plan : Copy(plan, index, 0);
    }

    private static bool WaitsForTownWalk(string skillKind, Situation situation) =>
        situation?.InTownRegion != true && IsTownStep(skillKind) && HasTownErrand(skillKind, situation);

    /// <summary>
    /// A town step with nothing to do in town is no reason to walk there: a woodcutter with
    /// an empty pack walked to town for a bank visit that deposited nothing, or for a
    /// shopping trip with nothing to buy.
    /// </summary>
    private static bool HasTownErrand(string skillKind, Situation situation) =>
        skillKind switch
        {
            SkillKinds.VendorSell => situation?.HasSellGoods == true,
            SkillKinds.BankDeposit => situation?.NothingToBank != true,
            SkillKinds.VendorBuy => situation?.HasShoppingErrand == true,
            SkillKinds.BuyMount => situation?.CanBuyMount == true,
            _ => true
        };

    private static bool IsTownStep(string skillKind) =>
        skillKind is SkillKinds.VendorSell or SkillKinds.BankDeposit or SkillKinds.VendorBuy
            or SkillKinds.BuyMount or SkillKinds.Browse
            or SkillKinds.Tavern or SkillKinds.House or SkillKinds.PlayerVendor or SkillKinds.Cartography;

    public static GoalPlan AfterOutcome(GoalPlan plan, string skillKind, bool success)
    {
        if (plan == null || plan.IsComplete)
        {
            return plan;
        }

        // A red its gang took out on their run while it hung about the Den comes home from
        // that run: its own plan picks up at the ride back, not at the next drink.
        if (success && IsRedRun(plan.Id) && plan.Index >= RedRideHomeStep && RedGangRunRules.IsOuting(skillKind))
        {
            return Copy(plan, RedRideHomeStep, 0);
        }

        // A flee or a bandage in the middle of the work is not the work step. A step that
        // is itself a heal or a walk home moves on when it ends, like any other.
        if (!StepAccepts(plan.CurrentSkill, skillKind))
        {
            return plan;
        }

        if (success)
        {
            return Copy(plan, plan.Index + 1, 0);
        }

        var failures = plan.StepFailures + 1;

        if (failures >= MaxStepTries)
        {
            return Copy(plan, plan.Index + 1, 0);
        }

        return Copy(plan, plan.Index, failures);
    }

    /// <param name="canRun">Whether a step can run now; a red's next run picks an outing that can.</param>
    public static GoalPlan ContinueOrStart(
        GoalPlan current,
        Goal goal,
        Situation situation,
        IReadOnlyList<string> availableSkills,
        int seed,
        Func<string, bool> canRun = null
    )
    {
        var set = ToSet(availableSkills);

        if (current is { IsComplete: false } && !ShouldRevise(current, goal, situation))
        {
            return SkipUntilReady(current, situation, set);
        }

        return SkipUntilReady(Begin(goal, situation, set, seed, canRun), situation, set);
    }

    /// <summary>
    /// The plan for this goal, at the first step the person has the skill for. A fitted recipe
    /// passes over the steps it lacks as the plan goes on (<see cref="ContinueOrStart"/>).
    /// </summary>
    /// <param name="canRun">
    /// Whether a step can run now; a red's run picks an outing that can. Without it, any
    /// outing the person has the skill for.
    /// </param>
    public static GoalPlan Start(
        Goal goal,
        Situation situation,
        IReadOnlyList<string> availableSkills,
        int seed,
        Func<string, bool> canRun = null
    ) =>
        Begin(goal, situation, ToSet(availableSkills), seed, canRun);

    private static GoalPlan Begin(
        Goal goal,
        Situation situation,
        HashSet<string> set,
        int seed,
        Func<string, bool> canRun
    ) =>
        SkipUntilReady(RecipeFor(goal, situation, set, seed, canRun), situation: null, set);

    private static GoalPlan RecipeFor(
        Goal goal,
        Situation situation,
        HashSet<string> set,
        int seed,
        Func<string, bool> canRun
    )
    {
        if (goal.Kind == GoalKind.Ghost || situation?.IsGhost == true)
        {
            return FromId(Ghost);
        }

        if (goal.Kind == GoalKind.Flee || situation?.MustFlee == true)
        {
            return FromId(Flee);
        }

        // A blue with no fight in the Den goes home before anything else (Situation.LeavesDen).
        if (goal.Kind == GoalKind.Leisure && goal.Target == SkillKinds.GoHome)
        {
            return FromId(LeaveDen);
        }

        // A red lives by runs out of the Den; a blue answering a PK report walks to it once.
        // A red that lost its kit, or whose run would turn back at once, goes to the Den first:
        // its run starts at the ride back to the Den's bank and shops, not at the outing, so it
        // never rides out naked, short of reagents or with a pack heavy with loot.
        if (goal.Kind == GoalKind.Pk && situation?.Disposition == DispositionKind.Outlaw)
        {
            var run = FromId(RedRunOf(RedGangRunRules.PickOuting(seed, canRun ?? set.Contains)));
            return situation.GoesToDenFirst ? Copy(run, RedRideHomeStep, 0) : run;
        }

        if (goal.Kind == GoalKind.Pk)
        {
            return FromId(Conflict);
        }

        if (goal.Kind == GoalKind.Recover)
        {
            return FromId(Recover);
        }

        if (JobRules.TryParse(goal.Target, out var job) && JobPlanOf(job) is { } jobPlan)
        {
            return FromId(jobPlan);
        }

        if (goal.Kind == GoalKind.Leisure && situation?.Needs?.AmbitionWantsTravel == true &&
            set.Contains(SkillKinds.Sightsee))
        {
            return FromId(PlaceTrip);
        }

        if (goal.Kind == GoalKind.Leisure)
        {
            return FromId(Leisure);
        }

        if (goal.Kind == GoalKind.Hunt)
        {
            return FromId(HuntTrip);
        }

        if (goal.Kind == GoalKind.Dungeon)
        {
            return FromId(DungeonTrip);
        }

        if (goal.Kind == GoalKind.Trade || situation?.HasSellGoods == true)
        {
            return FromId(SellBank);
        }

        if (goal.Kind == GoalKind.Work && goal.Target == SkillKinds.House)
        {
            return FromId(HouseBuy);
        }

        if (situation?.HasTreasureMap == true && set.Contains(SkillKinds.Cartography))
        {
            return FromId(TreasureHunt);
        }

        return FromId(PickWork(set, situation, seed));
    }

    /// <summary>
    /// The saved plan: the same recipe a start builds, at the saved index. The index counts in
    /// the whole recipe, so it names the step the plan stood at when it was saved.
    /// </summary>
    public static GoalPlan Restore(string id, int index, int failures)
    {
        var plan = FromId(id);

        if (plan == null)
        {
            return null;
        }

        var clamped = index < 0 ? 0 : index;
        return Copy(plan, clamped, failures < 0 ? 0 : failures);
    }

    public static IReadOnlyList<string> SkillKindsFrom(IReadOnlyList<ActionCandidate> candidates)
    {
        var kinds = new List<string>();

        if (candidates == null)
        {
            return kinds;
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            var kind = candidates[i].SkillKind;

            if (string.IsNullOrWhiteSpace(kind) || kinds.Contains(kind))
            {
                continue;
            }

            kinds.Add(kind);
        }

        return kinds;
    }

    private static bool HasCraft(HashSet<string> set)
    {
        for (var i = 0; i < CraftSteps.Length; i++)
        {
            if (IsCraftSkill(CraftSteps[i].SkillKind) && set.Contains(CraftSteps[i].SkillKind))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCraftSkill(string skillKind) =>
        skillKind is not (SkillKinds.VendorSell or SkillKinds.BankDeposit);

    /// <summary>The id of the work recipe for the person's skills.</summary>
    private static string PickWork(HashSet<string> set, Situation situation, int seed)
    {
        // A crafter's work is its trade, not the mine or the wood its template also knew.
        if (!string.IsNullOrWhiteSpace(situation?.CraftTrade) && set.Contains(situation.CraftTrade))
        {
            return TradeWork;
        }

        var recipes = new List<string>();
        AddIf(recipes, set.Contains(SkillKinds.Mine), MinerWork);
        AddIf(recipes, set.Contains(SkillKinds.Lumberjack), WoodWork);
        AddIf(recipes, set.Contains(SkillKinds.Fish), FishWork);
        AddIf(recipes, set.Contains(SkillKinds.Boat), BoatOwn);
        AddIf(recipes, HasCraft(set), CraftWork);
        AddIf(recipes, set.Contains(SkillKinds.Mage), MageCast);
        AddIf(recipes, set.Contains(SkillKinds.Cartography), TreasureHunt);

        if (recipes.Count == 0)
        {
            return WaitWork;
        }

        var trains = RecipeThatTrains(situation?.AmbitionSkill);

        if (recipes.Contains(trains))
        {
            return trains;
        }

        return situation?.HasBoat == true && recipes.Contains(BoatOwn)
            ? BoatOwn
            : recipes[Math.Abs(seed) % recipes.Count];
    }

    private static void AddIf(List<string> recipes, bool fits, string id)
    {
        if (fits)
        {
            recipes.Add(id);
        }
    }

    private static string RecipeThatTrains(string ambitionSkill) =>
        ambitionSkill switch
        {
            AmbitionRules.SkillMining => MinerWork,
            AmbitionRules.SkillLumberjacking => WoodWork,
            AmbitionRules.SkillFishing => FishWork,
            AmbitionRules.SkillMagery => MageCast,
            _ => null
        };

    private static bool ShouldRevise(GoalPlan plan, Goal goal, Situation situation)
    {
        if (IsInterrupt(situation) || goal.Kind is GoalKind.Flee or GoalKind.Ghost or GoalKind.Recover)
        {
            return false;
        }

        if (plan.StepFailures >= MaxStepTries)
        {
            return true;
        }

        if (GoalSwitch.ShouldSwitch(situation?.FailedGoalCount ?? 0))
        {
            return true;
        }

        return !Serves(plan, goal);
    }

    private static bool Serves(GoalPlan plan, Goal goal)
    {
        // Leaving the Den is its own plan: a leisure plan at the Den's tavern does not serve it.
        if (plan.Id == LeaveDen || goal.Target == SkillKinds.GoHome)
        {
            return plan.Id == LeaveDen && goal.Target == SkillKinds.GoHome;
        }

        if (JobRules.TryParse(goal.Target, out var job))
        {
            return JobRules.PlanServes(job, plan.Id);
        }

        if (plan.Kind == goal.Kind)
        {
            return true;
        }

        // A gather or craft plan also serves the sale of what it made.
        return plan.Id is MinerWork or WoodWork or FishWork or CraftWork or TradeWork && goal.Kind == GoalKind.Trade;
    }

    /// <summary>
    /// Moves past the steps that have nothing to do: in a fitted recipe the steps the person
    /// has no skill for, and the steps the situation makes pointless. Without a situation only
    /// the missing skills count.
    /// </summary>
    private static GoalPlan SkipUntilReady(GoalPlan plan, Situation situation, HashSet<string> skills)
    {
        if (plan == null || plan.IsComplete)
        {
            return plan;
        }

        var fitted = FitsSkills(plan.Id);
        var index = plan.Index;

        while (index < plan.Steps.Count &&
               (fitted && !skills.Contains(plan.Steps[index].SkillKind) ||
                ShouldSkip(plan.Steps[index].SkillKind, situation)))
        {
            index++;
        }

        return index == plan.Index ? plan : Copy(plan, index, 0);
    }

    private static bool ShouldSkip(string skillKind, Situation situation)
    {
        if (situation == null || string.IsNullOrWhiteSpace(skillKind))
        {
            return false;
        }

        return skillKind switch
        {
            SkillKinds.Mine or SkillKinds.Lumberjack or SkillKinds.Fish when situation.HasHarvestGoods => true,
            SkillKinds.VendorSell when !situation.HasSellGoods => true,
            SkillKinds.House when situation.HasHouse && !situation.HouseDue => true,
            SkillKinds.PlayerVendor when !situation.VendorVisitDue => true,
            SkillKinds.UpgradeGear when !situation.CanUpgradeGear => true,
            SkillKinds.VendorBuy when !situation.HasShoppingErrand => true,
            SkillKinds.Mount when !situation.CanMount => true,
            SkillKinds.BuyMount when !situation.CanBuyMount => true,
            SkillKinds.Follow when situation.KeepsAwayFromParty => true,
            SkillKinds.BankCrowd when !situation.BankCrowdOpen => true,
            _ => false
        };
    }

    /// <summary>The fixed plan of a town or travel job. Hunt, dungeon and work jobs run their own recipes.</summary>
    private static string JobPlanOf(JobKind job) =>
        job switch
        {
            JobKind.Bank => JobRules.BankPlan,
            JobKind.Shop => JobRules.ShopPlan,
            JobKind.Tavern => JobRules.TavernPlan,
            JobKind.Idle => JobRules.IdlePlan,
            JobKind.Travel => JobRules.TravelPlan,
            _ => null
        };

    private static string RedRunOf(string outing) =>
        outing switch
        {
            SkillKinds.Dungeon => RedDungeonRun,
            SkillKinds.Hunt => RedHuntRun,
            _ => RedConflictRun
        };

    private static PlanRecipe RedRun(PlanStep outing) =>
        Fixed(GoalKind.Pk, RedRunPurpose, [outing, .. RedReturnSteps, .. RedHangOutSteps]);

    private static GoalPlan FromId(string id) =>
        id != null && Recipes.TryGetValue(id, out var recipe)
            ? new GoalPlan { Id = id, Kind = recipe.Kind, Purpose = recipe.Purpose, Steps = recipe.Steps }
            : null;

    private static bool FitsSkills(string id) => id != null && Recipes.TryGetValue(id, out var recipe) && recipe.FitsSkills;

    private static GoalPlan Copy(GoalPlan plan, int index, int failures) =>
        new()
        {
            Id = plan.Id,
            Kind = plan.Kind,
            Purpose = plan.Purpose,
            Steps = plan.Steps,
            Index = index,
            StepFailures = failures
        };

    private static PlanStep Step(string skill, string why) => new(skill, why);

    /// <summary>A recipe whose steps the person runs only when it has their skills.</summary>
    private static PlanRecipe Fitted(GoalKind kind, string purpose, PlanStep[] steps) => new(kind, purpose, steps, FitsSkills: true);

    /// <summary>A recipe that keeps every step for every person.</summary>
    private static PlanRecipe Fixed(GoalKind kind, string purpose, PlanStep[] steps) => new(kind, purpose, steps, FitsSkills: false);

    private static HashSet<string> ToSet(IReadOnlyList<string> skills)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (skills == null)
        {
            return set;
        }

        for (var i = 0; i < skills.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(skills[i]))
            {
                set.Add(skills[i]);
            }
        }

        return set;
    }

    /// <summary>The goal a recipe serves, why it runs, its steps, and whether a person runs only the steps it has.</summary>
    private sealed record PlanRecipe(GoalKind Kind, string Purpose, PlanStep[] Steps, bool FitsSkills);
}
