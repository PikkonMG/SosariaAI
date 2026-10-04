using System;
using System.Collections.Generic;
using Server;
using Server.Engines.PlayerMurderSystem;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using SosariaAI.Social;

namespace SosariaAI.Population;

/// <summary>
/// Murder decay for characters (see <see cref="MurderDecayRules"/>). The minute pulse counts
/// each character once a minute: time in the world is logged-in time, time on
/// <see cref="Map.Internal"/> before the boot logs it in is not. The marks live in the engine's murder record, which the
/// world save keeps; the last-count times live only here, so a restart starts counting afresh
/// and neither loses the saved decay nor counts the downtime.
/// </summary>
public static class MurderClock
{
    private static readonly Dictionary<Serial, DateTime> LastCounted = new();

    /// <summary>World thread. Counts one character's logged-in time since its last count and lets its murders decay.</summary>
    public static void Count(SosariaCharacter character, DateTime now)
    {
        if (!People.InWorld(character))
        {
            LastCounted.Remove(character.Serial);
            return;
        }

        var loggedIn = MurderDecayRules.LoggedInSince(LastCounted.GetValueOrDefault(character.Serial), now);
        LastCounted[character.Serial] = now;

        if (loggedIn <= TimeSpan.Zero || !PlayerMurderSystem.GetMurderContext(character, out var context))
        {
            return;
        }

        var killsBefore = character.Kills;
        var marks = MurderDecayRules.Advance(
            new MurderMarks(context.ShortTermElapse, context.LongTermElapse),
            loggedIn,
            context.ShortTermMurders,
            killsBefore,
            character.IsPk
        );

        context.ShortTermElapse = marks.ShortTermElapse;
        context.LongTermElapse = marks.LongTermElapse;
        context.DecayKills();

        if (MurderDecayRules.WentBlue(killsBefore, character.Kills))
        {
            WentBlue(character);
        }
    }

    /// <summary>World thread, after a pulse counted everyone: forgets the characters it did not count, gone or deleted.</summary>
    public static void Sweep(DateTime now)
    {
        var stale = new List<Serial>();

        foreach (var (serial, counted) in LastCounted)
        {
            if (counted != now)
            {
                stale.Add(serial);
            }
        }

        for (var i = 0; i < stale.Count; i++)
        {
            LastCounted.Remove(stale[i]);
        }
    }

    // The red name is gone: the guild the next bind would give it is worn now, as a new red leaves Order at once.
    private static void WentBlue(SosariaCharacter character)
    {
        EngineGuilds.Resettle(character, murderer: false);
        WorldPlay.Log($"{character.Name} is blue again ({character.Kills} murders)");
    }
}
