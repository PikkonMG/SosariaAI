using System;
using System.Globalization;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class ModelPlanTests
{
    private const string PlanId = "felucca:mira:1";
    private const int SavedRetries = 2;
    private static readonly DateTime Noon = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    private static string Ticks(DateTime time) => time.Ticks.ToString(CultureInfo.InvariantCulture);

    [Fact]
    public void FromLines_OlderSaveWithRetriesAndCreated_StillLoads()
    {
        var expires = Noon + ModelPlan.DefaultLife;
        string[] lines =
        [
            "v" + ModelPlan.CodecVersion,
            "id=" + PlanId,
            "rev=1",
            "who=felucca:mira",
            "goal=sell ore",
            "state=" + PlanState.Waiting,
            "index=0",
            "stepFail=0",
            "retries=" + SavedRetries,
            "created=" + Ticks(Noon),
            "expires=" + Ticks(expires),
            "trigger=" + PlanTrigger.NoPlan,
            "step=" + SkillKinds.Mine + "|ore to sell|"
        ];

        var plan = ModelPlan.FromLines(lines);

        Assert.NotNull(plan);
        Assert.Equal(PlanId, plan.Id);
        Assert.Equal(SkillKinds.Mine, plan.CurrentSkill);
        Assert.Equal(expires, plan.Expires);
    }

    [Fact]
    public void ToLines_WritesNoRetriesOrCreated()
    {
        var plan = new ModelPlan
        {
            Id = PlanId,
            Steps = [new ModelPlanStep(SkillKinds.Mine, "ore to sell", string.Empty)],
            State = PlanState.Waiting,
            Expires = Noon + ModelPlan.DefaultLife
        };

        var lines = plan.ToLines();

        Assert.DoesNotContain(lines, line => line.StartsWith("retries=", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.StartsWith("created=", StringComparison.Ordinal));
        Assert.Equal(plan.Expires, ModelPlan.FromLines(lines).Expires);
    }
}
