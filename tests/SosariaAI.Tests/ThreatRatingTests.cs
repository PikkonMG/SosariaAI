using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class ThreatRatingTests
{
    private const int TrollHits = 110;
    private const int TrollStrength = 190;
    private const int TrollAverageDamage = 11;
    private const int VeteranPower = 120;
    private const int FreshPower = 70;
    private const int NoAlliesPower = 0;
    private const int HugeThreat = 10_000;
    private const int CoinFlipSlack = 15;
    private const double FullHitsFraction = 1.0;
    private const double HitsBelowOpen = 0.01;

    private static readonly HostileStats Troll = new(TrollHits, TrollStrength, TrollAverageDamage);
    private static readonly HostileStats[] OneTroll = [Troll];
    private static readonly HostileStats[] TwoTrolls = [Troll, Troll];

    [Fact]
    public void Score_NullOrEmpty_IsZero()
    {
        Assert.Equal(0, ThreatRating.Score(null));
        Assert.Equal(0, ThreatRating.Score([]));
    }

    [Fact]
    public void Score_OneTroll_IsCoinFlipWithVeteran()
    {
        var threat = ThreatRating.Score(OneTroll);
        Assert.InRange(threat, VeteranPower - CoinFlipSlack, VeteranPower + CoinFlipSlack);
        Assert.True(threat <= VeteranPower * ThreatRating.DefaultThreatMultiple);
        Assert.True(threat > FreshPower * ThreatRating.DefaultThreatMultiple);
    }

    [Fact]
    public void Score_TwoTrolls_IsAbout185TimesTheStronger()
    {
        var one = ThreatRating.Score(OneTroll);
        var two = ThreatRating.Score(TwoTrolls);
        var expected = (int)(one * (ThreatRating.SingleFoeFactor + ThreatRating.ExtraFoeShare));
        Assert.Equal(expected, two);
        Assert.True(two > VeteranPower * ThreatRating.DefaultThreatMultiple);
    }

    [Fact]
    public void ShouldEngage_AlreadyAttacked_IsTrueEvenIfThreatHuge()
    {
        Assert.True(
            Engage(
                FreshPower,
                HugeThreat,
                hitsFraction: ThreatRating.OpenFightHitsFraction - HitsBelowOpen,
                hasHealing: false,
                alreadyAttacked: true
            )
        );
    }

    [Fact]
    public void ShouldEngage_VeteranVsOneTroll_IsTrue() =>
        Assert.True(Engage(VeteranPower, ThreatRating.Score(OneTroll)));

    [Fact]
    public void ShouldEngage_VeteranVsTwoTrolls_IsFalse() =>
        Assert.False(Engage(VeteranPower, ThreatRating.Score(TwoTrolls)));

    [Fact]
    public void ShouldEngage_FreshVsOneTroll_IsFalse() =>
        Assert.False(Engage(FreshPower, ThreatRating.Score(OneTroll)));

    [Fact]
    public void ShouldEngage_LowHits_DoesNotOpen() =>
        Assert.False(
            Engage(
                VeteranPower,
                ThreatRating.Score(OneTroll),
                hitsFraction: ThreatRating.OpenFightHitsFraction - HitsBelowOpen
            )
        );

    [Fact]
    public void ShouldEngage_HitsAtOpenFraction_MayOpen() =>
        Assert.True(
            Engage(
                VeteranPower,
                ThreatRating.Score(OneTroll),
                hitsFraction: ThreatRating.OpenFightHitsFraction
            )
        );

    [Fact]
    public void ShouldEngage_VeteranWithoutHealingVsOneTroll_IsFalse() =>
        Assert.False(Engage(VeteranPower, ThreatRating.Score(OneTroll), hasHealing: false));

    [Fact]
    public void ShouldEngage_VeteranWithoutHealingVsTwoTrolls_IsFalse() =>
        Assert.False(Engage(VeteranPower, ThreatRating.Score(TwoTrolls), hasHealing: false));

    [Fact]
    public void ShouldEngage_PartyShareLetsTwoVeteransTakeTwoTrolls()
    {
        Assert.True(
            Engage(
                VeteranPower,
                ThreatRating.Score(TwoTrolls),
                alliesPower: VeteranPower
            )
        );
    }

    private static bool Engage(
        int power,
        int threat,
        double hitsFraction = FullHitsFraction,
        bool hasHealing = true,
        int alliesPower = NoAlliesPower,
        bool alreadyAttacked = false
    ) =>
        ThreatRating.ShouldEngage(
            power,
            threat,
            hitsFraction,
            hasHealing,
            alliesPower,
            ThreatRating.DefaultThreatMultiple,
            ThreatRating.OpenFightHitsFraction,
            alreadyAttacked
        );

    [Fact]
    public void Score_RisesWithEveryExtraFoeInTheRoom()
    {
        var troll = new HostileStats(110, 190, 11);
        var one = ThreatRating.Score([troll]);
        var two = ThreatRating.Score([troll, troll]);
        var four = ThreatRating.Score([troll, troll, troll, troll]);

        Assert.True(two > one);
        Assert.True(four > two);
    }

    [Fact]
    public void ShouldEngage_VeteranTakesOneTrollButNotFour()
    {
        var troll = new HostileStats(110, 190, 11);
        const int veteranPower = 120;

        Assert.True(Engage(veteranPower, ThreatRating.Score([troll])));
        Assert.False(Engage(veteranPower, ThreatRating.Score([troll, troll, troll, troll])));
    }

    private static bool Engage(int power, int threat) =>
        ThreatRating.ShouldEngage(
            power,
            threat,
            hitsFraction: 1.0,
            hasHealing: true,
            alliesPower: 0,
            ThreatRating.DefaultThreatMultiple,
            ThreatRating.OpenFightHitsFraction,
            alreadyAttacked: false
        );

    [Fact]
    public void ArtsFactor_IsOneForAFoeWithNone_AndOfOneKeepsTheBareScore()
    {
        Assert.Equal(1.0, ThreatRating.ArtsFactor(default));
        Assert.Equal(TrollHits + TrollStrength / ThreatRating.StrengthDivisor + TrollAverageDamage, ThreatRating.OfOne(Troll));
    }

    [Fact]
    public void ArtsFactor_CountsSpellsByMagery_BreathPoisonByLevel_AndRange()
    {
        const int noviceMagery = 30;
        const int lethalPoison = 5;
        const int lesserPoison = 1;
        var dreadSpider = new HostileArts(DreadSpiderMagery, false, lethalPoison, false);

        Assert.Equal(1 + ThreatRating.MasterSpellShare, ThreatRating.ArtsFactor(new HostileArts(ThreatRating.MasterSpellSkill, false, 0, false)), 3);
        Assert.Equal(1 + ThreatRating.AdeptSpellShare, ThreatRating.ArtsFactor(new HostileArts(ThreatRating.AdeptSpellSkill, false, 0, false)), 3);
        Assert.Equal(1 + ThreatRating.NoviceSpellShare, ThreatRating.ArtsFactor(new HostileArts(noviceMagery, false, 0, false)), 3);
        Assert.Equal(1 + ThreatRating.BreathShare, ThreatRating.ArtsFactor(new HostileArts(0, true, 0, false)), 3);
        Assert.Equal(1 + ThreatRating.PoisonShareBase, ThreatRating.ArtsFactor(new HostileArts(0, false, lesserPoison, false)), 3);
        Assert.Equal(1 + ThreatRating.RangedShare, ThreatRating.ArtsFactor(new HostileArts(0, false, 0, true)), 3);
        Assert.Equal(
            1 + ThreatRating.AdeptSpellShare + ThreatRating.PoisonShareBase + (lethalPoison - 1) * ThreatRating.PoisonSharePerLevel,
            ThreatRating.ArtsFactor(dreadSpider),
            3
        );
    }

    [Fact]
    public void Score_ADreadSpidersSpellsAndPoison_PutItPastAFighterItsBlowsAloneWouldNot()
    {
        // Five fighters of power 128 to 225 died to dread spiders in the Trinsic Passage.
        var blows = new HostileStats(DreadSpiderHits, DreadSpiderStrength, DreadSpiderDamage);
        var spider = blows with { Arts = new HostileArts(DreadSpiderMagery, false, LethalPoisonLevel, false) };

        Assert.True(Engage(DreadSpiderVictimPower, ThreatRating.Score([blows])));
        Assert.False(Engage(DreadSpiderVictimPower, ThreatRating.Score([spider])));
    }

    private const int DreadSpiderHits = 201;
    private const int DreadSpiderStrength = 219;
    private const int DreadSpiderDamage = 11;
    private const int DreadSpiderMagery = 72;
    private const int LethalPoisonLevel = 5;
    private const int DreadSpiderVictimPower = 200;
}
