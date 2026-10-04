using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Social;
using SosariaAI.Spawning;

namespace SosariaAI.Population;

/// <summary>
/// The minute pulse over the population, on the <see cref="ActivityPulse"/> minute. While the
/// server is up, everyone the plan binds stands in the world: the boot pass logs in each one
/// still on the internal map, and every pulse after logs in any it finds there, except one a
/// ghost fallback took out, whose return from death stands it up at home. A fixture outside its
/// own hours stands about where it is. The pulse is also the logged-in clock murder decay runs
/// on (<see cref="MurderClock"/>).
/// </summary>
public static class LifecycleClock
{
    private const double MinutesPerHour = 60.0;

    private static bool _onMinute;

    /// <summary>Runs one pulse after the bind, so everyone enters the world at boot, then pulses every minute.</summary>
    public static void AfterWorldLoad()
    {
        Pulse();

        if (!_onMinute)
        {
            ActivityPulse.MinutePassed += Pulse;
            _onMinute = true;
        }
    }

    /// <summary>The local time of day in hours, with the minutes as a fraction.</summary>
    public static double HourOfDay(DateTime utcNow) =>
        DayShapeRules.LocalHour(utcNow) + utcNow.Minute / MinutesPerHour;

    /// <summary>
    /// A person off the world logs back in on the pulse, unless its return from death is set: a
    /// ghost fallback (<see cref="SosariaCharacter.FallbackFromGhost"/>) took it out, and that
    /// return stands it up at home. Logged back in by the pulse, it went through
    /// <see cref="SosariaCharacter.ResumeSession"/>, which reads the spot of its last login
    /// (taking it off the world does not move that spot) and could put it back on a dungeon floor.
    /// </summary>
    public static bool LogsIn(bool inWorld, bool returnFromDeathSet) => !inWorld && !returnFromDeathSet;

    /// <summary>
    /// A fixture outside its own hours stands about where it is; anyone else lives its day. The
    /// hours are worked out only for a fixture.
    /// </summary>
    public static bool IdlesOffHours(bool isFixture, int hour, uint serial, int? startHour, int? endHour)
    {
        if (!isFixture)
        {
            return false;
        }

        var (start, end) = SessionHours.Resolve(serial, startHour, endHour);
        return !SessionHours.IsActive(hour, start, end);
    }

    private static void Pulse()
    {
        var now = Core.Now;
        var hour = (int)Math.Floor(HourOfDay(now));
        var bound = new List<SosariaCharacter>();

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is SosariaCharacter { Deleted: false, Definition: not null } character)
            {
                bound.Add(character);
            }
        }

        for (var i = 0; i < bound.Count; i++)
        {
            var character = bound[i];

            if (LogsIn(People.InWorld(character), ReturnSchedule.IsScheduled(character.ReturnAt)))
            {
                character.ResumeSession();
            }

            MurderClock.Count(character, now);

            if (People.InWorld(character) &&
                IdlesOffHours(
                    IsFixture(character),
                    hour,
                    character.Serial.Value,
                    character.Persona?.ActiveStartHour,
                    character.Persona?.ActiveEndHour
                ))
            {
                character.IdleAtCurrentSpot();
            }
        }

        MurderClock.Sweep(now);
    }

    /// <summary>A fixture or a player's recruit: it keeps its own hours, and stands about outside them.</summary>
    private static bool IsFixture(SosariaCharacter character) =>
        !WorkSites.IsCopy(character.CharacterId) || GuildRecruits.InPlayerGuild(character);
}
