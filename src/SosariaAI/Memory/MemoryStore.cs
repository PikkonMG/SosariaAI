using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Timer = Server.Timer;
using Server;
using Server.Logging;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Social;

namespace SosariaAI.Memory;

/// <summary>
/// Long-term memory for every SosariaAI character, in <c>sosariaai/memory.db</c>: the people
/// met, the bonds between them, the adventures they shared, the places they saw, and the shard
/// news. Game code reads and changes RAM only and never touches the disk. Changes go into a
/// queue that one background writer thread drains in one transaction every
/// <see cref="DrainIntervalMs"/>, and on <see cref="Flush"/> and <see cref="Close"/>.
/// When the file cannot open, the store runs empty in RAM and writes nothing.
/// </summary>
public sealed class MemoryStore
{
    public const string FileName = "memory.db";

    /// <summary>Per person, RAM keeps this many newest adventures.</summary>
    public const int RecentAdventuresKept = 20;

    /// <summary>Per person, RAM also keeps this many of the heaviest adventures kept forever.</summary>
    public const int HeaviestAdventuresKept = 50;

    /// <summary>The writer drains the queue this often.</summary>
    public const int DrainIntervalMs = 5_000;

    /// <summary>After a refused write, timed drains wait this long before the next try.</summary>
    public const int WriteRetryMs = 30_000;

    /// <summary>Unwritten changes kept while the file refuses writes; older ones are dropped.</summary>
    public const int MaxPending = 100_000;

    /// <summary>The longest <see cref="Flush"/> and <see cref="Close"/> wait for the writer.</summary>
    public const int FlushWaitMs = 30_000;

    /// <summary>The id <see cref="Record"/> returns for an adventure it refused.</summary>
    public const long NoAdventure = 0;

    public const string WriterThreadName = "SosariaAI memory writer";

    /// <summary>The game thread drops faded memories this often.</summary>
    public static readonly TimeSpan FadeInterval = TimeSpan.FromHours(1);

    // Declared before Shared: static fields start in text order, and the shared store's queue takes this logger.
    private static readonly ILogger logger = SosariaLog.For(typeof(MemoryStore));

    public static MemoryStore Shared { get; } = new();

    private readonly Lock _ram = new();
    private readonly PendingWrites _pending = new(MaxPending, WriteRetryMs, logger);
    private readonly Dictionary<string, KnownPerson> _people = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, Bond>> _bonds = new(StringComparer.Ordinal);
    private readonly Dictionary<long, Adventure> _adventures = new();
    private readonly Dictionary<string, List<long>> _adventuresByPerson = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _places = new(StringComparer.Ordinal);
    private List<ShardEvent> _savedNews = [];
    private long _lastAdventureId;
    private DiskWriter _disk;
    private Timer _fadeTimer;

    /// <summary>True while the database is open and changes reach the disk.</summary>
    public bool IsOpen => Volatile.Read(ref _disk) != null;

    /// <summary><c>memory.db</c> in the plugin's configuration folder.</summary>
    public static string DefaultPath() => ConfigFile.PathIn(FileName);

    /// <summary>
    /// Opens the database, upgrades its tables, loads what RAM keeps, starts the writer, and
    /// starts the hourly fade. Never throws: when the file cannot open, one warning names the
    /// path and the reason, and the store runs empty in RAM and writes nothing.
    /// </summary>
    public void Open(string path)
    {
        Close();
        _fadeTimer = Timer.DelayCall(FadeInterval, FadeInterval, FadeNow);
        MemoryDatabase database = null;

        try
        {
            SqliteLoader.Register();
            database = MemoryDatabase.Open(path);
            var snapshot = database.Load(
                RecentAdventuresKept,
                HeaviestAdventuresKept,
                DateTime.UtcNow - GossipRules.MaxAge,
                GossipRules.JournalCapacity
            );

            lock (_ram)
            {
                Fill(snapshot);
                logger.Information(
                    "Long-term memory open at {Path}: {People} people, {Owners} people with bonds, {Adventures} adventures in RAM",
                    path,
                    _people.Count,
                    _bonds.Count,
                    _adventures.Count
                );
            }
        }
        catch (Exception e)
        {
            database?.Dispose();

            lock (_ram)
            {
                ClearRam();
            }

            logger.Warning("Could not open {Path} ({Reason}); characters run without long-term memory", path, e.Message);
            return;
        }

        Volatile.Write(ref _disk, new DiskWriter(database, path, _pending));
    }

