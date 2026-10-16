using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A mage opens on a melee foe with Paralyze, bursts with Explosion and a bolt that land
/// together in the running era, and gets out by Recall or Invisibility before it would run.
/// </summary>
public class MageTacticsRulesTests
{
    private const double Grandmaster = 100;
    private const double Adept = 75;
    private const double Journeyman = 65;
    private const int FullMana = 100;
    private const int ExplosionAndStrike = 60;
    private const int ExplosionAndBolt = 40;
    private const int ExplosionOnly = 25;
    private const int OpenGround = CastTiming.TopCircle;
    private const int SixthCircle = 6;
    private const int FifthCircle = 5;
    private const int Pressed = CastTiming.FirstCircle;
    private const double FreshFoe = 1.0;
    private const double BeatenFoe = 0.2;
    private const double FullHits = 1.0;
    private const double HalfHits = 0.5;
    private const double Scratched = 0.65;
    private const double FighterLine = RetreatRules.FighterLine;
    private const double LowLine = RetreatRules.MinLine;
    private const int OneAttacker = 1;
    private const int NoAttacker = 0;
    private const int FirstCircleSpell = 1;

    [Fact]
    public void OpensWithParalyze_OnlyTheFirstSpellOnAFreeMeleeFoe()
    {
        Assert.True(MageTacticsRules.OpensWithParalyze(false, CombatStyle.Melee, false, true, OpenGround));
        Assert.False(MageTacticsRules.OpensWithParalyze(true, CombatStyle.Melee, false, true, OpenGround));
        Assert.False(MageTacticsRules.OpensWithParalyze(false, CombatStyle.Mage, false, true, OpenGround));
        Assert.False(MageTacticsRules.OpensWithParalyze(false, CombatStyle.Melee, true, true, OpenGround));
        Assert.False(MageTacticsRules.OpensWithParalyze(false, CombatStyle.Melee, false, false, OpenGround));
    }

    [Fact]
    public void OpensWithParalyze_WaitsForTheRoomToSayIt()
    {
        Assert.True(MageTacticsRules.OpensWithParalyze(false, CombatStyle.Melee, false, true, FifthCircle));
        Assert.False(MageTacticsRules.OpensWithParalyze(false, CombatStyle.Melee, false, true, Pressed));
    }

    [Fact]
    public void BurstFollowUp_FlameStrikeWhenItCan_ElseEnergyBolt()
    {
        Assert.Equal(SpellKind.FlameStrike, MageTacticsRules.BurstFollowUp(Grandmaster, SpellBook.FlameStrike.Mana, TestSpellStock.Every));
        Assert.Equal(SpellKind.EnergyBolt, MageTacticsRules.BurstFollowUp(Grandmaster, SpellBook.EnergyBolt.Mana, TestSpellStock.Every));
        Assert.Equal(SpellKind.EnergyBolt, MageTacticsRules.BurstFollowUp(Adept, FullMana, TestSpellStock.Every));
        Assert.Null(MageTacticsRules.BurstFollowUp(Journeyman, FullMana, TestSpellStock.Every));
        Assert.Null(MageTacticsRules.BurstFollowUp(Grandmaster, SpellBook.EnergyBolt.Mana - 1, TestSpellStock.Every));
    }

    [Fact]
    public void LandsTogether_TheBoltsOfBothErasLandBesideTheBlast()
    {
        foreach (var aos in new[] { false, true })
        {
            Assert.True(MageTacticsRules.LandsTogether(SpellBook.EnergyBolt.Circle, aos));
            Assert.True(MageTacticsRules.LandsTogether(SpellBook.FlameStrike.Circle, aos));
        }
    }

    [Fact]
    public void LandsTogether_AFirstCircleSpellLandsLongBeforeTheBlast() =>
        Assert.False(MageTacticsRules.LandsTogether(FirstCircleSpell, aos: false));

