using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Mobiles;
using SosariaAI.Spawning;

namespace SosariaAI.Behaviour;

/// <summary>
/// Someone flags criminal and a few willing fighters nearby go after the gray: killing one is
/// no crime, which is why the bank steps emptied every time a thief got caught. At most three
/// answer one gray. Nobody starts on a flag already running out, and each watcher breaks off
/// once the flag lapses. A watcher that let go of a gray still flagged (it fled, or lost it)
/// leaves that gray be for a while: one fighter drew on the same gray thirteen times. Under
/// the guards the answer is a shout for them, since the guard line ends every brawl in town
/// anyway. Guildmates and partymates are covered for, not hunted. A gray in a lawful fight
/// (guild war, Order against Chaos, duel) is left to it. In Buccaneer's Den a watcher draws
/// only on a Den raid or on a gray hurting its friend (<see cref="WorldPlay.MayStartFightInDen"/>).
/// Keys are serial values. In memory only.
/// </summary>
public sealed class GrayWatch
{
    public const int MaxWatchers = 3;

    /// <summary>A fighter this close to a gray may answer it.</summary>
    public const int WatchRange = 12;

    /// <summary>Grays gone or blue again, and rests run out, are dropped this often.</summary>
    public static readonly TimeSpan SweepGap = TimeSpan.FromSeconds(30);

    /// <summary>Of every hundred fighters this many go after a gray; the brave and the cautious differ.</summary>
    public const int WillingPercent = 50;

    public const int BraveWillingPercent = 75;
    public const int CautiousWillingPercent = 25;
    public const int PercentScale = 100;

    /// <summary>The first watcher on a gray calls it out this often; the rest draw quietly.</summary>
    public const int CallPercent = 45;

    /// <summary>
    /// The engine clears the flag two minutes after the crime. Nobody starts a fight on a flag
    /// older than this, so the fight cannot outlive its reason and gray the watcher instead.
    /// </summary>
    public static readonly TimeSpan StaleFlag = TimeSpan.FromSeconds(90);

    /// <summary>
    /// A bystander who sees a gray under the guards takes this long at the least to shout for
    /// them, <see cref="BystanderCallMax"/> at most: a person has to notice and react. A call
    /// in the same second as the crime killed every caught thief before it could run.
    /// </summary>
    public static readonly TimeSpan BystanderCallMin = TimeSpan.FromSeconds(2);

    public static readonly TimeSpan BystanderCallMax = TimeSpan.FromSeconds(5);

    private static readonly HashSet<Serial> PendingCalls = [];

    /// <summary>The call still goes out when the gray is flagged, alive, under the guards, and still in sight range.</summary>
    public static bool CallStands(bool grayCriminal, bool grayAlive, bool grayUnderGuards, int distance) =>
        grayCriminal && grayAlive && grayUnderGuards && distance >= 0 && distance <= WatchRange;

    /// <summary>A watcher that let go of a gray still flagged leaves it be this long.</summary>
    public static readonly TimeSpan ReengageRest = TimeSpan.FromSeconds(90);

    public static GrayWatch Shared { get; } = new();

    private readonly Dictionary<uint, HashSet<uint>> _watchers = new();
    private readonly Dictionary<uint, DateTime> _flaggedSince = new();
    private readonly Dictionary<(uint Watcher, uint Gray), DateTime> _brokeOff = new();
    private DateTime _lastSweep;

    /// <summary>
    /// A gray in a lawful fight, a guild war, Order against Chaos or an agreed duel, is left to it.
    /// A guild-war blow once read as a crime drew three watchers on the fighter.
    /// </summary>
    public static bool IsAnswerable(bool grayInLawfulFight) => !grayInLawfulFight;

    /// <summary>
    /// Whether this fighter is the kind that goes after a gray. Read off its serial, so the same
    /// person answers the same way every time instead of rerolling its nerve each scan.
    /// </summary>
    public static bool IsWilling(uint serial, bool brave, bool cautious) =>
        serial % PercentScale < (brave ? BraveWillingPercent : cautious ? CautiousWillingPercent : WillingPercent);

