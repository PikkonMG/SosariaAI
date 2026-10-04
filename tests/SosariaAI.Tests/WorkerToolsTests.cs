using Server.Items;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class WorkerToolsTests
{
    [Fact]
    public void ToolFor_EachHarvestTrade()
    {
        Assert.Equal(typeof(Pickaxe), WorkerTools.ToolFor(SkillKinds.Mine));
        Assert.Equal(typeof(Hatchet), WorkerTools.ToolFor(SkillKinds.Lumberjack));
        Assert.Equal(typeof(FishingPole), WorkerTools.ToolFor(SkillKinds.Fish));
        Assert.Null(WorkerTools.ToolFor(SkillKinds.Tavern));
    }

    [Fact]
    public void ShopsFor_SendsEachToolWhereItIsSold()
    {
        // The engine's blacksmith sells no hatchet: a woodcutter sent there failed three times.
        Assert.Equal([ShopFinder.WeaponsmithToken], WorkerTools.ShopsFor(typeof(Hatchet)));
        Assert.Equal([ShopFinder.FishermanToken], WorkerTools.ShopsFor(typeof(FishingPole)));
        Assert.Contains(ShopFinder.SmithToken, WorkerTools.ShopsFor(typeof(Pickaxe)));
        Assert.Empty(WorkerTools.ShopsFor(typeof(Longsword)));
    }

    [Fact]
    public void HasToolFor_ASkillWithNoTool_IsAlwaysReady() =>
        Assert.True(WorkerTools.HasToolFor(null, SkillKinds.Tavern));
}
