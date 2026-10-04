using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using SosariaAI.Social;

namespace SosariaAI.Memory;

/// <summary>What <see cref="MemoryDatabase.Load"/> reads at boot.</summary>
internal sealed record MemorySnapshot(
    List<(PersonRef Person, DateTime LastSeenAt)> People,
    List<Bond> Bonds,
    List<Adventure> Adventures,
    List<(string OwnerId, string Place)> Places,
    List<ShardEvent> News,
    long LastAdventureId
);

/// <summary>
/// The SQL side of <c>memory.db</c>: schema and upgrades, the boot load, and batched writes.
/// One thread at a time uses an instance.
/// </summary>
internal sealed class MemoryDatabase : IDisposable
{
    /// <summary>Seconds a statement waits for a lock held by another connection.</summary>
    public const int BusyTimeoutSeconds = 2;

    /// <summary>Separates teller names in <c>shard_news.tellers</c>.</summary>
    public const char TellerSeparator = '\n';

    // One step per schema version: step N upgrades a version N database to N + 1.
    private static readonly string[] SchemaSteps =
    [
        """
        CREATE TABLE people (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            is_bot INTEGER NOT NULL,
            last_seen_at INTEGER NOT NULL
        );
        CREATE TABLE bonds (
            owner_id TEXT NOT NULL,
            other_id TEXT NOT NULL,
            score INTEGER NOT NULL,
            first_met_at INTEGER NOT NULL,
            first_met_place TEXT NOT NULL,
            last_seen_at INTEGER NOT NULL,
            shared_count INTEGER NOT NULL,
            last_reason TEXT NOT NULL,
            PRIMARY KEY (owner_id, other_id)
        );
        CREATE INDEX bonds_shared_count ON bonds (shared_count);
        CREATE TABLE adventures (
            id INTEGER PRIMARY KEY,
            kind TEXT NOT NULL,
            place TEXT NOT NULL,
            map TEXT NOT NULL,
            x INTEGER NOT NULL,
            y INTEGER NOT NULL,
            started_at INTEGER NOT NULL,
            ended_at INTEGER NOT NULL,
            kills INTEGER NOT NULL,
            deaths INTEGER NOT NULL,
            summary TEXT NOT NULL,
            weight INTEGER NOT NULL,
            told_count INTEGER NOT NULL
        );
        CREATE INDEX adventures_weight ON adventures (weight);
        CREATE TABLE adventure_members (
            adventure_id INTEGER NOT NULL,
            person_id TEXT NOT NULL,
            role TEXT NOT NULL,
            PRIMARY KEY (adventure_id, person_id)
        );
        CREATE INDEX adventure_members_person ON adventure_members (person_id, adventure_id);
        CREATE TABLE places_seen (
            owner_id TEXT NOT NULL,
            place TEXT NOT NULL,
            first_at INTEGER NOT NULL,
            PRIMARY KEY (owner_id, place)
        );
        CREATE TABLE shard_news (
            id TEXT PRIMARY KEY,
            at INTEGER NOT NULL,
            type TEXT NOT NULL,
            actor TEXT NOT NULL,
            other TEXT NOT NULL,
            place TEXT NOT NULL,
            facet TEXT NOT NULL,
            x INTEGER NOT NULL,
            y INTEGER NOT NULL,
            z INTEGER NOT NULL,
            tell_count INTEGER NOT NULL,
            last_told_at INTEGER NOT NULL,
            tellers TEXT NOT NULL
        );
        CREATE INDEX shard_news_at ON shard_news (at);
        """
    ];

    /// <summary>The schema version this plugin writes (<c>PRAGMA user_version</c>).</summary>
    public static int SchemaVersion => SchemaSteps.Length;

    private const string PersonSql =
        "INSERT OR REPLACE INTO people (id, name, is_bot, last_seen_at) VALUES ($id, $name, $bot, $seen)";

    private const string BondSql =
        "INSERT OR REPLACE INTO bonds (owner_id, other_id, score, first_met_at, first_met_place, last_seen_at, shared_count, last_reason) " +
        "VALUES ($owner, $other, $score, $metAt, $metPlace, $seen, $shared, $reason)";

