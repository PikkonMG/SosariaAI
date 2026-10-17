using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A mage opens on a monster with Paralyze and on a person with Weaken or an arrow, bursts with Explosion and a bolt that land
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

    private const double WeakenRoll = 0.0;
    private const double ArrowRoll = 0.999;
    private const long Now = 100_000;
    private const int ClosingChaser = SpellBook.ClosingTiles;
    private const int FarChaser = SpellBook.ClosingTiles + 1;
    private const int AdjacentChaser = 1;

    [Fact]
    public void Opener_ParalyzeOnAFreeMonsterThatFightsUpCloseOrCasts()
    {
        Assert.Equal(SpellKind.Paralyze, Opener(false, false, CombatStyle.Melee, false, OpenGround, WeakenRoll));
        Assert.Equal(SpellKind.Paralyze, Opener(false, false, CombatStyle.Mage, false, OpenGround, WeakenRoll));
        Assert.Null(Opener(false, false, CombatStyle.Archer, false, OpenGround, WeakenRoll));
        Assert.Null(Opener(true, false, CombatStyle.Melee, false, OpenGround, WeakenRoll));
        Assert.Null(Opener(false, false, CombatStyle.Melee, true, OpenGround, WeakenRoll));
        Assert.Null(Opener(false, false, CombatStyle.Melee, false, Pressed, WeakenRoll));
    }

    [Fact]
    public void Opener_OnAPersonIsWeakenOrAMagicArrow()
    {
        Assert.Equal(SpellKind.Weaken, Opener(false, true, CombatStyle.Mage, false, Pressed, WeakenRoll));
        Assert.Equal(SpellKind.MagicArrow, Opener(false, true, CombatStyle.Melee, false, Pressed, ArrowRoll));
        Assert.Null(Opener(false, true, CombatStyle.Melee, true, OpenGround, WeakenRoll));
        Assert.Null(Opener(true, true, CombatStyle.Melee, false, OpenGround, WeakenRoll));
        Assert.Equal(
            SpellKind.MagicArrow,
            MageTacticsRules.Opener(false, true, CombatStyle.Melee, false, true, canWeaken: false, canArrow: true, OpenGround, WeakenRoll)
        );
    }

    [Fact]
    public void PoisonFirst_OnlyOnAClean_PersonNotSeenToCure()
    {
        Assert.True(MageTacticsRules.PoisonFirst(foeIsPerson: true, foePoisoned: false, cureSeen: false));
        Assert.False(MageTacticsRules.PoisonFirst(foeIsPerson: true, foePoisoned: false, cureSeen: true));
        Assert.False(MageTacticsRules.PoisonFirst(foeIsPerson: true, foePoisoned: true, cureSeen: false));
        Assert.False(MageTacticsRules.PoisonFirst(foeIsPerson: false, foePoisoned: false, cureSeen: false));
    }

    [Fact]
    public void RaisesReflect_BeforeTheFirstBlowOnAPerson_BeforeAos()
    {
        Assert.True(MageTacticsRules.RaisesReflect(true, true, false, false, true, OpenGround));
        Assert.False(MageTacticsRules.RaisesReflect(false, true, false, false, true, OpenGround));
        Assert.False(MageTacticsRules.RaisesReflect(true, false, false, false, true, OpenGround));
        Assert.False(MageTacticsRules.RaisesReflect(true, true, true, false, true, OpenGround));
        Assert.False(MageTacticsRules.RaisesReflect(true, true, false, true, true, OpenGround));
        Assert.False(MageTacticsRules.RaisesReflect(true, true, false, false, true, Pressed));
    }

    [Fact]
    public void ChaseCap_ShortWordsOnARunner()
    {
        Assert.Equal(MageTacticsRules.ChaseCircle, MageTacticsRules.ChaseCap(OpenGround, foeRunsLow: true));
        Assert.Equal(OpenGround, MageTacticsRules.ChaseCap(OpenGround, foeRunsLow: false));
        Assert.Equal(Pressed, MageTacticsRules.ChaseCap(Pressed, foeRunsLow: true));
        Assert.Equal(MageTacticsRules.ChaseCircle, SpellBook.Lightning.Circle);
    }

    [Fact]
    public void Against_LandsInsideTheWindow_WaitsWhenEarly_ElseNone()
    {
        Assert.Equal(BurstCall.Lands, MageTacticsRules.Against(Now, Now + MageTacticsRules.BurstWindowMs));
        Assert.Equal(BurstCall.Early, MageTacticsRules.Against(Now, Now + MageTacticsRules.BurstWindowMs + 1));
        Assert.Equal(BurstCall.None, MageTacticsRules.Against(Now + MageTacticsRules.BurstWindowMs + 1, Now));
    }

    [Fact]
    public void ExplosionLandMs_WordsCursorAndBlast() =>
        Assert.Equal(
            CastTiming.CastDelayMs(SpellBook.Explosion.Circle, false, false) + FootworkRules.CursorAnswerMs + MageTacticsRules.PreAosExplosionDelayMs,
            MageTacticsRules.ExplosionLandMs(aos: false)
        );

    [Fact]
    public void WallsOff_AMeleeChaserClosingIn_WithRoomForTheWall()
    {
        Assert.True(MageTacticsRules.WallsOff(true, ClosingChaser, OpenGround));
        Assert.False(MageTacticsRules.WallsOff(false, ClosingChaser, OpenGround));
        Assert.False(MageTacticsRules.WallsOff(true, FarChaser, OpenGround));
        Assert.False(MageTacticsRules.WallsOff(true, AdjacentChaser, OpenGround));
        Assert.False(MageTacticsRules.WallsOff(true, ClosingChaser, Pressed));
    }

    private static SpellKind? Opener(bool opened, bool person, CombatStyle style, bool held, int planCircle, double roll) =>
        MageTacticsRules.Opener(opened, person, style, held, canParalyze: true, canWeaken: true, canArrow: true, planCircle, roll);

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