    /// <summary>A flag seen first at <paramref name="flaggedSince"/> still has time for a fight.</summary>
    public static bool FlagFresh(DateTime flaggedSince, DateTime now) =>
        !TimeRules.Passed(flaggedSince, now, StaleFlag);

    /// <summary>When the watch first saw this gray's flag. The first sighting holds until the gray is dropped.</summary>
    public DateTime NoteFlag(uint gray, DateTime now)
    {
        _flaggedSince.TryAdd(gray, now);
        return _flaggedSince[gray];
    }

    /// <summary>True while this watcher rests from a gray it let go.</summary>
    public bool Resting(uint watcher, uint gray, DateTime now) =>
        _brokeOff.TryGetValue((watcher, gray), out var at) && now - at < ReengageRest;

    /// <summary>Signs a watcher on to a gray. False when three already answer it.</summary>
    public bool TryJoin(uint gray, uint watcher)
    {
        if (!_watchers.TryGetValue(gray, out var set))
        {
            set = [];
            _watchers[gray] = set;
        }

        if (set.Contains(watcher))
        {
            return true;
        }

        if (set.Count >= MaxWatchers)
        {
            return false;
        }

        set.Add(watcher);
        return true;
    }

    public bool IsWatcher(uint gray, uint watcher) =>
        _watchers.TryGetValue(gray, out var set) && set.Contains(watcher);

    public void Leave(uint gray, uint watcher)
    {
        if (!_watchers.TryGetValue(gray, out var set))
        {
            return;
        }

        set.Remove(watcher);

        if (set.Count == 0)
        {
            _watchers.Remove(gray);
        }
    }

    /// <summary>The flag lapsed or the gray is gone: nobody answers it any more.</summary>
    public void Forget(uint gray)
    {
        _watchers.Remove(gray);
        _flaggedSince.Remove(gray);
    }

    /// <summary>
    /// Drops every gray the check calls finished (dead, gone, or its flag lapsed) and every rest
    /// run out, once per <see cref="SweepGap"/>. A gray that flags again later is a fresh flag.
    /// </summary>
    public void Sweep(Func<uint, bool> finished, DateTime now)
    {
        if (!TimeRules.Rested(_lastSweep, now, SweepGap))
        {
            return;
        }

        _lastSweep = now;
        List<uint> done = null;

        foreach (var gray in _flaggedSince.Keys)
        {
            if (finished(gray))
            {
                (done ??= []).Add(gray);
            }
        }

        foreach (var gray in _watchers.Keys)
        {
            if (!_flaggedSince.ContainsKey(gray) && finished(gray))
            {
                (done ??= []).Add(gray);
            }
        }

        for (var i = 0; done != null && i < done.Count; i++)
        {
            Forget(done[i]);
        }

        List<(uint, uint)> rested = null;

        foreach (var (pair, at) in _brokeOff)
        {
            if (now - at >= ReengageRest)
            {
                (rested ??= []).Add(pair);
            }
        }

        for (var i = 0; rested != null && i < rested.Count; i++)
        {
            _brokeOff.Remove(rested[i]);
        }
    }

    /// <summary>
    /// Drops the watchers that no longer fight this gray, so their places open up. Each one
    /// let go of a gray still flagged, so it rests from that gray.
    /// </summary>
    public void Prune(uint gray, Func<uint, bool> stillOn, DateTime now)
    {
        if (!_watchers.TryGetValue(gray, out var set))
        {
            return;
        }

        List<uint> gone = null;

        foreach (var watcher in set)
        {
            if (!stillOn(watcher))
            {
                (gone ??= []).Add(watcher);
            }
        }

        for (var i = 0; gone != null && i < gone.Count; i++)
        {
            set.Remove(gone[i]);
            _brokeOff[(gone[i], gray)] = now;
        }

        if (set.Count == 0)
        {
            _watchers.Remove(gray);
        }
    }

    public static string CallLine(string gray) =>
        string.IsNullOrWhiteSpace(gray) ? "Criminal! Stop them!" : $"Criminal! Stop {gray}!";