    /// <summary>Writes every queued change now and waits for the writer.</summary>
    public void Flush() => Volatile.Read(ref _disk)?.Flush();

    /// <summary>Stops the fade, writes every queued change, closes the file, and empties RAM.</summary>
    public void Close()
    {
        _fadeTimer?.Stop();
        _fadeTimer = null;
        Interlocked.Exchange(ref _disk, null)?.Stop();
        _pending.Clear();

        lock (_ram)
        {
            ClearRam();
        }
    }

    /// <summary>The person with this id as last seen, or null.</summary>
    public PersonRef? PersonOf(string personId)
    {
        lock (_ram)
        {
            return personId != null && _people.TryGetValue(personId, out var known) ? known.Person : null;
        }
    }

    /// <summary>
    /// The owner met the other person. Sets when and where they first met once, and the last
    /// time they were together. One way: the other's bond to the owner is its own call.
    /// </summary>
    public void NoteMet(PersonRef owner, PersonRef other, string place, DateTime now)
    {
        if (!IsPair(owner, other))
        {
            return;
        }

        lock (_ram)
        {
            NotePerson(owner, now);
            NotePerson(other, now);
            var bond = BondOrNew(owner.Id, other.Id, now, place);
            Put(
                bond with
                {
                    LastSeenAt = Later(bond.LastSeenAt, now),
                    FirstMetPlace = string.IsNullOrEmpty(bond.FirstMetPlace) ? place ?? string.Empty : bond.FirstMetPlace
                }
            );
        }
    }

    /// <summary>Moves the owner's bond to the other person, clamped to the bond range.</summary>
    public void ShiftBond(PersonRef owner, PersonRef other, int delta, string reason, DateTime now)
    {
        if (!IsPair(owner, other))
        {
            return;
        }

        lock (_ram)
        {
            NotePerson(owner, now);
            NotePerson(other, now);
            var bond = BondOrNew(owner.Id, other.Id, now, string.Empty);
            Put(
                bond with
                {
                    Score = BondRules.Clamp(bond.Score + delta),
                    LastSeenAt = Later(bond.LastSeenAt, now),
                    LastReason = reason ?? string.Empty
                }
            );
        }
    }

    /// <summary>The owner's bond to the other person, or null.</summary>
    public Bond BondOf(string ownerId, string otherId)
    {
        if (ownerId == null || otherId == null)
        {
            return null;
        }

        lock (_ram)
        {
            return _bonds.TryGetValue(ownerId, out var bonds) && bonds.TryGetValue(otherId, out var bond) ? bond : null;
        }
    }

    /// <summary>Every bond the owner holds, warmest first.</summary>
    public IReadOnlyList<Bond> BondsOf(string ownerId)
    {
        if (ownerId == null)
        {
            return [];
        }

        lock (_ram)
        {
            if (!_bonds.TryGetValue(ownerId, out var bonds))
            {
                return [];
            }

            var list = new List<Bond>(bonds.Values);
            list.Sort(WarmestFirst);
            return list;
        }
    }

