using System;
using System.Collections.Generic;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class BrowseAndPackTests
{
    private const int RollSweep = 500;

    [Fact]
    public void Browse_TriesEveryShopKindOnce()
    {
        var seen = new HashSet<string>();

        for (var attempt = 0; attempt < BrowseRules.ShopRoles.Length; attempt++)
        {
            Assert.True(seen.Add(BrowseRules.TokenAt(RollSweep, attempt)));
        }

        Assert.All(seen, token => Assert.StartsWith(DestinationCatalog.VendorTokenPrefix, token));
    }

    [Fact]
    public void Browse_LastsAFewMinutes()
    {
        for (var roll = -RollSweep; roll < RollSweep; roll++)
        {
            Assert.InRange(
                BrowseRules.Length(roll),
                TimeSpan.FromMinutes(BrowseRules.MinMinutes),
                TimeSpan.FromMinutes(BrowseRules.MaxMinutes)
            );
        }
    }

    [Fact]
    public void ShopJob_LooksOverTheWaresInTown()
    {
        // A shop visit with nothing on the list stood at the counter; now it looks round a shop.
        var plan = GoalPlanRules.Restore(JobRules.ShopPlan, 0, 0);
        var kinds = new List<string>();

        for (var i = 0; i < plan.Steps.Count; i++)
        {
            kinds.Add(plan.Steps[i].SkillKind);
        }

        Assert.Contains(SkillKinds.Browse, kinds);
        Assert.True(JobRules.IsDoable(JobKind.Shop, [SkillKinds.Browse]));
        Assert.Equal(
            "must be in town",
            ActionScorer.Unavailable(
                new ActionCandidate
                {
                    Id = new ActionId("browse:Browse:0"),
                    SkillKind = SkillKinds.Browse,
                    RoutineId = "browse",
                    Step = new SkillStepDefinition { Skill = SkillKinds.Browse }
                },
                new Situation { Needs = new NeedsSnapshot { HitsFraction = 1 }, InTownRegion = false },
                catalog: null,
                JobRules.GoalFor(JobKind.Shop)
            )
        );
    }

    [Fact]
    public void PackBeast_TheTrainersLlamaAndHorse()
    {
        Assert.True(PackAnimals.IsPackBeast(typeof(PackLlama)));
        Assert.True(PackAnimals.IsPackBeast(typeof(PackHorse)));
        Assert.False(PackAnimals.IsPackBeast(typeof(Horse)));
        Assert.Equal(PackAnimals.LlamaPrice, PackAnimals.PriceOf(typeof(PackLlama)));
        Assert.Equal(PackAnimals.HorsePrice, PackAnimals.PriceOf(typeof(PackHorse)));
        Assert.False(PackAnimals.WantsBeast(null));
    }

    [Fact]
    public void Cook_BuysItsPanAtTheTavern() =>
        Assert.Equal(ShopFinder.TavernToken, CookRules.Trade.ToolShopToken);
}
