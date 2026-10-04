using System;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class DayShapeRulesTests
{
    private const int DefaultWindowWorkHour = 10;
    private const int DefaultWindowEveningHour = 19;
    private const int DefaultWindowNightHour = 2;
    private const int DefaultEveningStartHour = 17;
    private const int WrapStartHour = 20;
    private const int WrapEndHour = 6;
    private const int WrapWorkHour = 22;
    private const int WrapEveningHour = 3;
    private const int WrapNightHour = 10;

    [Fact]
    public void Part_DefaultWindow_MapsWorkEveningNight()
    {
        Assert.Equal(DayPart.Work, DayShapeRules.Part(DefaultWindowWorkHour, null, null));
        Assert.Equal(DayPart.Evening, DayShapeRules.Part(DefaultWindowEveningHour, null, null));
        Assert.Equal(DayPart.Night, DayShapeRules.Part(DefaultWindowNightHour, null, null));
        Assert.Equal(DayPart.Evening, DayShapeRules.Part(DefaultEveningStartHour, null, null));
        Assert.Equal(DayPart.Work, DayShapeRules.Part(DefaultEveningStartHour - 1, null, null));
        Assert.Equal(DayPart.Night, DayShapeRules.Part(DayShapeRules.DefaultEndHour, null, null));
    }

    [Fact]
    public void Part_WrapWindow_MapsWorkEveningNight()
    {
        Assert.Equal(DayPart.Work, DayShapeRules.Part(WrapWorkHour, WrapStartHour, WrapEndHour));
        Assert.Equal(DayPart.Evening, DayShapeRules.Part(WrapEveningHour, WrapStartHour, WrapEndHour));
        Assert.Equal(DayPart.Night, DayShapeRules.Part(WrapNightHour, WrapStartHour, WrapEndHour));
        Assert.Equal(DayPart.Work, DayShapeRules.Part(WrapStartHour, WrapStartHour, WrapEndHour));
        Assert.Equal(DayPart.Night, DayShapeRules.Part(WrapEndHour, WrapStartHour, WrapEndHour));
    }

    [Fact]
    public void NormalizeHour_WrapsTwentyFourToZero()
    {
        Assert.Equal(0, DayShapeRules.NormalizeHour(DayShapeRules.HoursPerDay));
        Assert.Equal(23, DayShapeRules.NormalizeHour(-1));
        Assert.Equal(1, DayShapeRules.NormalizeHour(DayShapeRules.HoursPerDay + 1));
    }

    [Fact]
    public void LocalHour_IsAClockHour()
    {
        var hour = DayShapeRules.LocalHour(DateTime.UtcNow);
        Assert.InRange(hour, 0, 23);
    }

    [Theory]
    [InlineData(DayPart.Work, RoutineFamily.Work, DayShapeRules.WorkActiveBoost)]
    [InlineData(DayPart.Work, RoutineFamily.Hunt, DayShapeRules.WorkActiveBoost)]
    [InlineData(DayPart.Work, RoutineFamily.Dungeon, DayShapeRules.WorkActiveBoost)]
    [InlineData(DayPart.Work, RoutineFamily.Trade, DayShapeRules.WorkActiveBoost)]
    [InlineData(DayPart.Work, RoutineFamily.Leisure, DayShapeRules.WorkSocialCut)]
    [InlineData(DayPart.Work, RoutineFamily.Party, DayShapeRules.NeutralWeight)]
    [InlineData(DayPart.Evening, RoutineFamily.Leisure, DayShapeRules.EveningSocialBoost)]
    [InlineData(DayPart.Evening, RoutineFamily.Work, DayShapeRules.EveningWorkCut)]
    [InlineData(DayPart.Evening, RoutineFamily.Hunt, DayShapeRules.EveningHuntCut)]
    [InlineData(DayPart.Evening, RoutineFamily.Dungeon, DayShapeRules.EveningHuntCut)]
    [InlineData(DayPart.Night, RoutineFamily.Rest, DayShapeRules.NightRestBoost)]
    [InlineData(DayPart.Night, RoutineFamily.Hunt, DayShapeRules.NightHuntCut)]
    [InlineData(DayPart.Night, RoutineFamily.Dungeon, DayShapeRules.NightHuntCut)]
    [InlineData(DayPart.Night, RoutineFamily.Work, DayShapeRules.NightWorkCut)]
    [InlineData(DayPart.Night, RoutineFamily.Trade, DayShapeRules.NeutralWeight)]
    public void WeightMultiplier_ShapesEachFamilyByTheHour(DayPart part, RoutineFamily family, double expected) =>
        Assert.Equal(expected, DayShapeRules.WeightMultiplier(part, family));

    [Fact]
    public void CreateDefaultPersonas_HaveStaggeredActiveHours()
    {
        Assert.Equal((6, 20), Hours(PersonasFile.CreateDefaultConnor()));
        Assert.Equal((7, 21), Hours(PersonasFile.CreateDefaultMira()));
        Assert.Equal((8, 22), Hours(PersonasFile.CreateDefaultTobin()));
        Assert.Equal((9, 21), Hours(PersonasFile.CreateDefaultHal()));
        Assert.Equal((5, 18), Hours(PersonasFile.CreateDefaultWren()));
        Assert.Equal((7, 22), Hours(PersonasFile.CreateDefaultBran()));
        Assert.Equal((10, 23), Hours(PersonasFile.CreateDefaultSela()));
        Assert.Equal((8, 20), Hours(PersonasFile.CreateDefaultTam()));
        Assert.Equal((6, 19), Hours(PersonasFile.CreateDefaultDunn()));
        Assert.Equal((11, 23), Hours(PersonasFile.CreateDefaultOrla()));
        Assert.Equal((8, 20), Hours(PersonasFile.CreateDefaultKerr()));
    }

    private static (int Start, int End) Hours(Persona persona) =>
        (persona.ActiveStartHour!.Value, persona.ActiveEndHour!.Value);
}