    [Fact]
    public void ExplosionDelay_FollowsTheEra()
    {
        Assert.Equal(MageTacticsRules.PreAosExplosionDelayMs, MageTacticsRules.ExplosionDelayMs(aos: false));
        Assert.Equal(MageTacticsRules.AosExplosionDelayMs, MageTacticsRules.ExplosionDelayMs(aos: true));
        Assert.Equal(MageTacticsRules.PreAosRecoveryMs, MageTacticsRules.RecoveryMs(aos: false));
        Assert.Equal(MageTacticsRules.AosRecoveryMs, MageTacticsRules.RecoveryMs(aos: true));
    }

    [Fact]
    public void StartsBurst_NeedsTheManaForBothSpells()
    {
        Assert.True(MageTacticsRules.StartsBurst(Grandmaster, ExplosionAndStrike, OpenGround, FreshFoe, aos: false, TestSpellStock.Every));
        Assert.True(MageTacticsRules.StartsBurst(Grandmaster, ExplosionAndBolt, OpenGround, FreshFoe, aos: false, TestSpellStock.Every));
        Assert.False(MageTacticsRules.StartsBurst(Grandmaster, ExplosionOnly, OpenGround, FreshFoe, aos: false, TestSpellStock.Every));
        Assert.False(MageTacticsRules.StartsBurst(Journeyman, FullMana, OpenGround, FreshFoe, aos: false, TestSpellStock.Every));
    }

    [Fact]
    public void StartsBurst_NotOnABeatenFoeNorWhereTheWordsWouldBreak()
    {
        Assert.False(MageTacticsRules.StartsBurst(Grandmaster, FullMana, OpenGround, BeatenFoe, aos: false, TestSpellStock.Every));
        Assert.False(MageTacticsRules.StartsBurst(Grandmaster, FullMana, Pressed, FreshFoe, aos: false, TestSpellStock.Every));
    }

    [Fact]
    public void StartsBurst_TheFollowUpMustFitTheRoomToo() =>
        Assert.False(MageTacticsRules.StartsBurst(Grandmaster, ExplosionAndStrike, SixthCircle, FreshFoe, aos: false, TestSpellStock.Every));

    [Fact]
    public void Losing_NearTheRetreatLineOrLosingTheTrade()
    {
        Assert.True(MageTacticsRules.Losing(HalfHits, FighterLine, FightOutlook.Even, OneAttacker));
        Assert.False(MageTacticsRules.Losing(FullHits, FighterLine, FightOutlook.Even, OneAttacker));
        Assert.True(MageTacticsRules.Losing(HalfHits, LowLine, FightOutlook.Losing, OneAttacker));
        Assert.False(MageTacticsRules.Losing(Scratched, LowLine, FightOutlook.Losing, OneAttacker));
        Assert.False(MageTacticsRules.Losing(HalfHits, LowLine, FightOutlook.Even, OneAttacker));
        Assert.False(MageTacticsRules.Losing(HalfHits, FighterLine, FightOutlook.Losing, NoAttacker));
    }

    [Fact]
    public void Escape_RecallFirst_ThenInvisibility()
    {
        Assert.Equal(EscapeMove.Recall, MageTacticsRules.Escape(hidden: false, losing: true, recallAllowed: true, waitsForRecall: true, canVanish: true));
        Assert.Equal(EscapeMove.Vanish, MageTacticsRules.Escape(hidden: false, losing: true, recallAllowed: false, waitsForRecall: true, canVanish: true));
        Assert.Equal(EscapeMove.None, MageTacticsRules.Escape(hidden: false, losing: true, recallAllowed: false, waitsForRecall: true, canVanish: false));
        Assert.Equal(EscapeMove.None, MageTacticsRules.Escape(hidden: false, losing: false, recallAllowed: true, waitsForRecall: true, canVanish: true));
    }

