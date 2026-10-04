using System;
using System.Collections.Generic;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class TasteRulesTests
{
    [Fact]
    public void MayBegin_Null_IsFalse() =>
        Assert.False(TasteRules.MayBegin(null));

    [Fact]
    public void IsTaster_OnlyABuildThatTrainsTasteID()
    {
        var alchemist = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["Alchemy"] = 80, ["tasteid"] = 70 };
        var fighter = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["Swords"] = 80, ["Healing"] = 70 };

        Assert.True(TasteRules.IsTaster(alchemist));
        Assert.False(TasteRules.IsTaster(fighter));
        Assert.False(TasteRules.IsTaster(null));
    }

    [Fact]
    public void GatedKinds_KeepTheTasteStepFromNonTasters() =>
        Assert.Contains(SkillKinds.Taste, SkillReadiness.GatedKinds);
}
