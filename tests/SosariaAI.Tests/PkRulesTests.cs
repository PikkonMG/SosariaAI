using Server;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class PkRulesTests
{
    private const int BucsDenX = 2700;
    private const int BucsDenY = 2150;
    private const int BritainBankX = 1425;
    private const int BritainBankY = 1695;

    [Fact]
    public void IsRed_FourKills_IsFalse() =>
        Assert.False(PkRules.IsRed(PkRules.MurdersToRed - 1));

    [Fact]
    public void IsRed_FiveKills_IsTrue() =>
        Assert.True(PkRules.IsRed(PkRules.MurdersToRed));

    [Fact]
    public void InBuccaneersDen_InsideBox_IsTrue() =>
        Assert.True(PkRules.InBuccaneersDen(BucsDenX, BucsDenY));

    [Fact]
    public void InBuccaneersDen_BritainBank_IsFalse() =>
        Assert.False(PkRules.InBuccaneersDen(BritainBankX, BritainBankY));

    [Fact]
    public void MayAttack_NotPk_IsFalse() =>
        Assert.False(PkRules.MayAttack(false, false, false, false, false));

    [Fact]
    public void MayAttack_EitherUnderGuards_IsFalse()
    {
        Assert.False(PkRules.MayAttack(true, true, false, false, false));
        Assert.False(PkRules.MayAttack(true, false, true, false, false));
    }

    [Fact]
    public void MayAttack_VictimIsRed_IsFalse() =>
        Assert.False(PkRules.MayAttack(true, false, false, true, false));

    [Fact]
    public void MayAttack_InBucsDen_IsFalse() =>
        Assert.False(PkRules.MayAttack(true, false, false, false, true));

    [Fact]
    public void MayAttack_FeluccaWild_IsTrue() =>
        Assert.True(PkRules.MayAttack(true, false, false, false, false));

    [Fact]
    public void MayBankAt_BlueAnywhere_IsTrue()
    {
        Assert.True(PkRules.MayBankAt(false, BucsDenX, BucsDenY));
        Assert.True(PkRules.MayBankAt(false, BritainBankX, BritainBankY));
    }

    [Fact]
    public void MayBankAt_RedInBucsDen_IsTrue() =>
        Assert.True(PkRules.MayBankAt(true, BucsDenX, BucsDenY));

    [Fact]
    public void MayBankAt_RedAtBritainBank_IsFalse() =>
        Assert.False(PkRules.MayBankAt(true, BritainBankX, BritainBankY));

    [Fact]
    public void BucsDen_KeepsTheStockBoxAndSquare()
    {
        // The haven a stock shard runs with is the Den box the constants always named.
        Assert.Equal(PkRules.BucsDen, PkRules.Haven);
        Assert.Equal(FacetNames.Felucca, PkRules.BucsDen.Facet);
        Assert.True(PkRules.BucsDen.Contains(PkRules.BucsDenMinX, PkRules.BucsDenMinY));
        Assert.True(PkRules.BucsDen.Contains(PkRules.BucsDenMaxX, PkRules.BucsDenMaxY));
        Assert.False(PkRules.BucsDen.Contains(PkRules.BucsDenMinX - 1, PkRules.BucsDenMinY));
        Assert.False(PkRules.BucsDen.Contains(PkRules.BucsDenMaxX + 1, PkRules.BucsDenMaxY));
        Assert.False(PkRules.BucsDen.Contains(PkRules.BucsDenMaxX, PkRules.BucsDenMaxY + 1));
        Assert.True(PkRules.BucsDen.Contains(PkRules.BucsDenHaven.X, PkRules.BucsDenHaven.Y));
    }

    [Fact]
    public void RedHaven_HoldsEveryAreaAndNothingElse()
    {
        var haven = new RedHaven(
            "Custom",
            [new Rectangle2D(10, 10, 5, 5), new Rectangle2D(100, 100, 2, 2)],
            new Point3D(12, 12, 0)
        );

        Assert.True(haven.Contains(10, 10));
        Assert.True(haven.Contains(14, 14));
        Assert.False(haven.Contains(15, 14));
        Assert.True(haven.Contains(101, 101));
        Assert.False(haven.Contains(50, 50));
        Assert.False(new RedHaven("Custom", [], Point3D.Zero).Contains(0, 0));
    }
}