    /// <summary>
    /// A bystander calls the guards on a gray after a short random wait, if the call still
    /// stands then (<see cref="CallStands"/>).
    /// </summary>
    private static void CallGuardsSoon(SosariaCharacter watcher, Mobile gray)
    {
        if (!PendingCalls.Add(watcher.Serial))
        {
            return;
        }

        var delay = RedGangRules.Between(BystanderCallMin, BystanderCallMax, Utility.RandomDouble());

        Timer.StartTimer(delay, () =>
        {
            PendingCalls.Remove(watcher.Serial);

            if (!watcher.Deleted && watcher.Alive && watcher.Map == gray.Map &&
                CallStands(gray.Criminal, gray.Alive && !gray.Deleted, SosariaCharacter.UnderGuards(gray), (int)watcher.GetDistanceToSqrt(gray)))
            {
                GuardCall.TryCall(watcher, gray);
            }
        });
    }

    /// <summary>
    /// A fighter first breaks off a gray whose flag lapsed, then answers a fresh one when fewer
    /// than three already do: under the guards with a shout for them, out of their reach with a blade.
    /// </summary>
    public static void Consider(SosariaCharacter character)
    {
        BreakOff(character);
        var now = Core.Now;

        Shared.Sweep(gray => World.FindMobile((Serial)gray) is not { Deleted: false, Alive: true, Criminal: true }, now);

        if (character.IsPk || character.Build?.Role != CharacterRole.Fighter || character.Combatant != null ||
            !People.InWorld(character) ||
            !IsWilling(
                character.Serial.Value,
                character.PersonProfile.Has(PersonTrait.Brave),
                character.PersonProfile.Has(PersonTrait.Cautious)
            ))
        {
            return;
        }

        foreach (var mobile in character.Map.GetMobilesInRange(character.Location, WatchRange))
        {
            if (mobile is not PlayerMobile { Alive: true, Hidden: false, Criminal: true } gray || gray == character ||
                PkRules.IsRed(gray.Kills) || !character.CanSee(gray) || OwnSide(character, gray) ||
                !IsAnswerable(FactionWar.LawfulFight(gray, gray.Combatant)) ||
                !WorldPlay.MayStartFightInDen(character, gray))
            {
                continue;
            }

            var graySerial = gray.Serial.Value;

            if (!FlagFresh(Shared.NoteFlag(graySerial, now), now))
            {
                continue;
            }

            // A watcher off its gray let go of it; the prune starts its rest before it may join again.
            Shared.Prune(graySerial, watcher => World.FindMobile((Serial)watcher)?.Combatant == gray, now);

            if (Shared.Resting(character.Serial.Value, graySerial, now))
            {
                continue;
            }

            if (SosariaCharacter.UnderGuards(character) || SosariaCharacter.UnderGuards(gray))
            {
                CallGuardsSoon(character, gray);
                return;
            }

            var first = !Shared._watchers.ContainsKey(graySerial);

            if (!Shared.TryJoin(graySerial, character.Serial.Value))
            {
                continue;
            }

            if (first && Utility.Random(PercentScale) < CallPercent)
            {
                character.SpeakScripted(CallLine(gray.Name));
            }

            character.JoinAgainst(gray);
            WorldPlay.Log($"{character.Name} drew on the gray {gray.Name}");
            return;
        }
    }

    // A guildmate or a partymate who flags gray is someone to cover for, not someone to kill.
    private static bool OwnSide(SosariaCharacter character, Mobile gray) =>
        character.Guild != null && gray.Guild == character.Guild ||
        GameParty.Of(character)?.Contains(gray) == true;

    // Once the flag lapses the gray is blue again and the watcher lets it go.
    private static void BreakOff(SosariaCharacter character)
    {
        if (character.Combatant is not PlayerMobile gray ||
            !Shared.IsWatcher(gray.Serial.Value, character.Serial.Value) ||
            gray.Alive && !gray.Deleted && gray.Criminal)
        {
            return;
        }

        if (!gray.Alive || gray.Deleted)
        {
            Shared.Forget(gray.Serial.Value);
            return;
        }

        Shared.Leave(gray.Serial.Value, character.Serial.Value);

        if (!PkRules.IsRed(gray.Kills))
        {
            character.StandDown();
            WorldPlay.Log($"{character.Name} let {gray.Name} go: the flag lapsed");
        }
    }
}
