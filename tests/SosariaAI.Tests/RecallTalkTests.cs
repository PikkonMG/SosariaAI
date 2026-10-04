using System.Collections.Generic;
using SosariaAI.Behaviour;
using SosariaAI.Memory;
using SosariaAI.Social;
using Xunit;
using static SosariaAI.Tests.RecallTestStore;

namespace SosariaAI.Tests;

public class RecallTalkTests
{
    private const int RollsTried = 20;
    private const int HoursAgo = 2;
    private const int OneTelling = 1;
    private const int FeudDelta = -60;
    private const string SpokenDespise = "despise";

    private static List<string> Heard(RecallTestStore memory, string category, string tellerId, string listenerId, string listenerName, long expectedId)
    {
        var heard = new List<string>();

        for (var roll = 0; roll < RollsTried; roll++)
        {
            var told = GreetingLines.Recollect(memory.Store, category, tellerId, listenerId, listenerName, roll);

            Assert.NotNull(told);
            Assert.Equal(expectedId, told.Value.AdventureId);
            Assert.DoesNotContain(TalkSlots.TokenOpen, told.Value.Line);
            heard.Add(told.Value.Line);
        }

        return heard;
    }

    [Fact]
    public void OldFriend_FillsFriendPlaceAndDeed_FromARealSharedRun_AndTheTellingCounts()
    {
        using var memory = new RecallTestStore();
        var id = memory.Record(
            AdventureKinds.Dungeon,
            Despise,
            Now.AddHours(-HoursAgo),
            "Cleared Despise",
            (Halvard, AdventureRoles.With),
            (Tamsin, AdventureRoles.Healer),
            (Grim, AdventureRoles.Against)
        );

        var heard = Heard(memory, TalkCategory.GreetOldFriend, Halvard.Id, Tamsin.Id, Tamsin.Name, id);

        Assert.Contains(heard, line => line.Contains(Tamsin.Name));
        Assert.Contains(heard, line => line.Contains(SpokenDespise));
        Assert.Contains(heard, line => line.Contains(TalkWords.Deed(AdventureKinds.Dungeon)));

        memory.Store.NoteTold(id);

        Assert.Equal(OneTelling, memory.Store.AdventuresOf(Halvard.Id)[0].ToldCount);
    }

    [Fact]
    public void RecallAdventure_TellsTheSharedRedKillInSmallTalk()
    {
        using var memory = new RecallTestStore();
        var id = memory.Record(
            AdventureKinds.RedKill,
            Britain,
            Now.AddHours(-HoursAgo),
            "Bryn killed the red Grim at Britain",
            (Halvard, AdventureRoles.With),
            (Bryn, AdventureRoles.Killer),
            (Grim, AdventureRoles.Against)
        );

        var heard = Heard(memory, TalkCategory.RecallAdventure, Bryn.Id, Halvard.Id, Halvard.Name, id);

        Assert.Contains(heard, line => line.Contains(TalkWords.Deed(AdventureKinds.RedKill)));
        Assert.Contains(heard, line => line.Contains(Halvard.Name));
    }

    [Fact]
    public void Recollect_TheWild_SkipsLinesThatNameAPlace()
    {
        using var memory = new RecallTestStore();
        var id = memory.Record(
            AdventureKinds.Hunt,
            PlaceNameRules.Wild,
            Now.AddHours(-HoursAgo),
            "Hunted orcs",
            (Halvard, AdventureRoles.With),
            (Bryn, AdventureRoles.With)
        );

        var heard = Heard(memory, TalkCategory.GreetOldFriend, Halvard.Id, Bryn.Id, Bryn.Name, id);

        Assert.All(heard, line => Assert.DoesNotContain(PlaceNameRules.Wild, line));
    }

    [Fact]
    public void Recollect_NothingSharedOnOneSide_OrAColdBond_SaysNothing()
    {
        using var memory = new RecallTestStore();
        memory.Record(
            AdventureKinds.Duel,
            Britain,
            Now.AddHours(-HoursAgo),
            "Halvard beat Grim in a duel at Britain",
            (Halvard, AdventureRoles.With),
            (Grim, AdventureRoles.Against)
        );
        memory.Record(
            AdventureKinds.Dungeon,
            Despise,
            Now.AddHours(-HoursAgo),
            "Cleared Despise",
            (Halvard, AdventureRoles.With),
            (Bryn, AdventureRoles.With)
        );
        memory.Store.ShiftBond(Halvard, Bryn, FeudDelta, "stole my loot", Now);

        Assert.Null(GreetingLines.Recollect(memory.Store, TalkCategory.GreetOldFriend, Halvard.Id, Tamsin.Id, Tamsin.Name, 0));
        Assert.Null(GreetingLines.Recollect(memory.Store, TalkCategory.GreetOldFriend, Halvard.Id, Grim.Id, Grim.Name, 0));
        Assert.Null(GreetingLines.Recollect(memory.Store, TalkCategory.GreetOldFriend, Halvard.Id, Bryn.Id, Bryn.Name, 0));
    }
}