    [Fact]
    public void Escape_HiddenRecallsWhenItMay_ElseHoldsStill()
    {
        Assert.Equal(EscapeMove.Recall, MageTacticsRules.Escape(hidden: true, losing: false, recallAllowed: true, waitsForRecall: false, canVanish: false));
        Assert.Equal(EscapeMove.StayHidden, MageTacticsRules.Escape(hidden: true, losing: false, recallAllowed: false, waitsForRecall: true, canVanish: true));
    }

    [Fact]
    public void Escape_HiddenWithNoRecallToWaitFor_Leaves() =>
        Assert.Equal(EscapeMove.Leave, MageTacticsRules.Escape(hidden: true, losing: false, recallAllowed: false, waitsForRecall: false, canVanish: true));

    [Fact]
    public void WaitsForRecall_OnlyWhileARefusalMayPassAndNotPastTheWait()
    {
        // With no rune home the hidden mage held still beside its foe for the rest of the fight.
        Assert.True(MageTacticsRules.WaitsForRecall(recallRefusedForGood: false, hiddenMs: 0));
        Assert.False(MageTacticsRules.WaitsForRecall(recallRefusedForGood: true, hiddenMs: 0));
        Assert.True(MageTacticsRules.WaitsForRecall(recallRefusedForGood: false, MageTacticsRules.HideWaitMs - 1));
        Assert.False(MageTacticsRules.WaitsForRecall(recallRefusedForGood: false, MageTacticsRules.HideWaitMs));
    }

    [Fact]
    public void DispelChance_FollowsTheEngineFormula()
    {
        // DispelSpell: (50 + 100 * (Magery - difficulty) / (focus * 2)) / 100.
        const double VortexDifficulty = 80;
        const double Focus = 20;
        const double Expert = 80;

        Assert.Equal(1.0, MageTacticsRules.DispelChance(Grandmaster, VortexDifficulty, Focus), 3);
        Assert.Equal(0.5, MageTacticsRules.DispelChance(Expert, VortexDifficulty, Focus), 3);
        Assert.Equal(0.0, MageTacticsRules.DispelChance(Grandmaster, VortexDifficulty, focus: 0), 3);
    }

    [Fact]
    public void Dispels_ASummonTheSpellWillLikelyUnmake()
    {
        Assert.True(MageTacticsRules.Dispels(MageTacticsRules.MinDispelChance, canDispel: true, OpenGround));
        Assert.True(MageTacticsRules.Dispels(FullHits, canDispel: true, SixthCircle));
        Assert.False(MageTacticsRules.Dispels(MageTacticsRules.MinDispelChance - 0.01, canDispel: true, OpenGround));
        Assert.False(MageTacticsRules.Dispels(FullHits, canDispel: false, OpenGround));
        Assert.False(MageTacticsRules.Dispels(FullHits, canDispel: true, FifthCircle));
    }

    [Fact]
    public void HasAnyAttack_AnAttackItsMageryHoldsAndItsPackStocks()
    {
        Assert.True(MageTacticsRules.HasAnyAttack(Grandmaster, SpellStock.None.With(SpellKind.FlameStrike)));
        Assert.False(MageTacticsRules.HasAnyAttack(Grandmaster, SpellStock.None.With(SpellKind.Heal)));
        Assert.False(MageTacticsRules.HasAnyAttack(Journeyman, SpellStock.None.With(SpellKind.FlameStrike)));
    }

    [Fact]
    public void Burst_NeedsTheReagentsOfBothSpells()
    {
        var noExplosion = TestSpellStock.Except(SpellKind.Explosion);
        var explosionOnly = SpellStock.None.With(SpellKind.Explosion);

        Assert.False(MageTacticsRules.StartsBurst(Grandmaster, FullMana, OpenGround, FreshFoe, aos: false, noExplosion));
        Assert.False(MageTacticsRules.StartsBurst(Grandmaster, FullMana, OpenGround, FreshFoe, aos: false, explosionOnly));
        Assert.Null(MageTacticsRules.BurstFollowUp(Grandmaster, FullMana, explosionOnly));
    }
}
