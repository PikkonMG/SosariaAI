using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

public class MurderReportTests
{
    private const string Swordsman = "Howell Weller";
    private const string Guard = "a guard";

    [Fact]
    public void KillerOf_TheKillingBlowFirst() =>
        Assert.Equal(Swordsman, MurderReport.KillerOf(Swordsman, Guard));

    [Fact]
    public void KillerOf_AGuardStrikeNamesTheLastDamager() =>
        Assert.Equal(Guard, MurderReport.KillerOf(null, Guard));

    [Fact]
    public void KillerOf_NobodyWhenNobodyDealtDamage() =>
        Assert.Null(MurderReport.KillerOf<string>(null, null));
}
