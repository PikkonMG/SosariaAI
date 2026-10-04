using System;
using System.Collections.Generic;
using System.Globalization;
using Server.Logging;
using SosariaAI.Configuration;
using SosariaAI.Logging;

namespace SosariaAI.Combat;

/// <summary>
/// Counts, shard wide, the spells characters began and the ones a blow broke, by circle, and
/// how the footwork went: casts bought by stepping clear and casts made where the caster
/// stood. Every <see cref="CensusText.CensusMinutes"/> minutes one activity line gives the interrupt
/// rate per cast, so a count of "was interrupted" lines can be read against the casts. World
/// thread only.
/// </summary>
public static class CastTally
{
    public const int Percent = 100;

    private static readonly ILogger logger = SosariaLog.For(typeof(CastTally));
    private static readonly int[] Begun = new int[CastTiming.TopCircle + 1];
    private static readonly int[] Broken = new int[CastTiming.TopCircle + 1];
    private static int _steppedClear;
    private static int _stood;
    private static int _minutes;

    public static void Initialize() => ActivityPulse.MinutePassed += OnMinute;

    public static void NoteBegun(int circle) => Begun[circle]++;

    public static void NoteBroken(int circle) => Broken[circle]++;

    public static void NoteSteppedClear() => _steppedClear++;

    public static void NoteStood() => _stood++;

    /// <summary>
    /// The summary line: casts, the share a blow broke, the circles that were cast, and the
    /// footwork. Null when nobody cast.
    /// </summary>
    public static string Line(IReadOnlyList<int> begun, IReadOnlyList<int> broken, int steppedClear, int stood, int minutes)
    {
        var casts = 0;
        var breaks = 0;
        var circles = new List<string>();

        for (var circle = CastTiming.FirstCircle; circle < begun.Count; circle++)
        {
            casts += begun[circle];
            breaks += broken[circle];

            if (begun[circle] > 0)
            {
                circles.Add($"{circle}: {broken[circle]}/{begun[circle]}");
            }
        }

        if (casts == 0)
        {
            return null;
        }

        var rate = (breaks * Percent / casts).ToString(CultureInfo.InvariantCulture);
        return $"Casting in the last {minutes} minutes: {casts} casts, {breaks} broken by blows ({rate}%); " +
               $"broken/cast by circle {string.Join(", ", circles)}; {steppedClear} stepped clear first, {stood} cast where they stood";
    }

    private static void OnMinute()
    {
        if (!CensusText.Due(++_minutes))
        {
            return;
        }

        if (SosariaSettings.LogActivity && Line(Begun, Broken, _steppedClear, _stood, CensusText.CensusMinutes) is { } line)
        {
            logger.Information(line);
        }

        Array.Clear(Begun);
        Array.Clear(Broken);
        _steppedClear = 0;
        _stood = 0;
    }
}