    /// <summary>
    /// Keeps a closed adventure. Sets its id and weight, indexes it for every member, and for
    /// every pair on the same side raises <see cref="Bond.SharedCount"/> and warms the bond by
    /// <see cref="BondRules.SharedAdventureBonus"/>. Returns the id, or
    /// <see cref="NoAdventure"/> when it has no kind or no members.
    /// </summary>
    public long Record(Adventure adventure)
    {
        if (adventure == null || string.IsNullOrWhiteSpace(adventure.Kind))
        {
            return NoAdventure;
        }

        var members = DistinctMembers(adventure.Members);

        if (members.Count == 0)
        {
            return NoAdventure;
        }

        lock (_ram)
        {
            var stored = adventure with
            {
                Id = ++_lastAdventureId,
                Members = members,
                Weight = MemoryFade.WeightOf(adventure.Kind, LargestSide(members))
            };
            _adventures[stored.Id] = stored;

            for (var i = 0; i < members.Count; i++)
            {
                NotePerson(members[i].Person, stored.EndedAt);
                AdventureIdsOf(members[i].Person.Id).Add(stored.Id);
            }

            Queue(new AdventureWrite(stored));

            for (var i = 0; i < members.Count; i++)
            {
                for (var j = 0; j < members.Count; j++)
                {
                    if (i != j && AdventureRoles.SameSide(members[i].Role, members[j].Role))
                    {
                        Share(members[i].Person.Id, members[j].Person.Id, stored);
                    }
                }
            }

            for (var i = 0; i < members.Count; i++)
            {
                Trim(members[i].Person.Id);
            }

            return stored.Id;
        }
    }

    /// <summary>The adventures RAM keeps for this person, newest first.</summary>
    public IReadOnlyList<Adventure> AdventuresOf(string personId)
    {
        lock (_ram)
        {
            var list = AdventuresIn(personId);
            list.Sort(NewestFirst);
            return list;
        }
    }

    /// <summary>Up to <paramref name="max"/> adventures both people were in, heaviest then newest first.</summary>
    public IReadOnlyList<Adventure> SharedWith(string personId, string otherId, int max)
    {
        if (max <= 0 || otherId == null)
        {
            return [];
        }

        lock (_ram)
        {
            var list = AdventuresIn(personId);
            list.RemoveAll(adventure => adventure.MemberOf(otherId) == null);
            list.Sort(HeaviestFirst);

            if (list.Count > max)
            {
                list.RemoveRange(max, list.Count - max);
            }

            return list;
        }
    }

    /// <summary>Someone told this adventure: it counts one more telling and lives longer.</summary>
    public void NoteTold(long adventureId)
    {
        lock (_ram)
        {
            if (_adventures.TryGetValue(adventureId, out var adventure))
            {
                _adventures[adventureId] = adventure with { ToldCount = adventure.ToldCount + 1 };
            }

            Queue(new TellWrite(adventureId));
        }
    }

    /// <summary>The owner is at a place. True when the owner never saw it before.</summary>
    public bool SawPlace(string ownerId, string place, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || string.IsNullOrWhiteSpace(place))
        {
            return false;
        }

