using System.Collections.Generic;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class PkSlotRulesTests
{
    private const int NoSlots = 0;
    private const int OneSlot = 1;
    private const int TwoSlots = 2;
    private const int ExtraSlots = 8;

    private const string IdOutOfThePlan = "gone#9";

    private static readonly string[] NoSavedReds = [];

    private static readonly string[] DefaultRosterIds =
    [
        PersonasFile.ConnorId,
        PersonasFile.MiraId,
        PersonasFile.TobinId,
        PersonasFile.HalId,
        PersonasFile.WrenId,
        PersonasFile.BranId,
        PersonasFile.SelaId,
        PersonasFile.TamId,
        PersonasFile.DunnId,
        PersonasFile.OrlaId
    ];

    private static readonly bool[] DefaultVeterans =
    [
        false,
        false,
        false,
        false,
        false,
        true,
        true,
        false,
        false,
        true
    ];

    private static readonly bool[] DefaultFighters =
    [
        false,
        false,
        false,
        false,
        false,
        true,
        true,
        true,
        true,
        true
    ];

    private static readonly bool[] DefaultLeaders =
    [
        false,
        false,
        false,
        false,
        false,
        true,
        false,
        false,
        false,
        false
    ];

    [Fact]
    public void Select_LastTwoRosterIds_AreNotAutomaticPk()
    {
        var selected = Choose(TwoSlots);

        Assert.DoesNotContain(PersonasFile.DunnId, selected);
        Assert.Contains(PersonasFile.OrlaId, selected);
        Assert.Contains(PersonasFile.SelaId, selected);
        Assert.Equal([PersonasFile.OrlaId, PersonasFile.SelaId], selected);
    }

    [Fact]
    public void Select_PartyLeader_IsNeverChosen()
    {
        var selected = Choose(TwoSlots);

        Assert.DoesNotContain(PersonasFile.BranId, selected);
    }

    [Fact]
    public void Select_Workers_AreNeverChosen()
    {
        var selected = PkSlotRules.Select(
            DefaultRosterIds,
            DefaultVeterans,
            DefaultFighters,
            DefaultLeaders,
            NoSavedReds,
            pkCount: ExtraSlots
        );

        Assert.DoesNotContain(PersonasFile.ConnorId, selected);
        Assert.DoesNotContain(PersonasFile.MiraId, selected);
        Assert.DoesNotContain(PersonasFile.TobinId, selected);
        Assert.DoesNotContain(PersonasFile.HalId, selected);
        Assert.DoesNotContain(PersonasFile.WrenId, selected);
        Assert.DoesNotContain(PersonasFile.BranId, selected);
        Assert.Equal(
            [PersonasFile.OrlaId, PersonasFile.SelaId, PersonasFile.DunnId, PersonasFile.TamId],
            selected
        );
    }

    [Fact]
    public void Select_PkCountZero_SelectsNone() =>
        Assert.Empty(Choose(NoSlots));

    [Fact]
    public void Select_OneSlot_PicksLastVeteranFollower()
    {
        var selected = Choose(OneSlot);

        Assert.Equal([PersonasFile.OrlaId], selected);
        Assert.DoesNotContain(PersonasFile.DunnId, selected);
        Assert.DoesNotContain(PersonasFile.BranId, selected);
    }

    [Fact]
    public void Select_NoShardCap_TakesEveryFighterAsked()
    {
        var selected = Choose(ExtraSlots);

        Assert.Equal(
            [PersonasFile.OrlaId, PersonasFile.SelaId, PersonasFile.DunnId, PersonasFile.TamId],
            selected
        );
    }

    [Fact]
    public void Select_FillsNoviceFighters_WhenVeteransRunOut()
    {
        string[] ids = [PersonasFile.ConnorId, PersonasFile.BranId, PersonasFile.DunnId];
        bool[] veterans = [false, true, false];
        bool[] fighters = [false, true, true];
        bool[] leaders = [false, true, false];

        var selected = PkSlotRules.Select(
            ids,
            veterans,
            fighters,
            leaders,
            NoSavedReds,
            pkCount: TwoSlots
        );

        Assert.Equal([PersonasFile.DunnId], selected);
        Assert.DoesNotContain(PersonasFile.ConnorId, selected);
        Assert.DoesNotContain(PersonasFile.BranId, selected);
    }

    [Fact]
    public void Select_VeteranWorker_IsNeverChosen()
    {
        string[] ids = [PersonasFile.ConnorId, PersonasFile.OrlaId];
        bool[] veterans = [true, true];
        bool[] fighters = [false, true];
        bool[] leaders = [false, false];

        var selected = PkSlotRules.Select(
            ids,
            veterans,
            fighters,
            leaders,
            NoSavedReds,
            pkCount: TwoSlots
        );

        Assert.Equal([PersonasFile.OrlaId], selected);
        Assert.DoesNotContain(PersonasFile.ConnorId, selected);
    }

    [Fact]
    public void Select_SavedRed_StaysRedAheadOfFreshPicks()
    {
        var selected = PkSlotRules.Select(
            DefaultRosterIds,
            DefaultVeterans,
            DefaultFighters,
            DefaultLeaders,
            [PersonasFile.TamId],
            pkCount: TwoSlots
        );

        Assert.Equal([PersonasFile.TamId, PersonasFile.OrlaId], selected);
    }

    [Fact]
    public void Select_SavedReds_AllStayPastTheCount()
    {
        var selected = PkSlotRules.Select(
            DefaultRosterIds,
            DefaultVeterans,
            DefaultFighters,
            DefaultLeaders,
            [PersonasFile.ConnorId, PersonasFile.BranId],
            pkCount: OneSlot
        );

        Assert.Equal([PersonasFile.ConnorId, PersonasFile.BranId], selected);
    }

    [Fact]
    public void Select_GrownPlan_KeepsTheSavedReds()
    {
        string[] before = [PersonasFile.DunnId, PersonasFile.OrlaId];
        string[] grown = [PersonasFile.DunnId, PersonasFile.OrlaId, PersonasFile.SelaId, PersonasFile.TamId];
        bool[] veterans = [true, true, true, true];
        bool[] fighters = [true, true, true, true];
        bool[] leaders = [false, false, false, false];

        var first = PkSlotRules.Select(before, veterans, fighters, leaders, NoSavedReds, pkCount: OneSlot);
        var next = PkSlotRules.Select(grown, veterans, fighters, leaders, first, pkCount: OneSlot);

        Assert.Equal(first, next);
    }

    [Fact]
    public void Select_SavedRedOutOfThePlan_IsNotPicked()
    {
        var selected = PkSlotRules.Select(
            DefaultRosterIds,
            DefaultVeterans,
            DefaultFighters,
            DefaultLeaders,
            [IdOutOfThePlan],
            pkCount: OneSlot
        );

        Assert.Equal([PersonasFile.OrlaId], selected);
    }

    private static IReadOnlyList<string> Choose(int pkCount) =>
        PkSlotRules.Select(
            DefaultRosterIds,
            DefaultVeterans,
            DefaultFighters,
            DefaultLeaders,
            NoSavedReds,
            pkCount: pkCount
        );
}
