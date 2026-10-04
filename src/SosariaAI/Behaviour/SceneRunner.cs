using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Social;

namespace SosariaAI.Behaviour;

/// <summary>
/// Plays a scene: timed lines between several characters, one beat after another. Each actor
/// is found again when its beat comes up; one that died, was deleted, left the facet or walked
/// off the stage ends the scene quietly. A character plays in one scene at a time, and the
/// shard holds a limited number at once. World thread only.
/// </summary>
public static class SceneRunner
{
    /// <summary>Old place marks are swept once the table holds this many.</summary>
    private const int PlaceSweepAt = 256;

    private static readonly HashSet<Serial> Busy = new();
    private static readonly Dictionary<(Map Map, int X, int Y), DateTime> LastAtPlace = new();
    private static DateTime _lastUnheard;
    private static int _active;

    public static bool IsBusy(Mobile mobile) => mobile != null && Busy.Contains(mobile.Serial);

    /// <summary>
    /// True when a scene about <paramref name="lead"/> may start now. An everyday scene also
    /// needs the spot to have rested. <paramref name="heard"/> says a person at a keyboard is near.
    /// </summary>
    public static bool MayStage(SosariaCharacter lead, bool everyday, out bool heard)
    {
        heard = false;

        if (lead?.Map == null || lead.Map == Map.Internal || IsBusy(lead))
        {
            return false;
        }

        heard = PresenceFocus.PlayerNearby(lead);
        var (x, y) = SceneRules.Cell(lead.X, lead.Y);
        return SceneRules.MayStart(
            heard,
            everyday,
            _active,
            LastAtPlace.GetValueOrDefault((lead.Map, x, y)),
            _lastUnheard,
            Core.Now
        );
    }

    /// <summary>Starts the beats. Cast member 0 is the lead; the stage is where the lead stands now.</summary>
    public static void Play(SceneKind kind, IReadOnlyList<SosariaCharacter> cast, List<SceneBeat> beats, bool heard)
    {
        if (cast is not { Count: > 0 } || beats is not { Count: > 0 } || cast[SceneScripts.Lead]?.Map is not { } map)
        {
            return;
        }

        var lead = cast[SceneScripts.Lead];
        var now = Core.Now;
        var serials = new Serial[cast.Count];

        for (var i = 0; i < cast.Count; i++)
        {
            serials[i] = cast[i].Serial;
            Busy.Add(serials[i]);
        }

        MarkPlace(map, lead.Location, now);

        if (!heard)
        {
            _lastUnheard = now;
        }

        _active++;
        WorldPlay.Log($"{lead.Name} is in a {kind} scene with {cast.Count - 1} others");
        Advance(new Staged(serials, map, lead.Location, beats));
    }

    private static void Advance(Staged scene)
    {
        while (scene.Next < scene.Beats.Count)
        {
            var beat = scene.Beats[scene.Next];

            if (beat.Delay > TimeSpan.Zero && !scene.Waited)
            {
                scene.Waited = true;
                Timer.StartTimer(beat.Delay, () => Advance(scene));
                return;
            }

            scene.Waited = false;
            scene.Next++;

            if (!Deliver(scene, beat))
            {
                break;
            }
        }

        End(scene);
    }

    private static bool Deliver(Staged scene, SceneBeat beat)
    {
        if (beat.Actor < 0 || beat.Actor >= scene.Cast.Length ||
            World.FindMobile(scene.Cast[beat.Actor]) is not SosariaCharacter actor ||
            !OnStage(actor, scene, beat.Channel))
        {
            return false;
        }

        if (scene.LastSpeaker != Serial.Zero && scene.LastSpeaker != actor.Serial &&
            World.FindMobile(scene.LastSpeaker) is { Deleted: false } previous && previous.Map == actor.Map)
        {
            actor.Direction = actor.GetDirectionTo(previous);
        }

        if (beat.Channel == SceneChannel.Party)
        {
            GameParty.Chat(actor, beat.Text);
        }
        else
        {
            actor.SpeakScripted(beat.Text);
        }

        Talk.NoteSpoke(actor);
        scene.LastSpeaker = actor.Serial;
        return true;
    }

    /// <summary>Alive and still on the stage: a party line only needs the party to hold.</summary>
    private static bool OnStage(SosariaCharacter actor, Staged scene, SceneChannel channel) =>
        !actor.Deleted && actor.Alive && actor.Map == scene.Map && actor.Map != Map.Internal &&
        (channel == SceneChannel.Party
            ? GameParty.InParty(actor)
            : NavMetric.Chebyshev(actor.Location, scene.Stage) <= SceneRules.StageRange);

    private static void End(Staged scene)
    {
        _active = Math.Max(0, _active - 1);

        for (var i = 0; i < scene.Cast.Length; i++)
        {
            Busy.Remove(scene.Cast[i]);
        }
    }

    private static void MarkPlace(Map map, Point3D at, DateTime now)
    {
        if (LastAtPlace.Count >= PlaceSweepAt)
        {
            var stale = new List<(Map, int, int)>();

            foreach (var (key, when) in LastAtPlace)
            {
                if (now - when >= SceneRules.PlaceRest)
                {
                    stale.Add(key);
                }
            }

            for (var i = 0; i < stale.Count; i++)
            {
                LastAtPlace.Remove(stale[i]);
            }
        }

        var (x, y) = SceneRules.Cell(at.X, at.Y);
        LastAtPlace[(map, x, y)] = now;
    }

    private sealed class Staged(Serial[] cast, Map map, Point3D stage, List<SceneBeat> beats)
    {
        public Serial[] Cast { get; } = cast;

        public Map Map { get; } = map;

        public Point3D Stage { get; } = stage;

        public List<SceneBeat> Beats { get; } = beats;

        public int Next { get; set; }

        public bool Waited { get; set; }

        public Serial LastSpeaker { get; set; } = Serial.Zero;
    }
}