        lock (_ram)
        {
            if (!_places.TryGetValue(ownerId, out var places))
            {
                places = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _places[ownerId] = places;
            }

            if (!places.Add(place))
            {
                return false;
            }

            Queue(new PlaceWrite(ownerId, place, now));
            return true;
        }
    }

    /// <summary>Every place the owner saw, in no set order.</summary>
    public IReadOnlyList<string> PlacesSeenBy(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return [];
        }

        lock (_ram)
        {
            return _places.TryGetValue(ownerId, out var places) ? [.. places] : [];
        }
    }

    /// <summary>
    /// Drops faded bonds and adventures from RAM (see <see cref="MemoryFade"/>) and queues the
    /// same sweep for the file, which also drops news too old to tell.
    /// </summary>
    public void Fade(DateTime now)
    {
        lock (_ram)
        {
            var emptyOwners = new List<string>();

            foreach (var (ownerId, bonds) in _bonds)
            {
                var faded = new List<string>();

                foreach (var bond in bonds.Values)
                {
                    if (MemoryFade.BondFades(bond.Score, bond.SharedCount, bond.LastSeenAt, now))
                    {
                        faded.Add(bond.OtherId);
                    }
                }

                for (var i = 0; i < faded.Count; i++)
                {
                    bonds.Remove(faded[i]);
                }

                if (bonds.Count == 0)
                {
                    emptyOwners.Add(ownerId);
                }
            }

            for (var i = 0; i < emptyOwners.Count; i++)
            {
                _bonds.Remove(emptyOwners[i]);
            }

            var fadedAdventures = new List<Adventure>();

            foreach (var adventure in _adventures.Values)
            {
                if (MemoryFade.AdventureFades(adventure.Weight, adventure.EndedAt, adventure.ToldCount, now))
                {
                    fadedAdventures.Add(adventure);
                }
            }

            for (var i = 0; i < fadedAdventures.Count; i++)
            {
                Forget(fadedAdventures[i]);
            }

            Queue(new FadeWrite(now));
        }
    }

    /// <summary>Saves this shard event as it is now: its tellings and its tellers included.</summary>
    public void SaveNews(ShardEvent evt)
    {
        if (evt == null)
        {
            return;
        }

        Queue(
            new NewsWrite(
                new NewsRow(
                    evt.Id,
                    evt.At,
                    evt.Type,
                    evt.Actor,
                    evt.Other,
                    evt.Place,
                    evt.Facet,
                    evt.X,
                    evt.Y,
                    evt.Z,
                    evt.TellCount,
                    evt.LastToldAt,
                    [.. evt.Tellers]
                )
            )
        );
    }

    /// <summary>
    /// The saved news loaded at open that is still young enough to tell, oldest first, capped at
    /// <see cref="GossipRules.JournalCapacity"/>. Hands it over once; later calls get nothing.
    /// </summary>
    public IReadOnlyList<ShardEvent> TakeSavedNews(DateTime now)
    {
        lock (_ram)
        {
            var saved = _savedNews;
            _savedNews = [];
            saved.RemoveAll(evt => now - evt.At > GossipRules.MaxAge);
            return saved;
        }
    }

    private void FadeNow() => Fade(Core.Now);

    private void Queue(MemoryWrite write)
    {
        if (IsOpen)
        {
            _pending.Add(write);
        }
    }

    private void Fill(MemorySnapshot snapshot)
    {
        ClearRam();

        for (var i = 0; i < snapshot.People.Count; i++)
        {
            var (person, lastSeenAt) = snapshot.People[i];
            _people[person.Id] = new KnownPerson(person, lastSeenAt);
        }

        for (var i = 0; i < snapshot.Bonds.Count; i++)
        {
            BondsHeldBy(snapshot.Bonds[i].OwnerId)[snapshot.Bonds[i].OtherId] = snapshot.Bonds[i];
        }

        for (var i = 0; i < snapshot.Places.Count; i++)
        {
            var (ownerId, place) = snapshot.Places[i];

            if (!_places.TryGetValue(ownerId, out var places))
            {
                places = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _places[ownerId] = places;
            }

            places.Add(place);
        }

        for (var i = 0; i < snapshot.Adventures.Count; i++)
        {
            var adventure = snapshot.Adventures[i];
            _adventures[adventure.Id] = adventure;

            for (var m = 0; m < adventure.Members.Count; m++)
            {
                AdventureIdsOf(adventure.Members[m].Person.Id).Add(adventure.Id);
            }
        }

        var indexed = new List<string>(_adventuresByPerson.Keys);

        for (var i = 0; i < indexed.Count; i++)
        {
            Trim(indexed[i]);
        }

        _savedNews = snapshot.News;
        _lastAdventureId = snapshot.LastAdventureId;
    }

    private void ClearRam()
    {
        _people.Clear();
        _bonds.Clear();
        _adventures.Clear();
        _adventuresByPerson.Clear();
        _places.Clear();
        _savedNews = [];
        _lastAdventureId = NoAdventure;
    }

    // Keeps the name a person last went by and the latest time anyone saw them.
    private void NotePerson(PersonRef person, DateTime at)
    {
        var name = person.Name;

        if (_people.TryGetValue(person.Id, out var known))
        {
            if (string.IsNullOrEmpty(name))
            {
                name = known.Person.Name;
            }

            at = Later(known.LastSeenAt, at);
        }

        var updated = new KnownPerson(person with { Name = name ?? string.Empty }, at);
        _people[person.Id] = updated;
        Queue(new PersonWrite(updated.Person, updated.LastSeenAt));
    }

    private Bond BondOrNew(string ownerId, string otherId, DateTime now, string place) =>
        _bonds.TryGetValue(ownerId, out var bonds) && bonds.TryGetValue(otherId, out var bond)
            ? bond
            : new Bond(ownerId, otherId, BondRules.NeutralScore, now, place ?? string.Empty, now, 0, string.Empty);

    private void Put(Bond bond)
    {
        BondsHeldBy(bond.OwnerId)[bond.OtherId] = bond;
        Queue(new BondWrite(bond));
    }

    // One shared adventure on the same side: one more shared count and a warmer bond.
    private void Share(string ownerId, string otherId, Adventure adventure)
    {
        var bond = BondOrNew(ownerId, otherId, adventure.EndedAt, adventure.Place);
        Put(
            bond with
            {
                Score = BondRules.Clamp(bond.Score + BondRules.SharedAdventureBonus),
                SharedCount = bond.SharedCount + 1,
                LastSeenAt = Later(bond.LastSeenAt, adventure.EndedAt),
                LastReason = adventure.Headline
            }
        );
    }

    private Dictionary<string, Bond> BondsHeldBy(string ownerId)
    {
        if (!_bonds.TryGetValue(ownerId, out var bonds))
        {
            bonds = new Dictionary<string, Bond>(StringComparer.Ordinal);
            _bonds[ownerId] = bonds;
        }

        return bonds;
    }

    private List<long> AdventureIdsOf(string personId)
    {
        if (!_adventuresByPerson.TryGetValue(personId, out var ids))
        {
            ids = [];
            _adventuresByPerson[personId] = ids;
        }

        return ids;
    }

    private List<Adventure> AdventuresIn(string personId)
    {
        var list = new List<Adventure>();

        if (personId == null || !_adventuresByPerson.TryGetValue(personId, out var ids))
        {
            return list;
        }

        for (var i = 0; i < ids.Count; i++)
        {
            list.Add(_adventures[ids[i]]);
        }

        return list;
    }

    // RAM keeps, per person, the newest adventures and the heaviest ones kept forever, the
    // same window Open loads. An adventure nobody keeps any more leaves RAM; the file keeps it.
    private void Trim(string personId)
    {
        if (!_adventuresByPerson.TryGetValue(personId, out var ids) || ids.Count <= RecentAdventuresKept)
        {
            return;
        }

        var held = AdventuresIn(personId);
        var keep = new HashSet<long>();
        held.Sort(NewestFirst);

        for (var i = 0; i < RecentAdventuresKept && i < held.Count; i++)
        {
            keep.Add(held[i].Id);
        }

        held.Sort(HeaviestFirst);

        for (var i = 0; i < HeaviestAdventuresKept && i < held.Count && MemoryFade.KeptForever(held[i].Weight); i++)
        {
            keep.Add(held[i].Id);
        }

        for (var i = ids.Count - 1; i >= 0; i--)
        {
            if (keep.Contains(ids[i]))
            {
                continue;
            }

            var dropped = ids[i];
            ids.RemoveAt(i);

            if (!HeldByAnyone(_adventures[dropped]))
            {
                _adventures.Remove(dropped);
            }
        }
    }

    private bool HeldByAnyone(Adventure adventure)
    {
        for (var i = 0; i < adventure.Members.Count; i++)
        {
            if (_adventuresByPerson.TryGetValue(adventure.Members[i].Person.Id, out var ids) && ids.Contains(adventure.Id))
            {
                return true;
            }
        }

        return false;
    }

    private void Forget(Adventure adventure)
    {
        _adventures.Remove(adventure.Id);

        for (var i = 0; i < adventure.Members.Count; i++)
        {
            var personId = adventure.Members[i].Person.Id;

            if (_adventuresByPerson.TryGetValue(personId, out var ids) && ids.Remove(adventure.Id) && ids.Count == 0)
            {
                _adventuresByPerson.Remove(personId);
            }
        }
    }

    // One member per person, first one wins; a member with no role stands with the group.
    private static List<AdventureMember> DistinctMembers(IReadOnlyList<AdventureMember> members)
    {
        var distinct = new List<AdventureMember>();

        if (members == null)
        {
            return distinct;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];

            if (string.IsNullOrWhiteSpace(member.Person.Id) || !seen.Add(member.Person.Id))
            {
                continue;
            }

            distinct.Add(string.IsNullOrWhiteSpace(member.Role) ? member with { Role = AdventureRoles.With } : member);
        }

        return distinct;
    }

    private static int LargestSide(List<AdventureMember> members)
    {
        var opponents = 0;

        for (var i = 0; i < members.Count; i++)
        {
            if (AdventureRoles.IsOpponent(members[i].Role))
            {
                opponents++;
            }
        }

        return Math.Max(opponents, members.Count - opponents);
    }

    private static bool IsPair(PersonRef owner, PersonRef other) =>
        !string.IsNullOrWhiteSpace(owner.Id) &&
        !string.IsNullOrWhiteSpace(other.Id) &&
        !string.Equals(owner.Id, other.Id, StringComparison.Ordinal);

    private static DateTime Later(DateTime first, DateTime second) => first >= second ? first : second;

    private static int WarmestFirst(Bond x, Bond y)
    {
        var byScore = y.Score.CompareTo(x.Score);
        return byScore != 0 ? byScore : y.LastSeenAt.CompareTo(x.LastSeenAt);
    }

    private static int NewestFirst(Adventure x, Adventure y)
    {
        var byEnd = y.EndedAt.CompareTo(x.EndedAt);
        return byEnd != 0 ? byEnd : y.Id.CompareTo(x.Id);
    }

    private static int HeaviestFirst(Adventure x, Adventure y)
    {
        var byWeight = y.Weight.CompareTo(x.Weight);
        return byWeight != 0 ? byWeight : NewestFirst(x, y);
    }

    private readonly record struct KnownPerson(PersonRef Person, DateTime LastSeenAt);

    /// <summary>
    /// The one thread that owns the write connection. It drains the queue every
    /// <see cref="DrainIntervalMs"/> once a failed write has waited out its retry time, and at
    /// once when asked to flush or to stop.
    /// </summary>
    private sealed class DiskWriter
    {
        private readonly MemoryDatabase _database;
        private readonly string _path;
        private readonly PendingWrites _pending;
        private readonly AutoResetEvent _wake = new(false);
        private readonly ConcurrentQueue<TaskCompletionSource> _flushes = new();
        private readonly Thread _thread;
        private volatile bool _stopping;

        public DiskWriter(MemoryDatabase database, string path, PendingWrites pending)
        {
            _database = database;
            _path = path;
            _pending = pending;
            _thread = new Thread(Run) { IsBackground = true, Name = WriterThreadName };
            _thread.Start();
        }

        public void Flush()
        {
            if (_stopping)
            {
                return;
            }

            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _flushes.Enqueue(done);
            _wake.Set();
            done.Task.Wait(FlushWaitMs);
        }

        public void Stop()
        {
            _stopping = true;
            _wake.Set();
            _thread.Join(FlushWaitMs);
        }

        private void Run()
        {
            try
            {
                while (true)
                {
                    _wake.WaitOne(DrainIntervalMs);
                    var stopping = _stopping;
                    var asked = TakeFlushes();

                    if (stopping || asked.Count > 0 || _pending.RetryDue)
                    {
                        Drain();
                    }

                    Release(asked);

                    if (stopping)
                    {
                        return;
                    }
                }
            }
            finally
            {
                _database.Dispose();
                Release(TakeFlushes());
            }
        }

        // A failed batch goes back in front of the queue; the writer thread never dies on it.
        private void Drain()
        {
            var batch = _pending.TakeAll();

            if (batch.Count == 0)
            {
                return;
            }

            try
            {
                _database.Write(batch);
                _pending.NoteSuccess();
            }
            catch (Exception e)
            {
                _pending.NoteFailure(batch, _path, e);
            }
        }

        private List<TaskCompletionSource> TakeFlushes()
        {
            var asked = new List<TaskCompletionSource>();

            while (_flushes.TryDequeue(out var done))
            {
                asked.Add(done);
            }

            return asked;
        }

        private static void Release(List<TaskCompletionSource> asked)
        {
            for (var i = 0; i < asked.Count; i++)
            {
                asked[i].TrySetResult();
            }
        }
    }
}
