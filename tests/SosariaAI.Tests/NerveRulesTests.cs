using SosariaAI.Combat;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class NerveRulesTests
{
    private const double Brave = 1.0;
    private const double Timid = 0.0;
    private const double Neutral = PersonaDrives.NeutralValue;
    private const int ReferencePower = NerveRules.ReferencePower;
    private const int VeryStrong = 1000;
    private const int VeryWeak = 0;
    private const long CentreSeed = NerveRules.JitterSteps / 2;
    private const long LowSeed = 0;
    private const long HighSeed = NerveRules.JitterSteps - 1;
    private const long AnySeed = 12345;
    private const double Tolerance = 1e-9;
    private const int Power = 100;
    private const int OneAlly = 1;
    private const int TwoAllies = 2;
    private const double NoOffset = 0;
    private const int ManyAllies = 10;
    private const int OneTrollThreat = 130;
    private const int TwoTrollThreat = 240;
    private const int WeakFreshPower = 40;
    private const int VeteranPower = 117;
    private const double FullHits = 1.0;
    private const double Multiple = ThreatRating.DefaultThreatMultiple;
    private const double StartLine = 0.5;
    private const int NoAllies = 0;
    private const int OneExtra = 1;
    private const int NoExtras = 0;

    [Fact]
    public void Of_NeutralFighterAtReference_IsBaseNerve() =>
        Assert.Equal(
            NerveRules.BaseNerve,
            NerveRules.Of(Neutral, Neutral, CharacterRole.Fighter, false, ReferencePower, CentreSeed),
            Tolerance
        );

    [Fact]
    public void Of_BraveOutranksTimid()
    {
        var brave = NerveRules.Of(Brave, Timid, CharacterRole.Fighter, false, ReferencePower, CentreSeed);
        var timid = NerveRules.Of(Timid, Brave, CharacterRole.Fighter, false, ReferencePower, CentreSeed);

        Assert.True(brave > timid);
    }

    [Fact]
    public void Of_WorkerHasLessNerveThanFighter()
    {
        var worker = NerveRules.Of(Neutral, Neutral, CharacterRole.Worker, false, ReferencePower, CentreSeed);
        var fighter = NerveRules.Of(Neutral, Neutral, CharacterRole.Fighter, false, ReferencePower, CentreSeed);

        Assert.True(worker < fighter);
    }

    [Fact]
    public void Of_VeteranAndPowerRaiseNerve()
    {
        var plain = NerveRules.Of(Neutral, Neutral, CharacterRole.Fighter, false, ReferencePower, CentreSeed);

        Assert.True(NerveRules.Of(Neutral, Neutral, CharacterRole.Fighter, true, ReferencePower, CentreSeed) > plain);
        Assert.True(NerveRules.Of(Neutral, Neutral, CharacterRole.Fighter, false, VeryStrong, CentreSeed) > plain);
    }

    [Fact]
    public void Of_StaysInsideBounds()
    {
        Assert.Equal(NerveRules.MaxNerve, NerveRules.Of(Brave, Timid, CharacterRole.Fighter, true, VeryStrong, HighSeed), Tolerance);
        Assert.True(NerveRules.Of(Timid, Brave, CharacterRole.Worker, false, VeryWeak, LowSeed) >= NerveRules.MinNerve);
    }

    [Fact]
    public void Of_SameInputs_SameNerve() =>
        Assert.Equal(
            NerveRules.Of(Brave, Neutral, CharacterRole.Fighter, false, Power, AnySeed),
            NerveRules.Of(Brave, Neutral, CharacterRole.Fighter, false, Power, AnySeed)
        );

    [Fact]
    public void PowerShift_IsCapped()
    {
        Assert.Equal(NerveRules.MaxPowerShift, NerveRules.PowerShift(VeryStrong), Tolerance);
        Assert.Equal(-NerveRules.MaxPowerShift, NerveRules.PowerShift(VeryWeak), Tolerance);
    }

    [Fact]
    public void Jitter_SpansHalfSpanEachWay()
    {
        Assert.Equal(-NerveRules.JitterSpan / 2, NerveRules.Jitter(LowSeed), Tolerance);
        Assert.Equal(NerveRules.JitterSpan / 2, NerveRules.Jitter(HighSeed), Tolerance);
        Assert.Equal(NoOffset, NerveRules.Jitter(CentreSeed), Tolerance);
    }

    [Fact]
    public void DarePower_AlliesOnFoeRaiseDare_Capped()
    {
        var alone = NerveRules.DarePower(Power, NerveRules.BaseNerve, NoAllies);
        var withOne = NerveRules.DarePower(Power, NerveRules.BaseNerve, OneAlly);
        var withMany = NerveRules.DarePower(Power, NerveRules.BaseNerve, ManyAllies);
        var capped = NerveRules.DarePower(Power, NerveRules.BaseNerve, NerveRules.MaxCountedAlliesOnFoe);

        Assert.Equal(Power, alone);
        Assert.True(withOne > alone);
        Assert.Equal(capped, withMany);
    }

    [Fact]
    public void DarePower_EachAllyOnTheFoeAddsSixtyPercent()
    {
        const int SixtyPercentMore = 160;

        Assert.Equal(SixtyPercentMore, NerveRules.DarePower(Power, NerveRules.BaseNerve, OneAlly));
    }

    [Fact]
    public void Decide_AlreadyAttacked_Engages() =>
        Assert.Equal(EngageChoice.Engage, NerveRules.Decide(Facts(WeakFreshPower, TwoTrollThreat, OneTrollThreat, OneExtra) with { AlreadyAttacked = true }));

    [Fact]
    public void Decide_RoomWithinDare_Engages() =>
        Assert.Equal(EngageChoice.Engage, NerveRules.Decide(Facts(VeteranPower, OneTrollThreat, OneTrollThreat, NoExtras) with { HasHealing = true }));

    [Fact]
    public void Decide_GroupTooMuchButOneFits_Pulls() =>
        Assert.Equal(EngageChoice.PullStraggler, NerveRules.Decide(Facts(VeteranPower, TwoTrollThreat, OneTrollThreat, OneExtra) with { HasHealing = true }));

    [Fact]
    public void Decide_TooMuchForAnyPull_Declines() =>
        Assert.Equal(EngageChoice.Decline, NerveRules.Decide(Facts(WeakFreshPower, TwoTrollThreat, OneTrollThreat, OneExtra)));

    [Fact]
    public void Decide_FriendsOnFoe_TurnDeclineIntoEngage()
    {
        var alone = Facts(VeteranPower, TwoTrollThreat, OneTrollThreat, NoExtras) with { HasHealing = true };

        Assert.Equal(EngageChoice.Decline, NerveRules.Decide(alone));
        Assert.Equal(EngageChoice.Engage, NerveRules.Decide(alone with { AlliesOnFoe = TwoAllies }));
    }

    [Fact]
    public void Decide_BelowStartLine_Declines() =>
        Assert.Equal(
            EngageChoice.Decline,
            NerveRules.Decide(Facts(VeteranPower, OneTrollThreat, OneTrollThreat, NoExtras) with { HitsFraction = StartLine / 2 })
        );

    private static EngageFacts Facts(int power, int roomThreat, int pullThreat, int extras) =>
        new(
            power,
            NerveRules.BaseNerve,
            NoAllies,
            NoAllies,
            FullHits,
            false,
            Multiple,
            StartLine,
            roomThreat,
            pullThreat,
            extras,
            false
        );
}
