using SosariaAI.Combat;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

public class AreaDifficultyTests
{
    private const int ExpectedTrollThreat = 130;
    private const int ExpectedRatThreat = 8;
    private const int ExpectedOrcThreat = 45;
    private const int TrollHits = 110;
    private const int TrollStrength = 190;
    private const int TrollAverageDamage = 11;
    private const int ExtraFoesForThree = 2;
    private const int ExtraFoesForFour = 3;
    private const string UnknownName = "Bogeyman";
    private const string TrollName = "Troll";
    private const string RatName = "Rat";
    private const string OrcName = "Orc";
    private const string ZombieName = "Zombie";
    private const string SkeletonName = "Skeleton";
    private const string DragonName = "Dragon";
    private const string GhoulName = "Ghoul";
    private const int ZombieThreat = 42;
    private const int SkeletonThreat = 49;
    private const int GhoulThreat = 65;
    private const int BossThreat = 790;
    private const int ZombieCount = 12;
    private const int SkeletonCount = 20;
    private const int GhoulCount = 6;
    private const int OneBoss = 1;

    private static readonly HostileStats LiveTroll = new(TrollHits, TrollStrength, TrollAverageDamage);

    [Fact]
    public void AreaScore_TrollArea_IsAboutOneTrollThreat()
    {
        var score = AreaDifficulty.AreaScore([TrollName]);
        Assert.Equal(ExpectedTrollThreat, score);
        Assert.Equal(ThreatRating.Score([LiveTroll]), score);
    }

    [Fact]
    public void AreaScore_RatArea_IsLow()
    {
        var score = AreaDifficulty.AreaScore([RatName]);
        Assert.Equal(ExpectedRatThreat, score);
        Assert.True(score < AreaDifficulty.DefaultUnknown);
        Assert.True(score < ExpectedTrollThreat);
    }

    [Fact]
    public void AreaScore_EmptyOrNull_IsZero()
    {
        Assert.Equal(0, AreaDifficulty.AreaScore(null));
        Assert.Equal(0, AreaDifficulty.AreaScore([]));
        Assert.Equal(0, AreaDifficulty.AreaScore(["", "  "]));
    }

    [Fact]
    public void AreaScore_Unknown_UsesDefaultUnknown()
    {
        Assert.Equal(AreaDifficulty.DefaultUnknown, AreaDifficulty.AreaScore([UnknownName]));
    }

    [Fact]
    public void AreaScore_TableNames_AreCaseInsensitive()
    {
        Assert.Equal(ExpectedTrollThreat, AreaDifficulty.AreaScore(["troll"]));
        Assert.Equal(ExpectedTrollThreat, AreaDifficulty.AreaScore(["TROLL"]));
        Assert.Equal(ExpectedRatThreat, AreaDifficulty.AreaScore(["rat"]));
    }

    [Fact]
    public void AreaScore_DuplicateNames_CountOnce()
    {
        Assert.Equal(ExpectedTrollThreat, AreaDifficulty.AreaScore([TrollName, "troll", "TROLL"]));
    }

    [Fact]
    public void SpawnScore_IsTheFoeFourInFiveStandAtOrBelow()
    {
        // Twelve zombies, twenty skeletons and six ghouls: the zombies are a third of the
        // spawn, the skeletons bring it past four in five, so the ground reads as its skeletons.
        HostileStats? Lookup(string name) =>
            name switch
            {
                ZombieName => new HostileStats(ZombieThreat, 0, 0),
                SkeletonName => new HostileStats(SkeletonThreat, 0, 0),
                GhoulName => new HostileStats(GhoulThreat, 0, 0),
                _ => null
            };

        var score = AreaDifficulty.SpawnScore(
            [new SpawnCount(ZombieName, ZombieCount), new SpawnCount(SkeletonName, SkeletonCount), new SpawnCount(GhoulName, GhoulCount)],
            Lookup
        );

        Assert.Equal(SkeletonThreat, score);
    }

    [Fact]
    public void SpawnScore_ALoneBossWeighsItsShare_HoweverFewStand()
    {
        HostileStats? Lookup(string name) =>
            name switch
            {
                SkeletonName => new HostileStats(SkeletonThreat, 0, 0),
                DragonName => new HostileStats(BossThreat, 0, 0),
                _ => null
            };

        var score = AreaDifficulty.SpawnScore(
            [new SpawnCount(SkeletonName, SkeletonCount), new SpawnCount(DragonName, OneBoss)],
            Lookup
        );

        Assert.Equal((int)(BossThreat * AreaDifficulty.BossShare), score);
        Assert.True(score > SkeletonThreat);
    }

