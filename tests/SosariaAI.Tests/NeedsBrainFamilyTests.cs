using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class NeedsBrainFamilyTests
{
    private const string TownRoutine = "town";
    private const string GraveyardRoutine = "graveyard";
    private const string DespiseRoutine = "despise";
    private const string TradeRoutine = "wts-bank";
    private const string SellRoutine = "vendorsell";
    private const string PkRoutine = "pk-wild";
    private const string FollowRoutine = "follow-bran";
    private const string TavernRoutine = "tavern";
    private const string VisitRoutine = "visit-friend";
    private const string SightseeRoutine = "sightsee";
    private const string LoiterRoutine = "loiter";
    private const string LookRoutine = "look-around";
    private const string PubRoutine = "pub";
    private const string LumberRoutine = "lumber";
    private const double PackedGreedTradeScore = 1.0 + PersonaDrives.MaxValue * NeedsBrain.GreedWorkWeight + 1.0;
    private const double BravePkScore = 1.0 + PersonaDrives.MaxValue * NeedsBrain.DungeonWhenBraveWeight;
    private const double HurtBravePkScore = BravePkScore - NeedsBrain.HuntPenaltyWhenHurt;
    private const double HurtHits = 0.2;
    private const double SocialGreed = 0.2;
    private const double SocialCaution = 0.8;
    private const double SocialValor = 0.3;

    [Theory]
    [InlineData(TownRoutine, RoutineFamily.Rest)]
    [InlineData(GraveyardRoutine, RoutineFamily.Hunt)]
    [InlineData(DespiseRoutine, RoutineFamily.Dungeon)]
    [InlineData(TradeRoutine, RoutineFamily.Trade)]
    [InlineData(SellRoutine, RoutineFamily.Trade)]
    [InlineData(PkRoutine, RoutineFamily.Pk)]
    [InlineData(FollowRoutine, RoutineFamily.Party)]
    [InlineData(TavernRoutine, RoutineFamily.Leisure)]
    [InlineData(VisitRoutine, RoutineFamily.Leisure)]
    [InlineData(SightseeRoutine, RoutineFamily.Leisure)]
    [InlineData(LoiterRoutine, RoutineFamily.Leisure)]
    [InlineData(LookRoutine, RoutineFamily.Leisure)]
    [InlineData(PubRoutine, RoutineFamily.Leisure)]
    [InlineData(LumberRoutine, RoutineFamily.Work)]
    public void FamilyOf_MapsRoutineId(string routineId, RoutineFamily expected) =>
        Assert.Equal(expected, NeedsBrain.FamilyOf(routineId));

    [Fact]
    public void Score_TradePackedGreed_IsHigh()
    {
        var packedGreedy = NeedsBrain.Score(
            RoutineFamily.Trade,
            new NeedsSnapshot
            {
                PackFillFraction = NeedsBrain.PackedFraction,
                Drives = new PersonaDrives(PersonaDrives.MaxValue, PersonaDrives.NeutralValue, PersonaDrives.NeutralValue, true)
            }
        );
        var unpackedGreedy = NeedsBrain.Score(
            RoutineFamily.Trade,
            new NeedsSnapshot
            {
                PackFillFraction = 0,
                Drives = new PersonaDrives(PersonaDrives.MaxValue, PersonaDrives.NeutralValue, PersonaDrives.NeutralValue, true)
            }
        );

        Assert.Equal(PackedGreedTradeScore, packedGreedy);
        Assert.True(packedGreedy > unpackedGreedy);
    }

    [Fact]
    public void Score_PkValorHigh_AddsAndHurtPenalizes()
    {
        var brave = NeedsBrain.Score(
            RoutineFamily.Pk,
            new NeedsSnapshot
            {
                HitsFraction = NeedsBrain.HealthyHitsFraction,
                Drives = new PersonaDrives(PersonaDrives.NeutralValue, PersonaDrives.NeutralValue, PersonaDrives.MaxValue, true)
            }
        );
        var timid = NeedsBrain.Score(
            RoutineFamily.Pk,
            new NeedsSnapshot
            {
                HitsFraction = NeedsBrain.HealthyHitsFraction,
                Drives = new PersonaDrives(PersonaDrives.NeutralValue, PersonaDrives.NeutralValue, PersonaDrives.MinValue, true)
            }
        );
        var hurtBrave = NeedsBrain.Score(
            RoutineFamily.Pk,
            new NeedsSnapshot
            {
                HitsFraction = HurtHits,
                Drives = new PersonaDrives(PersonaDrives.NeutralValue, PersonaDrives.NeutralValue, PersonaDrives.MaxValue, true)
            }
        );

        Assert.Equal(BravePkScore, brave);
        Assert.True(brave > timid);
        Assert.Equal(HurtBravePkScore, hurtBrave);
        Assert.True(hurtBrave < brave);
    }

    [Fact]
    public void Score_EveningTavern_BeatsLumberWhenDrivesSocial()
    {
        var needs = new NeedsSnapshot
        {
            DayPart = DayPart.Evening.ToString(),
            Drives = new PersonaDrives(SocialGreed, SocialCaution, SocialValor, isCustom: true)
        };

        var tavern = NeedsBrain.Score(RoutineFamily.Leisure, needs);
        var lumber = NeedsBrain.Score(RoutineFamily.Work, needs);

        Assert.True(tavern > lumber);
    }

    [Fact]
    public void Score_AmbitionWantsWork_BoostsWork()
    {
        var drives = new PersonaDrives(PersonaDrives.MaxValue, PersonaDrives.NeutralValue, PersonaDrives.NeutralValue, true);
        var plain = NeedsBrain.Score(RoutineFamily.Work, new NeedsSnapshot { Drives = drives });
        var ambitious = NeedsBrain.Score(
            RoutineFamily.Work,
            new NeedsSnapshot { Drives = drives, AmbitionWantsWork = true }
        );

        Assert.Equal(plain + NeedsBrain.AmbitionWorkBoost, ambitious);
        Assert.True(ambitious > plain);
    }

    [Theory]
    [InlineData(RoutineFamily.Hunt)]
    [InlineData(RoutineFamily.Dungeon)]
    public void Score_NightOuting_IsCut(RoutineFamily outing)
    {
        var drives = new PersonaDrives(SocialGreed, SocialCaution, SocialValor, isCustom: true);
        var anyHour = NeedsBrain.Score(outing, new NeedsSnapshot { HitsFraction = 1, Drives = drives });
        var night = NeedsBrain.Score(outing, new NeedsSnapshot { HitsFraction = 1, Drives = drives, DayPart = DayPart.Night.ToString() });

        Assert.Equal(anyHour * DayShapeRules.NightHuntCut, night, precision: 6);
    }
}
