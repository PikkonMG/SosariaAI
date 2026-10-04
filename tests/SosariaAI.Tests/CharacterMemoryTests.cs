using System;
using System.IO;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// The per-character facade: recent thoughts stay in RAM; bonds and places seen go to
/// <see cref="MemoryStore.Shared"/>, opened on a fresh temp file for each test.
/// </summary>
public sealed class CharacterMemoryTests : IDisposable
{
    private const int OverMaxDelta = 500;
    private const int ExtraThoughts = 5;
    private const int TwoPlaces = 2;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "sosaria-character-memory-" + Guid.NewGuid().ToString("N"));

    static CharacterMemoryTests() => Timer.Init(0);

    public CharacterMemoryTests()
    {
        TestMap.EnsureInternal();
        MemoryStore.Shared.Open(Path.Combine(_folder, MemoryStore.FileName));
    }

    public void Dispose()
    {
        MemoryStore.Shared.Close();

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Remember_DropsABackToBackRepeat()
    {
        var character = new SosariaCharacter((Serial)0x7102) { Name = "Bryn" };

        character.Remember("Met a tanner in Vesper.");
        character.Remember("Met a tanner in Vesper.");
        character.Remember("  ");

        Assert.Single(character.Memory.Working.Thoughts());
    }

    [Fact]
    public void Remember_KeepsTheThoughtsBounded()
    {
        var character = new SosariaCharacter((Serial)0x7103) { Name = "Cedric" };

        for (var i = 0; i < WorkingMemory.ThoughtCapacity + ExtraThoughts; i++)
        {
            character.Remember($"Saw fair {i}.");
        }

        var thoughts = character.Memory.Working.Thoughts();
        Assert.Equal(WorkingMemory.ThoughtCapacity, thoughts.Count);
        Assert.Equal($"Saw fair {ExtraThoughts}.", thoughts[0]);
    }

    [Fact]
    public void ShiftBond_MovesEachRuleAndStaysInRange()
    {
        var owner = Character(0x7104, "Dara");
        var healer = Character(0x7105, "Edda");
        var killer = Character(0x7106, "Fenn");
        var trader = Character(0x7107, "Gorm");
        var duelist = Character(0x7108, "Hale");
        var rude = Character(0x7109, "Ivo");

        owner.Memory.ShiftBond(healer, BondRules.HealBonus, BondRules.HealedReason);
        owner.Memory.ShiftBond(killer, -BondRules.KillPenalty, BondRules.KilledReason);
        owner.Memory.ShiftBond(trader, -BondRules.OutbidPenalty, TradeDeal.OutbidReason);
        owner.Memory.ShiftBond(duelist, DuelRules.FriendlyBond, DuelRules.FriendlyReason);
        owner.Memory.ShiftBond(rude, -SpeechResponder.InsultPenalty, SpeechResponder.InsultReason);

        Assert.Equal(BondRules.HealBonus, BondTo(owner, healer).Score);
        Assert.Equal(-BondRules.KillPenalty, BondTo(owner, killer).Score);
        Assert.Equal(BondRules.KilledReason, BondTo(owner, killer).LastReason);
        Assert.Equal(-BondRules.OutbidPenalty, BondTo(owner, trader).Score);
        Assert.Equal(DuelRules.FriendlyBond, BondTo(owner, duelist).Score);
        Assert.Equal(-SpeechResponder.InsultPenalty, BondTo(owner, rude).Score);
        Assert.Null(BondTo(healer, owner));

        owner.Memory.ShiftBond(healer, OverMaxDelta, BondRules.HealedReason);
        owner.Memory.ShiftBond(killer, -OverMaxDelta, BondRules.KilledReason);

        Assert.Equal(BondRules.MaxScore, BondTo(owner, healer).Score);
        Assert.Equal(BondRules.MinScore, BondTo(owner, killer).Score);
    }

    [Fact]
    public void ShiftBond_ReachesAKillerThatLeftTheWorld()
    {
        var owner = Character(0x710A, "Jory");
        var gone = PersonRef.Bot("Felucca:memory-gone", "Kerr");

        owner.Memory.ShiftBond(gone, -BondRules.KillPenalty, BondRules.KilledReason);

        Assert.Equal(-BondRules.KillPenalty, MemoryStore.Shared.BondOf(Recall.IdOf(owner), gone.Id)?.Score);
    }

    [Fact]
    public void ShiftBond_NeedsTwoPeople()
    {
        var owner = Character(0x710B, "Lorn");
        var nameless = new SosariaCharacter((Serial)0x710C) { Name = "Mott" };

        owner.Memory.ShiftBond(nameless, BondRules.GreetBonus, BondRules.GreetedReason);
        nameless.Memory.ShiftBond(owner, BondRules.GreetBonus, BondRules.GreetedReason);

        Assert.Empty(MemoryStore.Shared.BondsOf(Recall.IdOf(owner)));
    }

    [Fact]
    public void Decline_KeepsTheScoreAndNamesTheNo()
    {
        var leader = Character(0x710D, "Nell");
        var player = Character(0x710E, "Orin");
        leader.Memory.ShiftBond(player, BondRules.GreetBonus, BondRules.GreetedReason);

        leader.Memory.ShiftBond(player, InviteAskRules.DeclineBondShift, InviteAskRules.DeclineReason);

        Assert.Equal(BondRules.GreetBonus, BondTo(leader, player).Score);
        Assert.True(InviteAskRules.RemembersDecline(BondTo(leader, player)));
    }

    [Fact]
    public void Met_KeepsTheFirstMeetingOnce()
    {
        var owner = Character(0x710F, "Pell");
        var other = Character(0x7110, "Quin");

        owner.Memory.Met(other);
        var first = BondTo(owner, other);
        owner.Memory.Met(other);

        Assert.NotNull(first);
        Assert.Equal(first.FirstMetAt, BondTo(owner, other).FirstMetAt);
        Assert.Equal(BondRules.NeutralScore, BondTo(owner, other).Score);
    }

    [Fact]
    public void SawPlace_IsNewOnceAndKeepsEveryPlace()
    {
        var character = Character(0x7111, "Rhea");

        Assert.True(character.Memory.SawPlace("Moonglow"));
        Assert.False(character.Memory.SawPlace("MOONGLOW"));
        Assert.False(character.Memory.SawPlace("   "));
        Assert.True(character.Memory.SawPlace("Yew"));

        Assert.Equal(TwoPlaces, character.Memory.PlacesSeen().Count);
    }

    private static SosariaCharacter Character(uint serial, string name) =>
        new((Serial)serial) { Name = name, CharacterId = "Felucca:memory-" + name.ToLowerInvariant() };

    private static Bond BondTo(SosariaCharacter owner, SosariaCharacter other) =>
        MemoryStore.Shared.BondOf(Recall.IdOf(owner), Recall.IdOf(other));
}
