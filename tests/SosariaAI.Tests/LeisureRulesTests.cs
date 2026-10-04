using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class LeisureRulesTests
{
    private static readonly Point3D BritainBank = CharactersFile.DefaultBankSpot;

    [Fact]
    public void MayPick_KeepsNamedTownLandmarksNearHome()
    {
        Assert.True(
            LeisureRules.MayPick(
                "Britain West Bank",
                SightseeRules.KindBank,
                BritainBank,
                BritainBank,
                CareerSettings.DefaultLeisureRadius,
                sameFacet: true,
                canRoute: true
            )
        );
    }

    [Fact]
    public void MayPick_DropsDungeonInteriorsAndRawItemNames()
    {
        Assert.False(
            LeisureRules.MayPick(
                "The Painted Caves",
                SightseeRules.KindDungeon,
                new Point3D(1710, 1990, 0),
                BritainBank,
                CareerSettings.DefaultLeisureRadius,
                true,
                true
            )
        );
        Assert.False(
            LeisureRules.MayPick(
                "MiniatureMushroom 7032-411",
                SightseeRules.KindHunt,
                new Point3D(1500, 1600, 0),
                BritainBank,
                CareerSettings.DefaultLeisureRadius,
                true,
                true
            )
        );
        Assert.True(LeisureRules.IsRawItemName("Banker 1425-1690"));
    }

    [Fact]
    public void MayPick_DropsFarTargetsAndUnroutableOnes()
    {
        Assert.False(
            LeisureRules.MayPick(
                "Vesper Bank",
                SightseeRules.KindBank,
                new Point3D(2728, 893, 0),
                BritainBank,
                CareerSettings.DefaultLeisureRadius,
                true,
                true
            )
        );
        Assert.False(
            LeisureRules.MayPick(
                "Britain West Bank",
                SightseeRules.KindBank,
                BritainBank,
                BritainBank,
                CareerSettings.DefaultLeisureRadius,
                true,
                canRoute: false
            )
        );
    }
}