    [Fact]
    public void SpawnScore_CountsANameMetTwiceOnce_AndNoSpawnIsZero()
    {
        HostileStats? Lookup(string name) =>
            name == SkeletonName ? new HostileStats(SkeletonThreat, 0, 0) : new HostileStats(GhoulThreat, 0, 0);

        // Two skeleton spawners outweigh the ghouls together, one alone does not.
        Assert.Equal(
            SkeletonThreat,
            AreaDifficulty.SpawnScore(
                [new SpawnCount(SkeletonName, GhoulCount * 2), new SpawnCount("skeleton", GhoulCount * 2), new SpawnCount(GhoulName, GhoulCount)],
                Lookup
            )
        );
        Assert.Equal(0, AreaDifficulty.SpawnScore(null));
        Assert.Equal(0, AreaDifficulty.SpawnScore([new SpawnCount(SkeletonName, 0), new SpawnCount(" ", ZombieCount)]));
    }

    [Fact]
    public void SpawnScore_CountsTheArtsOfACaster()
    {
        var bare = new HostileStats(SkeletonThreat * 4, 0, 0);
        var caster = bare with { Arts = new HostileArts(ThreatRating.MasterSpellSkill, false, ThreatRating.NoPoison, false) };

        Assert.True(
            AreaDifficulty.SpawnScore([new SpawnCount(DragonName, OneBoss)], _ => caster) >
            AreaDifficulty.SpawnScore([new SpawnCount(DragonName, OneBoss)], _ => bare)
        );
    }

    [Fact]
    public void AreaScore_FourUnique_UsesTopThree()
    {
        var score = AreaDifficulty.AreaScore([RatName, SkeletonName, ZombieName, OrcName]);
        var expected = Combined(ExpectedOrcThreat, ExtraFoesForThree);
        var allFour = Combined(ExpectedOrcThreat, ExtraFoesForFour);
        Assert.Equal(expected, score);
        Assert.NotEqual(allFour, score);
    }

    [Fact]
    public void AreaScore_LookupStats_UseThreatRating()
    {
        Assert.Equal(ThreatRating.Score([LiveTroll]), AreaDifficulty.AreaScore([TrollName], _ => LiveTroll));
    }

    [Fact]
    public void AreaScore_NullLookup_UsesTable()
    {
        Assert.Equal(ExpectedTrollThreat, AreaDifficulty.AreaScore([TrollName], _ => null));
        Assert.Equal(AreaDifficulty.DefaultUnknown, AreaDifficulty.AreaScore([UnknownName], _ => null));
    }

    [Fact]
    public void AreaScore_LookupOverridesTable()
    {
        var stronger = new HostileStats(200, 200, 20);
        Assert.Equal(ThreatRating.Score([stronger]), AreaDifficulty.AreaScore([RatName], _ => stronger));
    }

    [Fact]
    public void AreaScore_LookupTopThree_ScoresStrongestHostiles()
    {
        HostileStats? Lookup(string name) =>
            name switch
            {
                DragonName => new HostileStats(200, 200, 20),
                TrollName => LiveTroll,
                OrcName => new HostileStats(ExpectedOrcThreat, 0, 0),
                RatName => new HostileStats(ExpectedRatThreat, 0, 0),
                _ => null
            };

        var score = AreaDifficulty.AreaScore([RatName, OrcName, TrollName, DragonName], Lookup);
        var dragon = Lookup(DragonName)!.Value;
        var troll = Lookup(TrollName)!.Value;
        var orc = Lookup(OrcName)!.Value;
        Assert.Equal(ThreatRating.Score([dragon, troll, orc]), score);
    }

    [Fact]
    public void FromGroup_ForestWoodYew_IsLumber()
    {
        Assert.Equal(ResourceKind.Lumber, ResourceKind.FromGroup("Britain Forest"));
        Assert.Equal(ResourceKind.Lumber, ResourceKind.FromGroup("Yew Wood"));
        Assert.Equal(ResourceKind.Lumber, ResourceKind.FromGroup("yew"));
    }

    [Fact]
    public void FromGroup_MineOreMountainMinoc_IsMine()
    {
        Assert.Equal(ResourceKind.Mine, ResourceKind.FromGroup("Minoc Mine"));
        Assert.Equal(ResourceKind.Mine, ResourceKind.FromGroup("ore vein"));
        Assert.Equal(ResourceKind.Mine, ResourceKind.FromGroup("mountain"));
        Assert.Equal(ResourceKind.Mine, ResourceKind.FromGroup("MINOC"));
    }

    [Fact]
    public void FromGroup_UnknownOrEmpty_IsNull()
    {
        Assert.Null(ResourceKind.FromGroup("Britain"));
        Assert.Null(ResourceKind.FromGroup("East Docks"));
        Assert.Null(ResourceKind.FromGroup(null));
        Assert.Null(ResourceKind.FromGroup(""));
    }

    private static int Combined(int strongest, int extraFoes) =>
        (int)(strongest * (ThreatRating.SingleFoeFactor + ThreatRating.ExtraFoeShare * extraFoes));
}
