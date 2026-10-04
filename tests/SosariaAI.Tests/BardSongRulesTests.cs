using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class BardSongRulesTests
{
    private const double Skilled = 80;
    private const double Unskilled = 40;
    private const double Tolerance = 1e-9;
    private const double Healthy = 0.9;
    private const double Hurt = 0.3;
    private const double StartLine = 0.5;
    private const int OneAttacker = 1;
    private const int TwoAttackers = 2;

    [Fact]
    public void Pick_ProvokesOntoAPartner()
    {
        Assert.Equal(CombatSong.Provoke, Pick(Skilled, Unskilled));
        Assert.Equal(CombatSong.None, Pick(Unskilled, Unskilled));
        Assert.Equal(CombatSong.None, Pick(Skilled, Unskilled, hasPartner: false));
    }

    [Fact]
    public void Pick_NeverProvokesAnAttackerTheEngineRefuses() =>
        Assert.Equal(CombatSong.None, Pick(Skilled, Unskilled, provocable: false));

    [Fact]
    public void Pick_CalmsOnlyWhenTheFightGoesBadly()
    {
        Assert.Equal(CombatSong.Peace, Pick(Unskilled, Skilled, hasPartner: false, goingBadly: true));
        Assert.Equal(CombatSong.None, Pick(Unskilled, Skilled, hasPartner: false));
        Assert.Equal(CombatSong.Peace, Pick(Skilled, Skilled, hasPartner: false, goingBadly: true));
    }

    [Fact]
    public void Pick_CalmsNoUncalmableAttacker()
    {
        Assert.Equal(CombatSong.None, Pick(Unskilled, Skilled, hasPartner: false, calmable: false, goingBadly: true));
        Assert.Equal(CombatSong.Peace, Pick(Skilled, Skilled, provocable: false, goingBadly: true));
    }

    [Fact]
    public void Pick_NeedsAnInstrumentAndACreatureOnTheBard()
    {
        Assert.Equal(CombatSong.None, Pick(Skilled, Skilled, hasInstrument: false, goingBadly: true));
        Assert.Equal(CombatSong.None, Pick(Skilled, Skilled, creatureOnSelf: false, goingBadly: true));
    }

    [Fact]
    public void NoSongRetry_IsShorterThanASongRest() =>
        Assert.InRange(BardSongRules.NoSongRetryMs, 1, BardSongRules.MinRestMs - 1);

    [Fact]
    public void CanSing_EitherSongAtTheBar()
    {
        Assert.True(BardSongRules.CanSing(BardSongRules.MinSongSkill, Unskilled));
        Assert.True(BardSongRules.CanSing(Unskilled, BardSongRules.MinSongSkill));
        Assert.False(BardSongRules.CanSing(Unskilled, Unskilled));
    }

    [Fact]
    public void CalmSeconds_FourAndATenthOfTheSkill() =>
        Assert.Equal(BardSongRules.CalmBaseSeconds + Skilled / BardSongRules.CalmSkillDivisor, BardSongRules.CalmSeconds(Skilled), Tolerance);

    [Fact]
    public void GoingBadly_LosingHurtOrOutnumbered()
    {
        Assert.False(BardSongRules.GoingBadly(FightOutlook.Even, Healthy, StartLine, OneAttacker));
        Assert.True(BardSongRules.GoingBadly(FightOutlook.Losing, Healthy, StartLine, OneAttacker));
        Assert.True(BardSongRules.GoingBadly(FightOutlook.Winning, Hurt, StartLine, OneAttacker));
        Assert.True(BardSongRules.GoingBadly(FightOutlook.Even, Healthy, StartLine, TwoAttackers));
    }

    private static CombatSong Pick(
        double provocation,
        double peacemaking,
        bool hasInstrument = true,
        bool creatureOnSelf = true,
        bool provocable = true,
        bool hasPartner = true,
        bool calmable = true,
        bool goingBadly = false
    ) =>
        BardSongRules.Pick(provocation, peacemaking, hasInstrument, creatureOnSelf, provocable, hasPartner, calmable, goingBadly);
}