    private const string AdventureSql =
        "INSERT OR REPLACE INTO adventures (id, kind, place, map, x, y, started_at, ended_at, kills, deaths, summary, weight, told_count) " +
        "VALUES ($id, $kind, $place, $map, $x, $y, $started, $ended, $kills, $deaths, $summary, $weight, $told)";

    private const string ClearMembersSql = "DELETE FROM adventure_members WHERE adventure_id = $id";

    private const string MemberSql =
        "INSERT OR REPLACE INTO adventure_members (adventure_id, person_id, role) VALUES ($id, $person, $role)";

    private const string TellSql = "UPDATE adventures SET told_count = told_count + 1 WHERE id = $id";

    private const string PlaceSql =
        "INSERT OR IGNORE INTO places_seen (owner_id, place, first_at) VALUES ($owner, $place, $at)";

    private const string NewsSql =
        "INSERT OR REPLACE INTO shard_news (id, at, type, actor, other, place, facet, x, y, z, tell_count, last_told_at, tellers) " +
        "VALUES ($id, $at, $type, $actor, $other, $place, $facet, $x, $y, $z, $tells, $toldAt, $tellers)";

    private const string SmallAdventuresSql =
        "SELECT id, weight, ended_at, told_count FROM adventures WHERE weight < $kept";

    private const string DeleteAdventureSql = "DELETE FROM adventures WHERE id = $id";

    private const string UnsharedBondsSql =
        "SELECT owner_id, other_id, score, shared_count, last_seen_at FROM bonds WHERE shared_count = 0";

    private const string DeleteBondSql = "DELETE FROM bonds WHERE owner_id = $owner AND other_id = $other";

    private const string OldNewsSql = "DELETE FROM shard_news WHERE at < $cutoff";

    // Per person: the newest adventures, and the heaviest of those kept forever.
    private const string ChosenAdventuresSql =
        """
        WITH ranked AS (
            SELECT a.id AS id, a.weight AS weight,
                ROW_NUMBER() OVER (PARTITION BY m.person_id ORDER BY a.ended_at DESC, a.id DESC) AS newest_rank,
                ROW_NUMBER() OVER (PARTITION BY m.person_id ORDER BY a.weight DESC, a.ended_at DESC, a.id DESC) AS heaviest_rank
            FROM adventure_members m JOIN adventures a ON a.id = m.adventure_id
        ),
        chosen AS (
            SELECT DISTINCT id FROM ranked
            WHERE newest_rank <= $recent OR (heaviest_rank <= $heaviest AND weight >= $kept)
        )
        SELECT a.id, a.kind, a.place, a.map, a.x, a.y, a.started_at, a.ended_at, a.kills, a.deaths, a.summary,
            a.weight, a.told_count, m.person_id, m.role, COALESCE(p.name, ''), COALESCE(p.is_bot, 0)
        FROM chosen c
        JOIN adventures a ON a.id = c.id
        JOIN adventure_members m ON m.adventure_id = a.id
        LEFT JOIN people p ON p.id = m.person_id
        ORDER BY a.id, m.rowid
        """;

    private readonly SqliteConnection _connection;

    private MemoryDatabase(SqliteConnection connection) => _connection = connection;

