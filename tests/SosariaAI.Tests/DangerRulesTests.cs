using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class DangerRulesTests
{
    [Fact]
    public void MustFlee_WorkerWhenThreatIsInSight()
    {
        Assert.True(DangerRules.MustFlee(CharacterRole.Worker, threat: ThreatRating.MinThreatToFlee));
        Assert.False(DangerRules.MustFlee(CharacterRole.Worker, threat: 0));
        Assert.False(DangerRules.MustFlee(CharacterRole.Worker, threat: 10));
        Assert.False(DangerRules.MustFlee(CharacterRole.Fighter, threat: ThreatRating.MinThreatToFlee));
    }

    [Fact]
    public void IsIntolerable_WhenThreatBeatsPower()
    {
        Assert.True(
            DangerRules.IsIntolerable(
                power: 31,
                threat: 130,
                hitsFraction: 1,
                hasHealing: false,
                alliesPower: 0,
                threatMultiple: ThreatRating.DefaultThreatMultiple
            )
        );
        Assert.False(
            DangerRules.IsIntolerable(
                power: 200,
                threat: 40,
                hitsFraction: 1,
                hasHealing: true,
                alliesPower: 0,
                threatMultiple: ThreatRating.DefaultThreatMultiple
            )
        );
    }

    [Fact]
    public void ShouldFight_AnAttackedFighterAnswersUpToHalfAgainItsDare()
    {
        const int Power = 100;
        const int JustOver = 120;
        const int FarPast = 200;
        const double Healthy = 1.0;
        const double BelowLine = 0.3;
        const double Multiple = 1.0;
        const int NoAllies = 0;

        Assert.False(ThreatRating.ShouldEngage(Power, JustOver, Healthy, true, NoAllies, Multiple, ThreatRating.OpenFightHitsFraction, false));
        Assert.True(DangerRules.ShouldFight(Power, JustOver, Healthy, hasHealing: true, alliesPower: NoAllies, threatMultiple: Multiple));
        Assert.False(DangerRules.ShouldFight(Power, FarPast, Healthy, hasHealing: true, alliesPower: NoAllies, threatMultiple: Multiple));
        Assert.False(DangerRules.ShouldFight(Power, JustOver, BelowLine, hasHealing: true, alliesPower: NoAllies, threatMultiple: Multiple));
    }

    [Fact]
    public void Overwhelms_PastHalfAgainTheDare_FriendsCounted()
    {
        const int Dare = 100;
        const int AtTheBound = 150;
        const int PastTheBound = 151;
        const int PartyPower = 100;
        const double Multiple = 1.0;
        const int NoAllies = 0;

        Assert.False(DangerRules.Overwhelms(AtTheBound, Dare, NoAllies, Multiple));
        Assert.True(DangerRules.Overwhelms(PastTheBound, Dare, NoAllies, Multiple));
        Assert.False(DangerRules.Overwhelms(PastTheBound, Dare, PartyPower, Multiple));
    }

    [Fact]
    public void Overwhelms_NeverAFoeTheDareWouldStart()
    {
        const int Dare = 100;
        const int NoAllies = 0;
        var startable = (int)(Dare * ThreatRating.DefaultThreatMultiple);

        Assert.True(ThreatRating.ShouldEngage(Dare, startable, 1.0, true, NoAllies, ThreatRating.DefaultThreatMultiple, 0, false));
        Assert.False(DangerRules.Overwhelms(startable, Dare, NoAllies, ThreatRating.DefaultThreatMultiple));
    }
}
