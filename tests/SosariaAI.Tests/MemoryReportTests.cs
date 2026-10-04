using SosariaAI.Memory;
using SosariaAI.Mobiles;
using Xunit;
using static SosariaAI.Tests.RecallTestStore;

namespace SosariaAI.Tests;

public class MemoryReportTests
{
    private const int HoursAgo = 2;
    private const int FriendDelta = 30;
    private const int ManyBonds = 12;

    [Fact]
    public void MemoryReport_ListsBondsAndAdventures()
    {
        using var memory = new RecallTestStore();
        memory.Store.NoteMet(Halvard, Bryn, Britain, Now.AddDays(-1));
        memory.Store.ShiftBond(Halvard, Bryn, FriendDelta, "healed me", Now.AddDays(-1));
        memory.Record(
            AdventureKinds.Dungeon,
            Despise,
            Now.AddHours(-HoursAgo),
            "Cleared Despise",
            (Halvard, AdventureRoles.With),
            (Bryn, AdventureRoles.Healer),
            (Grim, AdventureRoles.Against)
        );

        var lines = CharacterCommandText.MemoryReport(memory.Store, Halvard.Name, Halvard.Id, Now);

        Assert.Equal(CharacterCommandText.MemoryTitlePrefix + Halvard.Name, lines[0]);
        Assert.DoesNotContain(CharacterCommandText.MemoryClosed, lines);
        Assert.Contains(
            lines,
            line => line.StartsWith(CharacterCommandText.MemoryItemPrefix + Bryn.Name + ": " + Recall.WarmTone) &&
                    line.Contains("1 shared") &&
                    line.Contains("met at " + Britain + " 2026-10-02 18:00")
        );
        Assert.Contains(
            lines,
            line => line.Contains("2026-10-03 16:00 (today) " + AdventureKinds.Dungeon + ": Cleared Despise at " + Despise) &&
                    line.Contains(Bryn.Name + " " + AdventureRoles.Healer) &&
                    line.Contains(Grim.Name + " " + AdventureRoles.Against) &&
                    !line.Contains(Halvard.Name)
        );
    }

    [Fact]
    public void MemoryReport_ShowsTheTopBondsOnly_AndSaysWhenNothingIsKept()
    {
        using var memory = new RecallTestStore();

        for (var i = 0; i < ManyBonds; i++)
        {
            memory.Store.ShiftBond(Halvard, PersonRef.Bot("Felucca:friend" + i, "Friend" + i), FriendDelta + i, "greeted", Now);
        }

        var lines = CharacterCommandText.MemoryReport(memory.Store, Halvard.Name, Halvard.Id, Now);
        var empty = CharacterCommandText.MemoryReport(memory.Store, Tamsin.Name, Tamsin.Id, Now);

        Assert.Contains($"{CharacterCommandText.BondsPrefix}{CharacterCommandText.MemoryTopBonds} of {ManyBonds}", lines);
        Assert.Equal(CharacterCommandText.MemoryTopBonds, lines.FindAll(line => line.StartsWith(CharacterCommandText.MemoryItemPrefix + "Friend")).Count);
        Assert.Contains(lines, line => line.StartsWith(CharacterCommandText.MemoryItemPrefix + "Friend" + (ManyBonds - 1) + ":"));
        Assert.Contains(CharacterCommandText.BondsPrefix + CharacterCommandText.NoBonds, empty);
        Assert.Contains(CharacterCommandText.AdventuresPrefix + CharacterCommandText.NoAdventures, empty);
    }

    [Fact]
    public void MemoryReport_ClosedFile_SaysSo()
    {
        var closed = new MemoryStore();

        var lines = CharacterCommandText.MemoryReport(closed, Halvard.Name, Halvard.Id, Now);

        Assert.Contains(CharacterCommandText.MemoryClosed, lines);
    }
}