    /// <summary>
    /// Opens or creates the file in WAL mode and upgrades its tables in place. Throws when the
    /// file cannot open or was written by a newer plugin.
    /// </summary>
    public static MemoryDatabase Open(string path)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path));

        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
                DefaultTimeout = BusyTimeoutSeconds
            }.ToString()
        );

        try
        {
            connection.Open();
            Execute(connection, "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;");
            Upgrade(connection);
            return new MemoryDatabase(connection);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static void Upgrade(SqliteConnection connection)
    {
        using var read = connection.CreateCommand();
        read.CommandText = "PRAGMA user_version";
        var version = Convert.ToInt32(read.ExecuteScalar());

        if (version > SchemaVersion)
        {
            throw new InvalidOperationException($"schema version {version} is newer than this plugin's {SchemaVersion}");
        }

        if (version == SchemaVersion)
        {
            return;
        }

        using var transaction = connection.BeginTransaction();

        for (var step = version; step < SchemaVersion; step++)
        {
            Execute(connection, SchemaSteps[step], transaction);
        }

        Execute(connection, $"PRAGMA user_version = {SchemaVersion}", transaction);
        transaction.Commit();
    }

    /// <summary>
    /// Reads every person, bond and place seen; per person the newest
    /// <paramref name="recent"/> adventures and the <paramref name="heaviest"/> heaviest kept
    /// ones; and the newest <paramref name="newsCap"/> news since <paramref name="newsSince"/>,
    /// oldest first.
    /// </summary>
    public MemorySnapshot Load(int recent, int heaviest, DateTime newsSince, int newsCap)
    {
        var people = new List<(PersonRef Person, DateTime LastSeenAt)>();
        ReadRows(
            "SELECT id, name, is_bot, last_seen_at FROM people",
            row => people.Add((new PersonRef(row.GetString(0), row.GetString(1), row.GetInt64(2) != 0), FromTicks(row.GetInt64(3))))
        );

        var bonds = new List<Bond>();
        ReadRows(
            "SELECT owner_id, other_id, score, first_met_at, first_met_place, last_seen_at, shared_count, last_reason FROM bonds",
            row => bonds.Add(
                new Bond(
                    row.GetString(0),
                    row.GetString(1),
                    row.GetInt32(2),
                    FromTicks(row.GetInt64(3)),
                    row.GetString(4),
                    FromTicks(row.GetInt64(5)),
                    row.GetInt32(6),
                    row.GetString(7)
                )
            )
        );

        var places = new List<(string OwnerId, string Place)>();
        ReadRows("SELECT owner_id, place FROM places_seen", row => places.Add((row.GetString(0), row.GetString(1))));

        var adventures = LoadAdventures(recent, heaviest);
        var news = LoadNews(newsSince, newsCap);

        using var last = _connection.CreateCommand();
        last.CommandText = "SELECT COALESCE(MAX(id), 0) FROM adventures";
        var lastId = Convert.ToInt64(last.ExecuteScalar());

        return new MemorySnapshot(people, bonds, adventures, places, news, lastId);
    }

    private List<Adventure> LoadAdventures(int recent, int heaviest)
    {
        var adventures = new List<Adventure>();
        using var command = _connection.CreateCommand();
        command.CommandText = ChosenAdventuresSql;
        command.Parameters.AddWithValue("$recent", recent);
        command.Parameters.AddWithValue("$heaviest", heaviest);
        command.Parameters.AddWithValue("$kept", MemoryFade.KeepForeverWeight);
        using var reader = command.ExecuteReader();
        Adventure current = null;
        List<AdventureMember> members = null;

        while (reader.Read())
        {
            var id = reader.GetInt64(0);

            if (current == null || current.Id != id)
            {
                members = [];
                current = new Adventure
                {
                    Id = id,
                    Kind = reader.GetString(1),
                    Place = reader.GetString(2),
                    Map = reader.GetString(3),
                    X = reader.GetInt32(4),
                    Y = reader.GetInt32(5),
                    StartedAt = FromTicks(reader.GetInt64(6)),
                    EndedAt = FromTicks(reader.GetInt64(7)),
                    Kills = reader.GetInt32(8),
                    Deaths = reader.GetInt32(9),
                    Summary = reader.GetString(10),
                    Weight = reader.GetInt32(11),
                    ToldCount = reader.GetInt32(12),
                    Members = members
                };
                adventures.Add(current);
            }

            members.Add(
                new AdventureMember(
                    new PersonRef(reader.GetString(13), reader.GetString(15), reader.GetInt64(16) != 0),
                    reader.GetString(14)
                )
            );
        }

        return adventures;
    }

    private List<ShardEvent> LoadNews(DateTime since, int cap)
    {
        var news = new List<ShardEvent>();
        using var command = _connection.CreateCommand();
        command.CommandText =
            "SELECT id, at, type, actor, other, place, facet, x, y, z, tell_count, last_told_at, tellers " +
            "FROM shard_news WHERE at >= $since ORDER BY at DESC LIMIT $cap";
        command.Parameters.AddWithValue("$since", ToTicks(since));
        command.Parameters.AddWithValue("$cap", cap);
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var tellers = reader.GetString(12);
            news.Add(
                new ShardEvent
                {
                    Id = reader.GetString(0),
                    At = FromTicks(reader.GetInt64(1)),
                    Type = reader.GetString(2),
                    Actor = reader.GetString(3),
                    Other = reader.GetString(4),
                    Place = reader.GetString(5),
                    Facet = reader.GetString(6),
                    X = reader.GetInt32(7),
                    Y = reader.GetInt32(8),
                    Z = reader.GetInt32(9),
                    TellCount = reader.GetInt32(10),
                    LastToldAt = FromTicks(reader.GetInt64(11)),
                    Tellers = tellers.Length == 0 ? [] : tellers.Split(TellerSeparator)
                }
            );
        }

        news.Reverse();
        return news;
    }

    /// <summary>Runs the whole batch in one transaction. Throws, with nothing written, when it fails.</summary>
    public void Write(List<MemoryWrite> batch)
    {
        using var transaction = _connection.BeginTransaction();
        var commands = new Dictionary<string, SqliteCommand>(StringComparer.Ordinal);

        try
        {
            for (var i = 0; i < batch.Count; i++)
            {
                Apply(batch[i], commands, transaction);
            }

            transaction.Commit();
        }
        finally
        {
            foreach (var command in commands.Values)
            {
                command.Dispose();
            }
        }
    }

    private void Apply(MemoryWrite write, Dictionary<string, SqliteCommand> commands, SqliteTransaction transaction)
    {
        switch (write)
        {
            case PersonWrite person:
            {
                Run(
                    commands,
                    transaction,
                    PersonSql,
                    ("$id", person.Person.Id),
                    ("$name", person.Person.Name ?? string.Empty),
                    ("$bot", person.Person.IsBot ? 1 : 0),
                    ("$seen", ToTicks(person.LastSeenAt))
                );
                break;
            }
            case BondWrite { Bond: var bond }:
            {
                Run(
                    commands,
                    transaction,
                    BondSql,
                    ("$owner", bond.OwnerId),
                    ("$other", bond.OtherId),
                    ("$score", bond.Score),
                    ("$metAt", ToTicks(bond.FirstMetAt)),
                    ("$metPlace", bond.FirstMetPlace ?? string.Empty),
                    ("$seen", ToTicks(bond.LastSeenAt)),
                    ("$shared", bond.SharedCount),
                    ("$reason", bond.LastReason ?? string.Empty)
                );
                break;
            }
            case AdventureWrite { Adventure: var adventure }:
            {
                WriteAdventure(adventure, commands, transaction);
                break;
            }
            case TellWrite tell:
            {
                Run(commands, transaction, TellSql, ("$id", tell.AdventureId));
                break;
            }
            case PlaceWrite place:
            {
                Run(commands, transaction, PlaceSql, ("$owner", place.OwnerId), ("$place", place.Place), ("$at", ToTicks(place.FirstAt)));
                break;
            }
            case NewsWrite { Row: var row }:
            {
                Run(
                    commands,
                    transaction,
                    NewsSql,
                    ("$id", row.Id),
                    ("$at", ToTicks(row.At)),
                    ("$type", row.Type ?? string.Empty),
                    ("$actor", row.Actor ?? string.Empty),
                    ("$other", row.Other ?? string.Empty),
                    ("$place", row.Place ?? string.Empty),
                    ("$facet", row.Facet ?? string.Empty),
                    ("$x", row.X),
                    ("$y", row.Y),
                    ("$z", row.Z),
                    ("$tells", row.TellCount),
                    ("$toldAt", ToTicks(row.LastToldAt)),
                    ("$tellers", string.Join(TellerSeparator, row.Tellers))
                );
                break;
            }
            case FadeWrite fade:
            {
                Sweep(fade.Now, commands, transaction);
                break;
            }
        }
    }

    private void WriteAdventure(Adventure adventure, Dictionary<string, SqliteCommand> commands, SqliteTransaction transaction)
    {
        Run(
            commands,
            transaction,
            AdventureSql,
            ("$id", adventure.Id),
            ("$kind", adventure.Kind ?? string.Empty),
            ("$place", adventure.Place ?? string.Empty),
            ("$map", adventure.Map ?? string.Empty),
            ("$x", adventure.X),
            ("$y", adventure.Y),
            ("$started", ToTicks(adventure.StartedAt)),
            ("$ended", ToTicks(adventure.EndedAt)),
            ("$kills", adventure.Kills),
            ("$deaths", adventure.Deaths),
            ("$summary", adventure.Summary ?? string.Empty),
            ("$weight", adventure.Weight),
            ("$told", adventure.ToldCount)
        );
        Run(commands, transaction, ClearMembersSql, ("$id", adventure.Id));

        for (var i = 0; i < adventure.Members.Count; i++)
        {
            var member = adventure.Members[i];
            Run(commands, transaction, MemberSql, ("$id", adventure.Id), ("$person", member.Person.Id), ("$role", member.Role ?? string.Empty));
        }
    }

    // The fade rules live in MemoryFade; the sweep reads the rows that could fade and asks it.
    private void Sweep(DateTime now, Dictionary<string, SqliteCommand> commands, SqliteTransaction transaction)
    {
        var fadedAdventures = new List<long>();
        var small = Command(commands, transaction, SmallAdventuresSql);
        small.Parameters.AddWithValue("$kept", MemoryFade.KeepForeverWeight);

        using (var reader = small.ExecuteReader())
        {
            while (reader.Read())
            {
                if (MemoryFade.AdventureFades(reader.GetInt32(1), FromTicks(reader.GetInt64(2)), reader.GetInt32(3), now))
                {
                    fadedAdventures.Add(reader.GetInt64(0));
                }
            }
        }

        for (var i = 0; i < fadedAdventures.Count; i++)
        {
            Run(commands, transaction, ClearMembersSql, ("$id", fadedAdventures[i]));
            Run(commands, transaction, DeleteAdventureSql, ("$id", fadedAdventures[i]));
        }

        var fadedBonds = new List<(string Owner, string Other)>();
        var unshared = Command(commands, transaction, UnsharedBondsSql);

        using (var reader = unshared.ExecuteReader())
        {
            while (reader.Read())
            {
                if (MemoryFade.BondFades(reader.GetInt32(2), reader.GetInt32(3), FromTicks(reader.GetInt64(4)), now))
                {
                    fadedBonds.Add((reader.GetString(0), reader.GetString(1)));
                }
            }
        }

        for (var i = 0; i < fadedBonds.Count; i++)
        {
            Run(commands, transaction, DeleteBondSql, ("$owner", fadedBonds[i].Owner), ("$other", fadedBonds[i].Other));
        }

        Run(commands, transaction, OldNewsSql, ("$cutoff", ToTicks(now - GossipRules.MaxAge)));
    }

    private static void Run(
        Dictionary<string, SqliteCommand> commands,
        SqliteTransaction transaction,
        string sql,
        params (string Name, object Value)[] values
    )
    {
        var command = Command(commands, transaction, sql);

        for (var i = 0; i < values.Length; i++)
        {
            command.Parameters.AddWithValue(values[i].Name, values[i].Value);
        }

        command.ExecuteNonQuery();
    }

    // One prepared command per statement for the whole batch; its values are bound fresh each run.
    private static SqliteCommand Command(Dictionary<string, SqliteCommand> commands, SqliteTransaction transaction, string sql)
    {
        if (!commands.TryGetValue(sql, out var command))
        {
            command = transaction.Connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = transaction;
            commands[sql] = command;
        }

        command.Parameters.Clear();
        return command;
    }

    private void ReadRows(string sql, Action<SqliteDataReader> readRow)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            readRow(reader);
        }
    }

    private static void Execute(SqliteConnection connection, string sql, SqliteTransaction transaction = null)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        command.ExecuteNonQuery();
    }

    private static long ToTicks(DateTime at) => (at.Kind == DateTimeKind.Local ? at.ToUniversalTime() : at).Ticks;

    private static DateTime FromTicks(long ticks) => new(ticks, DateTimeKind.Utc);

    public void Dispose() => _connection.Dispose();
}
