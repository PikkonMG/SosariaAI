using System;
using SosariaAI.Behaviour;
using SosariaAI.Memory;
using Xunit;
using static SosariaAI.Tests.RecallTestStore;

namespace SosariaAI.Tests;

public class RecallChoiceTests
{
    private const int FriendDelta = 30;
    private const int EnemyDelta = -40;
    private const int AcquaintanceDelta = 5;
    private const int HoursAgo = 2;
    private const int DaysAgo = 3;
    private const string Shame = "Shame";
    private const string Covetous = "Covetous";
    private const string Destard = "Destard";

    private static readonly TimeSpan DeathMemory = TimeSpan.FromHours(24);

    [Fact]
    public void FriendAndEnemy_ComeFromTheBonds()
    {
        using var memory = new RecallTestStore();
        memory.Store.ShiftBond(Halvard, Bryn, FriendDelta, "healed me", Now);
        memory.Store.ShiftBond(Halvard, Grim, EnemyDelta, "killed me", Now);
        memory.Store.ShiftBond(Halvard, Tamsin, AcquaintanceDelta, "greeted", Now);

        Assert.Equal(Bryn.Id, MemoryChoiceRules.FriendId(memory.Store, Halvard.Id));
        Assert.Equal(Bryn.Name, MemoryChoiceRules.FriendName(memory.Store, Halvard.Id));
        Assert.Equal(Grim.Name, MemoryChoiceRules.EnemyName(memory.Store, Halvard.Id));
        Assert.True(MemoryChoiceRules.Avoids(memory.Store, Halvard.Id, Grim.Id));
        Assert.False(MemoryChoiceRules.Avoids(memory.Store, Halvard.Id, Bryn.Id));
        Assert.False(MemoryChoiceRules.Avoids(memory.Store, Halvard.Id, Tamsin.Id));
    }

    [Fact]
    public void FriendAndEnemy_NobodyWhenNoBondLeansEitherWay()
    {
        using var memory = new RecallTestStore();
        memory.Store.NoteMet(Halvard, Tamsin, Britain, Now);

        Assert.Null(MemoryChoiceRules.FriendId(memory.Store, Halvard.Id));
        Assert.Null(MemoryChoiceRules.FriendName(memory.Store, Halvard.Id));
        Assert.Null(MemoryChoiceRules.EnemyName(memory.Store, Halvard.Id));
        Assert.Null(MemoryChoiceRules.FriendName(memory.Store, Bryn.Id));
    }

    [Fact]
    public void FriendDeathPlaces_OnlyAWarmFriendsRecentFall()
    {
        using var memory = new RecallTestStore();
        memory.Store.ShiftBond(Halvard, Bryn, FriendDelta, "healed me", Now);
        memory.Store.ShiftBond(Halvard, Grim, EnemyDelta, "killed me", Now);
        memory.Record(AdventureKinds.Death, Despise, Now.AddHours(-HoursAgo), "Bryn died at Despise", (Bryn, AdventureRoles.Fallen));
        memory.Record(AdventureKinds.Death, Destard, Now.AddDays(-DaysAgo), "Bryn died at Destard", (Bryn, AdventureRoles.Fallen));
        memory.Record(AdventureKinds.Death, Shame, Now.AddHours(-HoursAgo), "Grim died at Shame", (Grim, AdventureRoles.Fallen));

        var places = MemoryChoiceRules.FriendDeathPlaces(memory.Store, Halvard.Id, Now, DeathMemory);

        Assert.Equal([Despise], places);
        Assert.True(DeathAvoid.FriendFellAt(places, null, Despise));
        Assert.True(DeathAvoid.FriendFellAt(places, "despise", null));
        Assert.False(DeathAvoid.FriendFellAt(places, Covetous, Shame));
        Assert.False(DeathAvoid.FriendFellAt(null, Despise, Despise));
    }

    [Fact]
    public void Lfg_PrefersABondedFriendOverAStranger()
    {
        using var memory = new RecallTestStore();
        memory.Record(AdventureKinds.Dungeon, Despise, Now.AddDays(-1), "Cleared Despise", (Halvard, AdventureRoles.With), (Bryn, AdventureRoles.With));
        memory.Record(AdventureKinds.Dungeon, Despise, Now.AddHours(-HoursAgo), "Cleared Despise", (Halvard, AdventureRoles.With), (Bryn, AdventureRoles.With));
        memory.Store.ShiftBond(Halvard, Grim, EnemyDelta, "killed me", Now);

        var stranger = memory.Store.BondOf(Halvard.Id, Tamsin.Id);
        var crewmate = memory.Store.BondOf(Halvard.Id, Bryn.Id);
        var enemy = memory.Store.BondOf(Halvard.Id, Grim.Id);

        Assert.Null(stranger);
        Assert.False(LfgRules.IsCrewmate(stranger));
        Assert.True(LfgRules.IsCrewmate(crewmate));
        Assert.False(LfgRules.IsCrewmate(enemy));
        Assert.Equal(2, LfgRules.FavoredIndex([enemy, stranger, crewmate]));
        Assert.Equal(1, LfgRules.FavoredIndex([enemy, stranger]));
        Assert.Equal(0, LfgRules.FavoredIndex([]));
    }
}
