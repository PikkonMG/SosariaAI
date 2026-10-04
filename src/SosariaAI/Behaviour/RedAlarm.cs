using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Social;

namespace SosariaAI.Behaviour;

/// <summary>
/// A civilian who sees a red screams "RED AT {PLACE}!!", the shard's danger map warms, and
/// travelers nearby drop their trip and steer round. One place is screamed about once in the
/// rest, whoever the red and whoever the witness: 179 of 208 alarms in one hour were
/// "RED AT BUCCANEER'S DEN". Nobody screams about the reds' own town, and nobody screams
/// about a red it is fighting. Under the guards the civilian shouts for the guards instead.
/// A scream is a warning line too, so it passes the shared <see cref="WarningGate"/>.
/// </summary>
public static class RedAlarm
{
    /// <summary>A civilian sees a red this far off.</summary>
    public const int SightRange = 14;

    /// <summary>Travelers this close to the scream scatter.</summary>
    public const int ScatterRange = 12;

    /// <summary>One scream per place in this time, shard-wide.</summary>
    public static readonly TimeSpan ScreamRest = TimeSpan.FromMinutes(10);

    private static readonly Dictionary<(string Facet, string Place), DateTime> LastScream = new();

    public static bool ScreamDue(DateTime last, DateTime now) => TimeRules.Rested(last, now, ScreamRest);

    /// <summary>
    /// A red is worth a scream when it stands outside the reds' own town and the witness is not
    /// already in a fight with it: "red in Despise" is never shouted at the red one is fighting.
    /// </summary>
    public static bool WorthScream(bool redInBucsDen, bool fightingWitness) => !redInBucsDen && !fightingWitness;

    public static void Consider(SosariaCharacter character)
    {
        if (character.IsPk || character.Build?.Role == CharacterRole.Fighter || character.Combatant != null ||
            character.HomeFacet != FacetNames.Felucca || !People.InWorld(character))
        {
            return;
        }

        var red = SeenRed(character);

        if (red == null)
        {
            return;
        }

        if (SosariaCharacter.UnderGuards(character))
        {
            GuardCall.TryCall(character, red);
            return;
        }

        if (!WorthScream(PkRules.InBuccaneersDen(red.X, red.Y), Fighting(character, red)))
        {
            return;
        }

        var now = Core.Now;
        var place = PlaceNames.Of(red);
        var key = (character.HomeFacet, place);

        if (!ScreamDue(LastScream.GetValueOrDefault(key), now) ||
            !WarningGate.Shared.TryWarn(character.Serial, Serial.Zero, red.Serial, now))
        {
            return;
        }

        ForgetOld(now);
        LastScream[key] = now;
        character.SpeakScripted(DangerMap.ScreamLine(place));
        ShardNews.RedSeen(red, character, now);
        Scatter(character, red, now);
        Scenes.RedAlert(character, red, GossipLines.PlaceWord(place));
        WorldPlay.Log($"{character.Name} screamed about {red.Name}");
    }

    // Either side's aim, or either side's hits on the other in the aggression lists.
    private static bool Fighting(Mobile witness, Mobile red) =>
        witness.Combatant == red || red.Combatant == witness || CombatBrain.IsAggressor(witness, red);

    private static Mobile SeenRed(SosariaCharacter character)
    {
        foreach (var mobile in character.Map.GetMobilesInRange(character.Location, SightRange))
        {
            if (mobile is PlayerMobile { Alive: true, Hidden: false } person && person != character &&
                PkRules.IsRed(person.Kills) && character.CanSee(person))
            {
                return person;
            }
        }

        return null;
    }

    // Travelers who are not fighting drop the trip, remember the spot and take a step away.
    // A step changes the sector list the range query walks, so the travelers are gathered
    // first and moved after.
    private static void Scatter(SosariaCharacter screamer, Mobile red, DateTime now)
    {
        var travelers = new List<SosariaCharacter>();

        foreach (var mobile in screamer.Map.GetMobilesInRange(screamer.Location, ScatterRange))
        {
            if (mobile is SosariaCharacter { IsPk: false, Alive: true } traveler &&
                traveler.Combatant == null && traveler.Build?.Role != CharacterRole.Fighter &&
                Duels.Find(traveler) == null)
            {
                travelers.Add(traveler);
            }
        }

        for (var i = 0; i < travelers.Count; i++)
        {
            var traveler = travelers[i];
            traveler.Memory.Danger.Note(red.Location, now);
            TownTrip.GiveUpForThreat(traveler, now);
            traveler.Motor.StepAwayFrom(red);
        }
    }

    private static void ForgetOld(DateTime now)
    {
        List<(string, string)> old = null;

        foreach (var (key, at) in LastScream)
        {
            if (ScreamDue(at, now))
            {
                (old ??= []).Add(key);
            }
        }

        for (var i = 0; old != null && i < old.Count; i++)
        {
            LastScream.Remove(old[i]);
        }
    }
}
