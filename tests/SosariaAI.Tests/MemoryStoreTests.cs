using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Server;
using SosariaAI.Memory;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public sealed class MemoryStoreTests : IDisposable
{
    private const int ManyPeople = 200;
    private const int RecordsEach = 100;
    private const int ExtraBonds = 12;
    private const int OverMaxDelta = 500;
    private const int DaysPastSmallFade = MemoryFade.SmallFadeDays + 1;
    private const int OldDeathDays = 400;
    private const int GossipOffset = 200;
    private const int DespiseX = 5490;
    private const int DespiseY = 600;
    private const int RunKills = 4;
    private const int RunDeaths = 1;
    private const int RunMinutes = 20;
    private const int AllShared = 10;
    private const int TopTwo = 2;

    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Point3D Speaker = new(100, 100, 0);

    private static readonly PersonRef Halvard = PersonRef.Bot("Felucca:halvard", "Halvard");
    private static readonly PersonRef Bryn = PersonRef.Bot("Felucca:bryn", "Bryn");
    private static readonly PersonRef Grim = PersonRef.Bot("Felucca:grim", "Grim");
    private static readonly PersonRef Tamsin = PersonRef.Player((Serial)0x7B01, "Tamsin");

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "sosaria-memory-" + Guid.NewGuid().ToString("N"));
    private readonly List<MemoryStore> _stores = [];

    static MemoryStoreTests() => Timer.Init(0);

    private string DatabasePath => Path.Combine(_folder, MemoryStore.FileName);

    public void Dispose()
    {
        for (var i = 0; i < _stores.Count; i++)
        {
            _stores[i].Close();
        }

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Reopen_KeepsEveryTable()
    {
        var store = OpenStore();
        store.NoteMet(Halvard, Tamsin, "Britain", Now);
        store.ShiftBond(Halvard, Tamsin, BondRules.WarmThreshold, "healed me", Now.AddMinutes(1));
        var id = store.Record(
            Adventure(
                AdventureKinds.Dungeon,
                Now.AddMinutes(30),
                (Halvard, AdventureRoles.With),
                (Tamsin, AdventureRoles.Healer),
                (Grim, AdventureRoles.Against)
            )
        );
        store.NoteTold(id);
        Assert.True(store.SawPlace(Halvard.Id, "Despise", Now));
        store.Close();

        var again = OpenStore();

        Assert.Equal(Tamsin, again.PersonOf(Tamsin.Id));
        Assert.Equal(Halvard, again.PersonOf(Halvard.Id));
        var bond = again.BondOf(Halvard.Id, Tamsin.Id);
        Assert.NotNull(bond);
        Assert.Equal(BondRules.WarmThreshold + BondRules.SharedAdventureBonus, bond.Score);
        Assert.Equal(Now, bond.FirstMetAt);
        Assert.Equal("Britain", bond.FirstMetPlace);
        Assert.Equal(Now.AddMinutes(30), bond.LastSeenAt);
        Assert.Equal(1, bond.SharedCount);
        Assert.Equal("did Despise", bond.LastReason);

        var adventure = Assert.Single(again.AdventuresOf(Tamsin.Id));
        Assert.Equal(id, adventure.Id);
        Assert.Equal(AdventureKinds.Dungeon, adventure.Kind);
        Assert.Equal("Despise", adventure.Place);
        Assert.Equal("Felucca", adventure.Map);
        Assert.Equal((DespiseX, DespiseY), (adventure.X, adventure.Y));
        Assert.Equal(Now.AddMinutes(30), adventure.EndedAt);
        Assert.Equal((RunKills, RunDeaths), (adventure.Kills, adventure.Deaths));
        Assert.Equal(MemoryFade.DungeonWithCompanyWeight, adventure.Weight);
        Assert.Equal(1, adventure.ToldCount);
        Assert.Equal(
            [new AdventureMember(Halvard, AdventureRoles.With), new AdventureMember(Tamsin, AdventureRoles.Healer), new AdventureMember(Grim, AdventureRoles.Against)],
            adventure.Members
        );
        Assert.False(again.SawPlace(Halvard.Id, "Despise", Now));
        Assert.True(again.Record(Adventure(AdventureKinds.Hunt, Now, (Bryn, AdventureRoles.With))) > id);
    }

    [Fact]
    public void ShiftBond_ClampsAndKeepsMoreThanEightBonds()
    {
        var store = OpenStore();

        for (var i = 0; i < ExtraBonds; i++)
        {
            store.ShiftBond(Halvard, PersonRef.Bot("Felucca:friend" + i, "Friend" + i), BondRules.GreetBonus, BondRules.GreetedReason, Now);
        }

        store.ShiftBond(Halvard, Bryn, OverMaxDelta, "saved my life", Now);
        store.ShiftBond(Halvard, Grim, -OverMaxDelta, "killed me", Now);

        var bonds = store.BondsOf(Halvard.Id);
        Assert.Equal(ExtraBonds + 2, bonds.Count);
        Assert.Equal(BondRules.MaxScore, bonds[0].Score);
        Assert.Equal(Bryn.Id, bonds[0].OtherId);
        Assert.Equal(BondRules.MinScore, bonds[^1].Score);
        Assert.Equal("killed me", bonds[^1].LastReason);

        store.Close();
        Assert.Equal(ExtraBonds + 2, OpenStore().BondsOf(Halvard.Id).Count);
    }

    [Fact]
    public void Record_SharesOnlyBetweenMembersOnTheSameSide()
    {
        var store = OpenStore();

        var id = store.Record(
            Adventure(
                AdventureKinds.RedKill,
                Now,
                (Halvard, AdventureRoles.Healer),
                (Bryn, AdventureRoles.With),
                (Grim, AdventureRoles.Killer),
                (Tamsin, AdventureRoles.Against)
            )
        );

        Assert.NotEqual(MemoryStore.NoAdventure, id);
        Assert.Equal(1, store.BondOf(Halvard.Id, Bryn.Id)?.SharedCount);
        Assert.Equal(1, store.BondOf(Bryn.Id, Grim.Id)?.SharedCount);
        Assert.Equal(BondRules.SharedAdventureBonus, store.BondOf(Grim.Id, Halvard.Id)?.Score);
        Assert.Null(store.BondOf(Halvard.Id, Tamsin.Id));
        Assert.Null(store.BondOf(Tamsin.Id, Grim.Id));
        Assert.Single(store.AdventuresOf(Tamsin.Id));
        Assert.Equal(MemoryStore.NoAdventure, store.Record(Adventure(AdventureKinds.Hunt, Now)));
    }

    [Fact]
    public void SharedWith_PutsTheHeaviestFirstThenTheNewest()
    {
        var store = OpenStore();
        var olderOuting = store.Record(Adventure(AdventureKinds.Outing, Now.AddHours(1), (Halvard, AdventureRoles.With), (Bryn, AdventureRoles.With)));
        var death = store.Record(Adventure(AdventureKinds.Death, Now, (Halvard, AdventureRoles.Fallen), (Bryn, AdventureRoles.Killer)));
        var newerOuting = store.Record(Adventure(AdventureKinds.Outing, Now.AddHours(2), (Halvard, AdventureRoles.With), (Bryn, AdventureRoles.With)));
        var hunt = store.Record(Adventure(AdventureKinds.Hunt, Now.AddHours(3), (Halvard, AdventureRoles.With), (Bryn, AdventureRoles.With)));
        store.Record(Adventure(AdventureKinds.Rescue, Now, (Halvard, AdventureRoles.With), (Grim, AdventureRoles.Healer)));

        Assert.Equal([death, hunt, newerOuting, olderOuting], Ids(store.SharedWith(Halvard.Id, Bryn.Id, AllShared)));
        Assert.Equal([death, hunt], Ids(store.SharedWith(Bryn.Id, Halvard.Id, TopTwo)));
        Assert.Empty(store.SharedWith(Bryn.Id, Grim.Id, AllShared));
    }

    [Fact]
    public void SawPlace_IsNewOnceForEachOwner()
    {
        var store = OpenStore();

        Assert.True(store.SawPlace(Halvard.Id, "Moonglow", Now));
        Assert.False(store.SawPlace(Halvard.Id, "Moonglow", Now));
        Assert.True(store.SawPlace(Bryn.Id, "Moonglow", Now));
        Assert.False(store.SawPlace(Bryn.Id, " ", Now));

        store.Close();
        Assert.False(OpenStore().SawPlace(Bryn.Id, "Moonglow", Now));
    }

    [Fact]
    public void Fade_DropsSmallOldMemoriesAndKeepsBigOrToldOnes()
    {
        var store = OpenStore();
        var old = Now.AddDays(-DaysPastSmallFade);
        store.NoteMet(Halvard, Bryn, "Britain", old);
        store.ShiftBond(Halvard, Bryn, BondRules.ChatBonus, BondRules.ChattedReason, old);
        store.ShiftBond(Halvard, Grim, BondRules.WarmThreshold, "healed me", old);
        var outing = store.Record(Adventure(AdventureKinds.Outing, old, (Halvard, AdventureRoles.With)));
        var toldOuting = store.Record(Adventure(AdventureKinds.Outing, old, (Halvard, AdventureRoles.With)));
        var death = store.Record(Adventure(AdventureKinds.Death, Now.AddDays(-OldDeathDays), (Halvard, AdventureRoles.Fallen)));
        store.NoteTold(toldOuting);

        store.Fade(Now);

        AssertFaded(store);
        store.Close();
        AssertFaded(OpenStore());

        void AssertFaded(MemoryStore faded)
        {
            Assert.Null(faded.BondOf(Halvard.Id, Bryn.Id));
            Assert.NotNull(faded.BondOf(Halvard.Id, Grim.Id));
            Assert.Equal([toldOuting, death], Ids(faded.AdventuresOf(Halvard.Id)));
            Assert.DoesNotContain(outing, Ids(faded.AdventuresOf(Halvard.Id)));
        }
    }

    [Fact]
    public void Open_WhereNoFolderCanBeMade_RunsEmptyAndWritesNothing()
    {
        Directory.CreateDirectory(_folder);
        var blocker = Path.Combine(_folder, "blocker");
        File.WriteAllText(blocker, string.Empty);
        var store = new MemoryStore();
        _stores.Add(store);

        store.Open(Path.Combine(blocker, "sub", MemoryStore.FileName));
        store.NoteMet(Halvard, Bryn, "Britain", Now);

        Assert.False(store.IsOpen);
        Assert.Empty(store.AdventuresOf(Halvard.Id));
        Assert.NotNull(store.BondOf(Halvard.Id, Bryn.Id));
        store.Flush();
        store.Close();
    }

    [Fact]
    public void Flush_ALockedFile_KeepsTheBatchAndWritesItLater()
    {
        var store = OpenStore();
        const string countBonds = "SELECT COUNT(*) FROM bonds";
        const long noRows = 0;

        using (var holder = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString()))
        {
            holder.Open();
            using var hold = holder.CreateCommand();
            hold.CommandText = "BEGIN IMMEDIATE";
            hold.ExecuteNonQuery();

            store.NoteMet(Halvard, Bryn, "Britain", Now);
            store.Flush();

            hold.CommandText = countBonds;
            Assert.Equal(noRows, Convert.ToInt64(hold.ExecuteScalar()));
        }

        store.Flush();
        store.Close();
        Assert.NotNull(OpenStore().BondOf(Halvard.Id, Bryn.Id));
    }

    [Fact]
    public void Record_ManyPeopleOnTheGameThread_KeepsTheRamViewAndWritesItAll()
    {
        var store = OpenStore();

        for (var person = 0; person < ManyPeople; person++)
        {
            var who = PersonRef.Bot("Felucca:copy#" + person, "Copy" + person);

            for (var n = 0; n < RecordsEach; n++)
            {
                store.Record(Adventure(AdventureKinds.Outing, Now.AddMinutes(n), (who, AdventureRoles.With)));
            }
        }

        for (var person = 0; person < ManyPeople; person++)
        {
            var held = store.AdventuresOf(PersonRef.BotPrefix + "Felucca:copy#" + person);
            Assert.Equal(MemoryStore.RecentAdventuresKept, held.Count);
            Assert.Equal(Now.AddMinutes(RecordsEach - 1), held[0].EndedAt);
        }

        store.Flush();
        store.Close();

        var again = OpenStore();

        for (var person = 0; person < ManyPeople; person++)
        {
            Assert.Equal(MemoryStore.RecentAdventuresKept, again.AdventuresOf(PersonRef.BotPrefix + "Felucca:copy#" + person).Count);
        }
    }

    [Fact]
    public void Journal_SavedNewsComesBackWithItsTellings()
    {
        var now = DateTime.UtcNow;
        var store = OpenStore();
        var journal = new EventJournal(store);
        var murder = News(now - TimeSpan.FromMinutes(10), "bran");
        journal.Record(murder);
        journal.Record(News(now - GossipRules.MaxAge - TimeSpan.FromMinutes(1), "stale"));
        Assert.Same(murder, journal.PickGossip("sela", Speaker, now));
        store.Close();

        var restored = new EventJournal(OpenStore());
        restored.Restore(now);

        var back = Assert.Single(restored.Snapshot());
        Assert.Equal(murder.Id, back.Id);
        Assert.Equal("bran", back.Actor);
        Assert.Equal(1, back.TellCount);
        Assert.Equal(now, back.LastToldAt);
        Assert.True(back.ToldBy("Sela"));
        Assert.Null(restored.PickGossip("sela", Speaker, now + GossipRules.RetellAfter));
    }

    private MemoryStore OpenStore()
    {
        var store = new MemoryStore();
        _stores.Add(store);
        store.Open(DatabasePath);
        Assert.True(store.IsOpen);
        return store;
    }

    private static Adventure Adventure(string kind, DateTime endedAt, params (PersonRef Person, string Role)[] members)
    {
        var list = new List<AdventureMember>();

        for (var i = 0; i < members.Length; i++)
        {
            list.Add(new AdventureMember(members[i].Person, members[i].Role));
        }

        return new Adventure
        {
            Kind = kind,
            Place = "Despise",
            Map = "Felucca",
            X = DespiseX,
            Y = DespiseY,
            StartedAt = endedAt.AddMinutes(-RunMinutes),
            EndedAt = endedAt,
            Kills = RunKills,
            Deaths = RunDeaths,
            Summary = "did Despise",
            Members = list
        };
    }

    private static List<long> Ids(IReadOnlyList<Adventure> adventures)
    {
        var ids = new List<long>();

        for (var i = 0; i < adventures.Count; i++)
        {
            ids.Add(adventures[i].Id);
        }

        return ids;
    }

    private static ShardEvent News(DateTime at, string actor) =>
        new()
        {
            At = at,
            Type = ShardEventType.Pk,
            Actor = actor,
            Other = "red",
            Place = "Yew",
            Facet = "Felucca",
            X = Speaker.X + GossipOffset,
            Y = Speaker.Y
        };
}
