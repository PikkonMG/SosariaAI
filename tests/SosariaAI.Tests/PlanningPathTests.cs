using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class PlanningPathTests
{
    private static readonly DateTime Noon = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);
    private const string WorkerId = "felucca:mira";
    private const string FighterId = "felucca:bran";
    private const string SocialId = "felucca:sela";
    private const string PartnerId = "player:aria";

    [Fact]
    public void CallKind_IsDecisionNotChat()
    {
        Assert.Equal(BrainProviders.DecisionKind, PlanningPath.CallKind);
        Assert.False(DecisionEvents.IsChat(BrainEventKind.Plan));
        Assert.True(DecisionEvents.Qualifies(BrainEventKind.Plan, speakerIsPlayer: false));
        Assert.True(DecisionEvents.IsChat(BrainEventKind.Spoken));
    }

    [Fact]
    public void OrdinaryEvent_ReachesDecisionProvider_AndValidReplyBecomesPlan()
    {
        var path = new PlanningPath();
        var provider = new FakeDecisionProvider();
        provider.Reply = WorkerPlanJson();

        Assert.True(Ask(path, PlanTrigger.PlayerRequest, Noon));
        var raw = provider.Send(path, WorkerFacts());
        var accepted = path.Deliver(raw, 1, WorkerId, AcceptContext(WorkerId), timedOut: false);

        Assert.Equal(BrainProviders.DecisionKind, provider.LastCallKind);
        Assert.True(accepted.Accepted);
        Assert.Equal(PlanControl.ControllerModel, path.Diag.Controller);
        Assert.Equal("better tools", path.Plan.Goal);
        Assert.Equal(SkillKinds.Mine, path.Plan.CurrentSkill);
        Assert.Equal(1, path.Diag.PlanningCalls);
        Assert.Equal(1, path.Diag.AcceptedPlans);
        Assert.Equal(0, path.Diag.ChatCalls);
    }

    [Fact]
    public void Plan_DrivesMultipleSteps_AndScorerCannotReplace()
    {
        var path = ReadyWorker();
        var catalog = Catalog(SkillKinds.Mine, SkillKinds.VendorSell, SkillKinds.VendorBuy, SkillKinds.IdleWander);
        var situation = QuietWork();

        var first = path.Next(catalog, situation, Noon);
        Assert.True(first.FromModel);
        Assert.Equal(SkillKinds.Mine, first.SkillKind);
        Assert.Equal(SkillKinds.Mine, first.Candidate.SkillKind);

        var stillMine = path.Next(catalog, situation, Noon.AddMinutes(1));
        Assert.Equal(SkillKinds.Mine, stillMine.SkillKind);
        Assert.False(path.ScorerMayReplace(situation, Noon.AddMinutes(1)));

        var afterMine = path.FinishStep(Harvested(SkillKinds.Mine), Noon.AddMinutes(5));
        Assert.Equal(StepResultKind.StepCompleted, afterMine.Kind);
        Assert.Equal(SkillKinds.VendorSell, path.Plan.CurrentSkill);

        var second = path.Next(catalog, situation, Noon.AddMinutes(6));
        Assert.Equal(SkillKinds.VendorSell, second.SkillKind);
        Assert.NotEqual(SkillKinds.IdleWander, second.SkillKind);
    }

    [Fact]
    public void FailedStep_RecordsCause_AndRevisesOnlyAfterLimit()
    {
        var path = ReadyWorker();
        var fail = NoProgress(SkillKinds.Mine);

        var first = path.FinishStep(fail, Noon);
        Assert.False(first.RequestRevision);
        Assert.Equal(1, path.Plan.StepFailures);
        Assert.Equal(PlanState.Waiting, path.Plan.State);

        path.FinishStep(fail, Noon.AddMinutes(1));
        var last = path.FinishStep(fail, Noon.AddMinutes(2));
        Assert.True(last.RequestRevision);
        Assert.Equal(PlanState.Failed, path.Plan.State);
        Assert.Equal(1, path.Diag.FailedGoals);
    }

    [Fact]
    public void LateReply_CannotReplaceNewerPlan()
    {
        var path = ReadyWorker();
        var firstId = path.Plan.Id;
        path.MarkEnqueued(2, Noon.AddMinutes(3));
        path.NoteAgreement();
        var stale = path.Deliver(
            WorkerPlanJson("other goal"),
            2,
            WorkerId,
            AcceptContext(WorkerId, path.Plan.Revision),
            timedOut: false
        );

        Assert.False(stale.Accepted);
        Assert.True(stale.Stale);
        Assert.Equal(firstId, path.Plan.Id);
        Assert.Equal(1, path.Diag.StaleReplies);
        Assert.Equal("better tools", path.Plan.Goal);
    }

    [Fact]
    public void InvalidTargetsAndForbiddenActions_CannotRun()
    {
        var skills = new[] { SkillKinds.Mine, SkillKinds.VendorSell };
        Assert.Equal(
            PlanValidator.RejectDecide,
            PlanValidator.Reject(Proposal(WorkerId, [new ModelPlanStep(SkillKinds.Decide, "think", "")]), skills, ["bank"], [], [], Expansion.None)
        );
        Assert.Equal(
            PlanValidator.RejectCoordinates,
            PlanValidator.Reject(Proposal(WorkerId, [new ModelPlanStep(SkillKinds.Mine, "dig", "1420,1695,0")]), skills, ["bank"], [], [], Expansion.None)
        );
        Assert.Equal(
            PlanValidator.RejectTarget,
            PlanValidator.Reject(Proposal(WorkerId, [new ModelPlanStep(SkillKinds.Mine, "dig", "dest:moon")]), skills, ["bank"], [], [], Expansion.None)
        );
        Assert.Equal(
            PlanValidator.RejectBadSkill,
            PlanValidator.Reject(Proposal(WorkerId, [new ModelPlanStep("Fly", "leave", "")]), skills, ["bank"], [], [], Expansion.None)
        );

        var catalog = Catalog(SkillKinds.Mine);
        var path = ReadyWorker();
        var bound = PlanControl.Bind(catalog, new ModelPlanStep("Fly", "leave", ""));
        Assert.Null(bound);
    }

    [Fact]
    public void TimeoutAndSpentBudget_UseFallback()
    {
        var path = new PlanningPath();
        Assert.True(Ask(path, PlanTrigger.PlayerRequest, Noon, budgetOk: true));
        path.MarkEnqueued(1, Noon);
        var timedOut = path.Deliver(null, 1, WorkerId, AcceptContext(WorkerId), timedOut: true);
        Assert.False(timedOut.Accepted);
        Assert.Equal("timeout", timedOut.Rejection);
        Assert.Equal(PlanControl.ControllerFallback, path.Diag.Controller);
        Assert.True(path.Diag.FallbackUse > 0);

        var blocked = new PlanningPath();
        Assert.False(Ask(blocked, PlanTrigger.PlayerRequest, Noon, budgetOk: false));
        Assert.Equal("budget", blocked.Diag.NextCall);
        var work = blocked.Next(Catalog(SkillKinds.IdleWander), QuietWork(), Noon);
        Assert.False(work.FromModel);
    }

    [Fact]
    public void Crowd_CannotFloodProvider()
    {
        var last = Noon;
        Assert.True(PlanRequestRules.CrowdAllows(default, Noon));
        Assert.False(PlanRequestRules.CrowdAllows(last, last.AddMilliseconds(PlanRequestRules.MinGapMilliseconds - 1)));
        Assert.True(PlanRequestRules.CrowdAllows(last, last.AddMilliseconds(PlanRequestRules.MinGapMilliseconds)));

        var path = new PlanningPath();
        path.MarkEnqueued(1, Noon);
        Assert.False(Ask(path, PlanTrigger.PlayerRequest, Noon.AddSeconds(1)));
        Assert.Equal("in-flight", path.Diag.NextCall);
    }

    [Fact]
    public void SaveAndLoad_PreservesIntent_WithoutRepeatingReward()
    {
        var path = ReadyWorker();
        path.FinishStep(Harvested(SkillKinds.Mine), Noon.AddMinutes(1));
        var gold = 40;
        var lines = path.Plan.ToLines();
        var restored = ModelPlan.FromLines(lines);
        var loaded = new PlanningPath();
        loaded.Restore(restored, Noon.AddMinutes(2), WorkerId);

        Assert.Equal(path.Plan.Id, loaded.Plan.Id);
        Assert.Equal(SkillKinds.VendorSell, loaded.Plan.CurrentSkill);
        Assert.Equal(1, loaded.Plan.Index);
        Assert.False(loaded.InFlight);
        Assert.Equal(40, gold);
        Assert.Equal(PlanState.Waiting, loaded.Plan.State);
    }

    [Fact]
    public void ChatPath_StaysDistinctFromPlanning()
    {
        Assert.True(DecisionEvents.IsChat(BrainEventKind.Spoken));
        Assert.True(DecisionEvents.IsChat(BrainEventKind.Musing));
        Assert.False(DecisionEvents.IsChat(BrainEventKind.Plan));
        Assert.False(DecisionEvents.IsChat(BrainEventKind.DungeonEnded));
        Assert.Equal(PromptBuilder.JsonShape, PromptBuilder.ShapeFor(BrainEventKind.Spoken));
        Assert.Equal(PromptBuilder.PlanJsonShape, PromptBuilder.ShapeFor(BrainEventKind.Plan));
        Assert.Equal(PromptBuilder.DecideJsonShape, PromptBuilder.ShapeFor(BrainEventKind.DungeonEnded));
    }

    [Fact]
    public void Worker_EarnsBuysAndUsesItem()
    {
        var path = ReadyWorker();
        var catalog = Catalog(SkillKinds.Mine, SkillKinds.VendorSell, SkillKinds.VendorBuy);
        var world = new WorldFacts(Gold: 0, BankGold: 0, Goods: 0, Tools: 0, ArmorEquipped: false,
            ItemObtained: false, ObtainedItem: "", Place: "mine", AtHome: false, InParty: false,
            PartyPartner: "", Alive: true, Fought: false, HuntKills: 0, Hits: 80, CorpseRecovered: false);

        Assert.Equal(SkillKinds.Mine, path.Next(catalog, QuietWork(), Noon).SkillKind);
        world = world with { Goods = 12 };
        var mined = path.FinishStep(StepProof.Observe(SkillKinds.Mine, world with { Goods = 0 }, world, true, false, false), Noon);
        Assert.Equal(StepResultKind.StepCompleted, mined.Kind);
        Assert.Equal(SkillKinds.VendorSell, path.Plan.CurrentSkill);

        var beforeSell = world;
        world = world with { Gold = 60, Goods = 0 };
        var sold = path.FinishStep(StepProof.Observe(SkillKinds.VendorSell, beforeSell, world, true, false, false), Noon);
        Assert.True(DetailSold(sold));
        Assert.Equal(SkillKinds.VendorBuy, path.Plan.CurrentSkill);

        var beforeBuy = world;
        world = world with { Gold = 20, Tools = 1, ItemObtained = true, ObtainedItem = "hatchet" };
        var bought = path.FinishStep(StepProof.Observe(SkillKinds.VendorBuy, beforeBuy, world, true, false, false), Noon);
        Assert.Equal(StepResultKind.GoalCompleted, bought.Kind);
        Assert.Equal(PlanState.Complete, path.Plan.State);
        Assert.True(StepProof.MeetsSuccess(StepProof.SuccessItemObtained, world));
        Assert.False(StepProof.Observe(SkillKinds.VendorBuy, beforeBuy, beforeBuy, true, false, false).Kind == StepResultKind.StepCompleted);
    }

    [Fact]
    public void Fighter_PreparesHuntsReturns_AndRevisesAfterDeath()
    {
        var path = new PlanningPath();
        path.MarkEnqueued(1, Noon);
        var accepted = path.Deliver(FighterPlanJson(), 1, FighterId, AcceptContext(FighterId, skills: FighterSkills()), false);
        Assert.True(accepted.Accepted);
        var catalog = Catalog(SkillKinds.UpgradeGear, SkillKinds.Hunt, SkillKinds.BankDeposit, SkillKinds.Heal);
        var world = new WorldFacts(80, 0, 0, 0, false, false, "", "britain", true, false, "", true, false, 0, 80, false);

        Assert.Equal(SkillKinds.UpgradeGear, path.Next(catalog, QuietWork(), Noon).SkillKind);
        var armed = world with { Gold = 20, ArmorEquipped = true, ItemObtained = true, ObtainedItem = "chainmail" };
        path.FinishStep(StepProof.Observe(SkillKinds.UpgradeGear, world, armed, true, false, false), Noon);
        Assert.Equal(SkillKinds.Hunt, path.Plan.CurrentSkill);

        var fought = armed with { Place = "graveyard", AtHome = false, Fought = true, HuntKills = 3, Hits = 50 };
        var hunted = path.FinishStep(StepProof.Observe(SkillKinds.Hunt, armed, fought, true, false, false), Noon);
        Assert.Equal(StepResultKind.StepCompleted, hunted.Kind);
        Assert.Equal(SkillKinds.BankDeposit, path.Plan.CurrentSkill);

        var dead = fought with { Alive = false, Hits = 0 };
        var death = path.FinishStep(StepProof.Observe(SkillKinds.BankDeposit, fought, dead, false, true, false), Noon.AddMinutes(20));
        Assert.Equal(StepResultKind.Interrupted, death.Kind);
        Assert.True(death.RequestRevision);
        path.MarkEnqueued(2, Noon.AddMinutes(21));

        var revised = path.Deliver(FighterReviseJson(), 2, FighterId, AcceptContext(FighterId, path.PendingRevision, FighterSkills()), false);
        Assert.True(revised.Accepted);
        Assert.Equal(SkillKinds.Heal, path.Plan.CurrentSkill);
        Assert.Contains("death", path.Plan.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Social_AcceptedInvitation_CarriesThroughWithPartner()
    {
        var path = new PlanningPath();
        path.NoteAgreement();
        Assert.True(Ask(path, PlanTrigger.Invitation, Noon));
        path.MarkEnqueued(1, Noon);
        var accepted = path.Deliver(SocialPlanJson(), 1, SocialId, AcceptContext(SocialId, skills: SocialSkills(), people: [PartnerId]), false);
        Assert.True(accepted.Accepted);
        Assert.Equal(PlanTrigger.Invitation, path.Plan.Trigger);

        var catalog = Catalog(SkillKinds.Follow, SkillKinds.Hunt);
        Assert.Equal(SkillKinds.Follow, path.Next(catalog, QuietWork(), Noon).SkillKind);
        var before = new WorldFacts(20, 0, 0, 0, false, false, "", "britain", true, false, "", true, false, 0, 80, false);
        var joined = before with { InParty = true, PartyPartner = PartnerId };
        var follow = path.FinishStep(StepProof.Observe(SkillKinds.Follow, before, joined, true, false, false), Noon);
        Assert.Equal(StepResultKind.StepCompleted, follow.Kind);
        Assert.Equal(SkillKinds.Hunt, path.Plan.CurrentSkill);

        var hunted = joined with { Fought = true, HuntKills = 1, Place = "graveyard", AtHome = false };
        var done = path.FinishStep(StepProof.Observe(SkillKinds.Hunt, joined, hunted, true, false, false), Noon);
        Assert.Equal(StepResultKind.GoalCompleted, done.Kind);
        Assert.True(StepProof.MeetsSuccess(StepProof.SuccessHuntedTogether, hunted));
        Assert.False(StepProof.Observe(SkillKinds.Follow, before, before, true, false, false).Kind == StepResultKind.StepCompleted);
    }

    [Fact]
    public void ParsePlan_ReadsStructuredReply_AndOldChoose()
    {
        var plan = ReplyParser.ParsePlan(WorkerPlanJson(), WorkerId, 0, 1);
        Assert.Equal("better tools", plan.Goal);
        Assert.Equal(3, plan.Steps.Count);
        Assert.Equal(SkillKinds.Mine, plan.Steps[0].SkillKind);

        var legacy = ReplyParser.ParsePlan("""{"choose":"Mine","say":"To the hills.","mood":"grim"}""", WorkerId, 0, 2);
        Assert.Equal(SkillKinds.Mine, legacy.Steps[0].SkillKind);
    }

    [Fact]
    public void UrgentFlee_InterruptsPlan_ThenFallbackOwns()
    {
        var path = ReadyWorker();
        var flee = QuietWork() with { MustFlee = true };
        Assert.True(path.ScorerMayReplace(flee, Noon));
        var work = path.Next(Catalog(SkillKinds.Mine, SkillKinds.Flee), flee, Noon);
        Assert.False(work.FromModel);
        Assert.Equal(PlanControl.FallbackUrgent, work.FallbackReason);
    }

    private static bool DetailSold(ObserveResult result) =>
        result.Kind == StepResultKind.StepCompleted && result.Detail == StepProof.DetailSoldGoods;

    private static PlanningPath ReadyWorker()
    {
        var path = new PlanningPath();
        path.MarkEnqueued(1, Noon);
        var accepted = path.Deliver(WorkerPlanJson(), 1, WorkerId, AcceptContext(WorkerId), false);
        Assert.True(accepted.Accepted);
        return path;
    }

    private static bool Ask(
        PlanningPath path,
        PlanTrigger trigger,
        DateTime now,
        bool budgetOk = true,
        bool talking = false
    ) =>
        path.ShouldAsk(
            trigger,
            now,
            PlanRequestRules.DefaultCooldown,
            talking,
            default,
            paused: false,
            budgetOk,
            playerNearby: true
        );

    private static PlanAcceptContext AcceptContext(
        string characterId,
        int revision = 0,
        IReadOnlyList<string> skills = null,
        IReadOnlyList<string> people = null
    ) =>
        new(
            characterId,
            revision,
            Alive: true,
            OffWorld: false,
            Noon,
            skills ?? WorkerSkills(),
            Destinations: ["bank", "blacksmith", "mine", "graveyard"],
            People: people ?? [],
            Items: ["hatchet", "armor", "pickaxe"],
            Expansion.None
        );

    private static PlanProposal Proposal(string id, IReadOnlyList<ModelPlanStep> steps) =>
        new(id, 0, "goal", "reason", StepProof.SuccessItemObtained, steps, 1);

    private static Situation QuietWork() => new()
    {
        MustFlee = false,
        IsGhost = false,
        InCombat = false
    };

    private static List<ActionCandidate> Catalog(params string[] skills)
    {
        var list = new List<ActionCandidate>();

        for (var i = 0; i < skills.Length; i++)
        {
            list.Add(new ActionCandidate
            {
                Id = new ActionId("work:" + skills[i] + ":" + i),
                SkillKind = skills[i],
                RoutineId = "work",
                Step = new SkillStepDefinition { Skill = skills[i] }
            });
        }

        return list;
    }

    private static StepObservation Harvested(string skill) =>
        StepProof.Observe(
            skill,
            new WorldFacts(0, 0, 0, 0, false, false, "", "mine", false, false, "", true, false, 0, 80, false),
            new WorldFacts(0, 0, 8, 0, false, false, "", "mine", false, false, "", true, false, 0, 80, false),
            true,
            false,
            false
        );

    private static StepObservation NoProgress(string skill) =>
        StepProof.Observe(
            skill,
            new WorldFacts(0, 0, 0, 0, false, false, "", "mine", false, false, "", true, false, 0, 80, false),
            new WorldFacts(0, 0, 0, 0, false, false, "", "mine", false, false, "", true, false, 0, 80, false),
            true,
            false,
            false
        );

    private static string[] WorkerSkills() => [SkillKinds.Mine, SkillKinds.VendorSell, SkillKinds.VendorBuy];

    private static string[] FighterSkills() => [SkillKinds.UpgradeGear, SkillKinds.Hunt, SkillKinds.BankDeposit, SkillKinds.Heal];

    private static string[] SocialSkills() => [SkillKinds.Follow, SkillKinds.Hunt];

    private static PlanFacts WorkerFacts() => new()
    {
        Role = "Worker",
        SupportedActions = WorkerSkills(),
        Destinations = ["mine", "bank", "blacksmith"],
        Items = ["pickaxe"]
    };

    private static string WorkerPlanJson(string goal = "better tools") =>
        "{\"goal\":\"" + goal +
        "\",\"reason\":\"I am a miner and my pick is worn\",\"success\":\"item-obtained\"," +
        "\"steps\":[{\"do\":\"Mine\",\"why\":\"earn ore\",\"ref\":\"dest:mine\"}," +
        "{\"do\":\"VendorSell\",\"why\":\"sell ore\",\"ref\":\"dest:blacksmith\"}," +
        "{\"do\":\"VendorBuy\",\"why\":\"buy a pick\",\"ref\":\"item:pickaxe\"}]}";

    private static string FighterPlanJson() =>
        """{"goal":"a harder hunt","reason":"I fight and my kit is thin","success":"hunt-returned","steps":[{"do":"UpgradeGear","why":"buy armor","ref":"item:armor"},{"do":"Hunt","why":"fight","ref":"dest:graveyard"},{"do":"BankDeposit","why":"bank the take","ref":"dest:bank"}]}""";

    private static string FighterReviseJson() =>
        """{"goal":"recover then hunt","reason":"A death taught me caution","success":"hunt-returned","steps":[{"do":"Heal","why":"bind wounds","ref":""},{"do":"Hunt","why":"try a safer fight","ref":"dest:graveyard"}]}""";

    private static string SocialPlanJson() =>
        """{"goal":"hunt with a friend","reason":"Aria said yes","success":"hunted-together","steps":[{"do":"Follow","why":"stay with Aria","ref":"person:player:aria"},{"do":"Hunt","why":"fight together","ref":"dest:graveyard"}]}""";

    private sealed class FakeDecisionProvider
    {
        public string LastCallKind { get; private set; }

        public string Reply { get; set; }

        public string Send(PlanningPath path, PlanFacts facts)
        {
            LastCallKind = PlanningPath.CallKind;
            path.MarkEnqueued(1, Noon);
            return Reply;
        }
    }
}
