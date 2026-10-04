using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Mobiles;
using SosariaAI.Configuration;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using SosariaAI.Social;
using Xunit;
using EngineParty = Server.Engines.PartySystem.Party;

namespace SosariaAI.Tests;

/// <summary>How adventures are made: each test writes to its own fresh temp memory file.</summary>
public sealed class AdventureTrackerTests : IDisposable
{
    private const int GhostBody = 0x192;
    private const int RunKills = 3;
    private const int LightHit = 10;
    private const int HeavyHit = 25;
    private const int RedKills = 5;
    private const int DespiseX = 5490;
    private const int DespiseY = 600;
    private const int GateX = 4467;
    private const int GateY = 1283;
    private const int TwoMembers = 2;
    private const int ThreeMembers = 3;
    private const int FourMembers = 4;
    private const int ThreeAdventures = 3;
    private const int ManyKills = 34;
    private const int FullHits = 100;
    private const int LowHits = 40;
    private const int HighHits = 80;
    private const int NoHits = 0;
    private const int NoKills = 0;
    private const uint FirstRunFoeSerial = 0x7C80;
    private const uint HuntFoeSerial = 0x7C90;
    private const string Felucca = "Felucca";
    private const string Despise = "Despise";

    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly AdventureSpot InDespise = new(Despise, Felucca, DespiseX, DespiseY);
    private static readonly AdventureSpot OutsideDespise = new("Britain", Felucca, DespiseX, DespiseY);

    private static readonly PersonRef Halvard = PersonRef.Bot("Felucca:halvard", "Halvard");
    private static readonly PersonRef Bryn = PersonRef.Bot("Felucca:bryn", "Bryn");
    private static readonly PersonRef Tamsin = PersonRef.Player((Serial)0x7C01, "Tamsin");
    private static readonly PersonRef Grim = PersonRef.Bot("Felucca:grim", "Grim");

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "sosaria-adventures-" + Guid.NewGuid().ToString("N"));
    private readonly MemoryStore _store = new();
    private readonly AdventureTracker _tracker;

    static AdventureTrackerTests() => Timer.Init(0);

    public AdventureTrackerTests()
    {
        TestMap.EnsureInternal();
        TestMap.EnsureRunningWorld();
        _store.Open(Path.Combine(_folder, MemoryStore.FileName));
        _tracker = new AdventureTracker(_store);
    }

    public void Dispose()
    {
        _store.Close();

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void PartyEntersDespiseFightsAndLeaves_IsOneDungeonAdventure()
    {
        var leader = Person(0x7C14, "Halvard");
        var fallen = Person(0x7C15, "Bryn");
        var party = Party(leader, fallen);
        var halvard = PersonRef.Of(leader)!.Value;
        var bryn = PersonRef.Of(fallen)!.Value;
        var despise = new object();

        _tracker.SeeParty(party, [halvard, bryn], () => InDespise, despise, false, Now);
        _tracker.SeeParty(party, [halvard, bryn, Tamsin], () => InDespise, despise, true, Now.AddMinutes(1));

        for (var i = 0; i < RunKills; i++)
        {
            Slay(leader, FirstRunFoeSerial + (uint)i, Now.AddMinutes(2));
        }

        _tracker.NoteFell(party, bryn, Now.AddMinutes(3));
        Assert.Empty(_store.AdventuresOf(halvard.Id));

        _tracker.SeeParty(party, [halvard, bryn, Tamsin], () => OutsideDespise, null, false, Now.AddMinutes(4));

        var adventure = Assert.Single(_store.AdventuresOf(halvard.Id));
        Assert.Equal(AdventureKinds.Dungeon, adventure.Kind);
        Assert.Equal(Despise, adventure.Place);
        Assert.Equal((RunKills, AdventureRules.OneDeath), (adventure.Kills, adventure.Deaths));
        Assert.Equal(Now, adventure.StartedAt);
        Assert.Equal(Now.AddMinutes(4), adventure.EndedAt);
        Assert.Equal("Despise: 3 kills, Bryn fell once", adventure.Summary);
        Assert.Equal(
            [
                new AdventureMember(halvard, AdventureRoles.With),
                new AdventureMember(bryn, AdventureRoles.Fallen),
                new AdventureMember(Tamsin, AdventureRoles.With)
            ],
            adventure.Members
        );
        Assert.Equal(adventure.Id, Assert.Single(_store.AdventuresOf(Tamsin.Id)).Id);
        Assert.Equal(1, _store.BondOf(halvard.Id, Tamsin.Id)?.SharedCount);
        party.Disband();
    }

    [Fact]
    public void RealPlayerInTheParty_IsKeptByItsSerial()
    {
        var leader = new SosariaCharacter((Serial)0x7C10) { Name = "Halvard", CharacterId = "Felucca:tracker-halvard" };
        var player = new PlayerMobile((Serial)0x7C11) { Name = "Tamsin" };
        var party = new EngineParty(leader);
        leader.Party = party;
        party.Add(player);

        var people = AdventureTracker.PeopleIn(party);
        _tracker.SeeParty(party, people, () => InDespise, new object(), false, Now);
        _tracker.PartyFinished(party, Now.AddMinutes(1));

        var adventure = Assert.Single(_store.AdventuresOf(PersonRef.PlayerPrefix + player.Serial));
        var member = Assert.Single(adventure.Members, m => !m.Person.IsBot);
        Assert.Equal(PersonRef.PlayerPrefix + player.Serial, member.Person.Id);
        Assert.Equal("Tamsin", member.Person.Name);
        Assert.Equal(TwoMembers, adventure.Members.Count);
        party.Disband();
    }

    [Fact]
    public void PartySplits_ClosesTheRunOnTheNextTick()
    {
        var leader = new SosariaCharacter((Serial)0x7C12) { Name = "Bryn", CharacterId = "Felucca:tracker-bryn" };
        var member = new SosariaCharacter((Serial)0x7C13) { Name = "Grim", CharacterId = "Felucca:tracker-grim" };
        var party = new EngineParty(leader);
        leader.Party = party;
        party.Add(member);
        _tracker.SeeParty(party, AdventureTracker.PeopleIn(party), () => InDespise, new object(), false, Now);

        party.Disband();
        _tracker.Tick(Now.AddMinutes(1));

        Assert.Equal(TwoMembers, Assert.Single(_store.AdventuresOf(Recall.IdOf(leader))).Members.Count);
    }

    [Fact]
    public void HuntWithNothingKilled_IsNotKept_AndAQuietHuntCloses()
    {
        var quiet = new object();
        var hunter = Person(0x7C16, "Grim");
        var bloody = Party(hunter, Person(0x7C17, "Vex"));
        _tracker.SeeParty(quiet, [Halvard, Bryn], () => OutsideDespise, null, true, Now);
        _tracker.SeeParty(bloody, AdventureTracker.PeopleIn(bloody), () => OutsideDespise, null, true, Now);
        Slay(hunter, HuntFoeSerial, Now);

        _tracker.Tick(Now + AdventureRules.OutingIdleLimit);

        Assert.Empty(_store.AdventuresOf(Halvard.Id));
        var hunt = Assert.Single(_store.AdventuresOf(Recall.IdOf(hunter)));
        Assert.Equal(AdventureKinds.Hunt, hunt.Kind);
        Assert.Equal("Britain: 1 kill", hunt.Summary);
        Assert.Equal(Now + AdventureRules.OutingIdleLimit, hunt.EndedAt);
        bloody.Disband();
    }

    [Fact]
    public void PartyWithNoCharacter_OpensNothing()
    {
        _tracker.SeeParty(new object(), [Tamsin], () => InDespise, new object(), true, Now);
        _tracker.CloseAll(Now);

        Assert.Empty(_store.AdventuresOf(Tamsin.Id));
    }

    [Fact]
    public void RedDiesAtAMoongate_IsOneSharedRedKillNamingTheGate()
    {
        var red = Person(0x7C20, "Grim");
        red.Kills = RedKills;
        var first = Person(0x7C21, "Halvard");
        var second = Person(0x7C22, "Bryn");
        var otherRed = Person(0x7C23, "Vex");
        otherRed.Kills = RedKills;
        red.RegisterDamage(LightHit, first);
        red.RegisterDamage(HeavyHit, second);
        red.RegisterDamage(LightHit, otherRed);
        var gate = new AdventureSpot(PlaceNameRules.GateName("Moonglow"), Felucca, GateX, GateY);

        var id = _tracker.RedDied(red, second, gate, Now);

        var adventure = Assert.Single(_store.AdventuresOf(Recall.IdOf(first)));
        Assert.Equal(id, adventure.Id);
        Assert.Equal(id, Assert.Single(_store.AdventuresOf(Recall.IdOf(second))).Id);
        Assert.Empty(_store.AdventuresOf(Recall.IdOf(otherRed)));
        Assert.Equal(AdventureKinds.RedKill, adventure.Kind);
        Assert.Equal("the Moonglow gate", adventure.Place);
        Assert.Equal("Bryn killed the red Grim at the Moonglow gate", adventure.Summary);
        Assert.Equal(AdventureRoles.Against, adventure.MemberOf(Recall.IdOf(red))?.Role);
        Assert.Equal(AdventureRoles.Killer, adventure.MemberOf(Recall.IdOf(second))?.Role);
        Assert.Equal(AdventureRoles.With, adventure.MemberOf(Recall.IdOf(first))?.Role);
        Assert.Equal(ThreeMembers, adventure.Members.Count);
        Assert.Equal(1, _store.BondOf(Recall.IdOf(first), Recall.IdOf(second))?.SharedCount);
    }

    [Fact]
    public void RedWithNoBlueAttacker_MakesNoRedKill()
    {
        var red = Person(0x7C24, "Grim");
        red.Kills = RedKills;

        Assert.Equal(MemoryStore.NoAdventure, _tracker.RedDied(red, null, InDespise, Now));
    }

    [Fact]
    public void Death_IsFallenAgainstTheKillerWithThePartyThere()
    {
        var id = _tracker.RecordDeath(Bryn, Grim, Grim.Name, [Halvard, Tamsin], InDespise, Now);

        var adventure = Assert.Single(_store.AdventuresOf(Bryn.Id));
        Assert.Equal(id, adventure.Id);
        Assert.Equal(AdventureKinds.Death, adventure.Kind);
        Assert.Equal(AdventureRules.OneDeath, adventure.Deaths);
        Assert.Equal("Bryn fell to Grim at Despise", adventure.Summary);
        Assert.Equal(AdventureRoles.Fallen, adventure.MemberOf(Bryn.Id)?.Role);
        Assert.Equal(AdventureRoles.Against, adventure.MemberOf(Grim.Id)?.Role);
        Assert.Equal(AdventureRoles.With, adventure.MemberOf(Tamsin.Id)?.Role);
    }

    [Fact]
    public void CreatureDeath_KeepsOnlyTheFallenAndNamesTheCreature()
    {
        _tracker.RecordDeath(Bryn, null, "a troll", [], InDespise, Now);

        var adventure = Assert.Single(_store.AdventuresOf(Bryn.Id));
        Assert.Equal("Bryn fell to a troll at Despise", adventure.Summary);
        Assert.Equal([new AdventureMember(Bryn, AdventureRoles.Fallen)], adventure.Members);
    }

    [Fact]
    public void PersonDied_ByAPersonInAPartyOuting_CountsInTheRun()
    {
        var dead = Person(0x7C30, "Hale");
        var killer = Person(0x7C31, "Ivo");
        var mate = Person(0x7C32, "Jory");
        var party = new EngineParty(dead);
        dead.Party = party;
        party.Add(mate);
        _tracker.SeeParty(party, AdventureTracker.PeopleIn(party), () => InDespise, new object(), false, Now);

        _tracker.PersonDied(dead, killer, PersonRef.Of(killer), Now);
        _tracker.PartyFinished(party, Now.AddMinutes(1));

        var adventures = _store.AdventuresOf(Recall.IdOf(dead));
        var death = Assert.Single(adventures, a => a.Kind == AdventureKinds.Death);
        Assert.Equal(AdventureRoles.Against, death.MemberOf(Recall.IdOf(killer))?.Role);
        Assert.Equal(AdventureRoles.With, death.MemberOf(Recall.IdOf(mate))?.Role);
        var run = Assert.Single(adventures, a => a.Kind == AdventureKinds.Dungeon);
        Assert.Equal(AdventureRoles.Fallen, run.MemberOf(Recall.IdOf(dead))?.Role);
        Assert.Equal(AdventureRules.OneDeath, run.Deaths);
        party.Disband();
    }

    [Fact]
    public void Rescue_HealerAndHelped_AndAFightHealCountsOncePerRest()
    {
        var raise = _tracker.RecordRescue(Halvard, Bryn, true, InDespise, Now);
        var heal = _tracker.RecordRescue(Tamsin, Bryn, false, InDespise, Now);
        var again = _tracker.RecordRescue(Tamsin, Bryn, false, InDespise, Now.AddMinutes(1));
        var later = _tracker.RecordRescue(Tamsin, Bryn, false, InDespise, Now + AdventureRules.RescueRest);

        Assert.NotEqual(MemoryStore.NoAdventure, raise);
        Assert.NotEqual(MemoryStore.NoAdventure, heal);
        Assert.Equal(MemoryStore.NoAdventure, again);
        Assert.NotEqual(MemoryStore.NoAdventure, later);
        var rescue = Assert.Single(_store.AdventuresOf(Halvard.Id));
        Assert.Equal(AdventureKinds.Rescue, rescue.Kind);
        Assert.Equal("Halvard raised Bryn at Despise", rescue.Summary);
        Assert.Equal(AdventureRoles.Healer, rescue.MemberOf(Halvard.Id)?.Role);
        Assert.Equal(AdventureRoles.With, rescue.MemberOf(Bryn.Id)?.Role);
        Assert.Equal(MemoryStore.NoAdventure, _tracker.RecordRescue(Bryn, Bryn, true, InDespise, Now));
    }

    [Fact]
    public void Rescued_FromMobiles_IsKept()
    {
        var healer = Person(0x7C40, "Kara");
        var helped = Person(0x7C41, "Lorn");

        _tracker.Rescued(healer, helped, true, Now);

        Assert.Equal(AdventureKinds.Rescue, Assert.Single(_store.AdventuresOf(Recall.IdOf(helped))).Kind);
    }

    [Fact]
    public void SoloOuting_IsKeptForANotableSkillOnly()
    {
        var miner = Person(0x7C50, "Mott");

        var mined = _tracker.SkillEnded(miner, SkillKinds.Mine, SkillStatus.Done, Now);
        var loitered = _tracker.SkillEnded(miner, SkillKinds.Loiter, SkillStatus.Done, Now);

        Assert.NotEqual(MemoryStore.NoAdventure, mined);
        Assert.Equal(MemoryStore.NoAdventure, loitered);
        var outing = Assert.Single(_store.AdventuresOf(Recall.IdOf(miner)));
        Assert.Equal(AdventureKinds.Outing, outing.Kind);
        Assert.Equal(MemoryFade.OutingWeight, outing.Weight);
        Assert.Equal($"{SkillKinds.Mine} done at {PlaceNameRules.Wild}", outing.Summary);
        Assert.Equal([new AdventureMember(PersonRef.Of(miner)!.Value, AdventureRoles.With)], outing.Members);
    }

    [Fact]
    public void DuelHouseAndFirstKill_AreKept_AndAFirstKillOnlyOnce()
    {
        var winner = Person(0x7C60, "Nell");
        var beaten = Person(0x7C61, "Orin");
        var victim = Person(0x7C62, "Pell");

        _tracker.Duel(winner, beaten, Now);
        _tracker.House(winner, Now);
        var first = _tracker.FirstKill(winner, victim, Now);
        var second = _tracker.FirstKill(winner, beaten, Now.AddMinutes(1));

        Assert.NotEqual(MemoryStore.NoAdventure, first);
        Assert.Equal(MemoryStore.NoAdventure, second);
        var kept = _store.AdventuresOf(Recall.IdOf(winner));
        Assert.Equal(ThreeAdventures, kept.Count);
        var duel = Assert.Single(kept, a => a.Kind == AdventureKinds.Duel);
        Assert.Equal(AdventureRoles.Killer, duel.MemberOf(Recall.IdOf(winner))?.Role);
        Assert.Equal(AdventureRoles.Against, duel.MemberOf(Recall.IdOf(beaten))?.Role);
        Assert.Single(kept, a => a.Kind == AdventureKinds.House);
        var kill = Assert.Single(kept, a => a.Kind == AdventureKinds.FirstKill);
        Assert.Equal(AdventureRoles.Against, kill.MemberOf(Recall.IdOf(victim))?.Role);
    }

    [Fact]
    public void PartySummary_CountsKillsAndFalls()
    {
        Assert.Equal("Despise: no kills", AdventureRules.PartySummary(Despise, NoKills, []));
        Assert.Equal(
            "Despise: 34 kills, Halvard fell once, Bryn fell twice, Grim fell 4 times",
            AdventureRules.PartySummary(
                Despise,
                ManyKills,
                new List<(string, int)> { ("Halvard", AdventureRules.OneDeath), ("Bryn", TwoMembers), ("Grim", FourMembers) }
            )
        );
    }

    [Fact]
    public void IsNotableSkill_KeepsTheDaysWorkNotTheSteps()
    {
        Assert.False(AdventureRules.IsNotableSkill(SkillKinds.IdleWander));
        Assert.False(AdventureRules.IsNotableSkill(SkillKinds.Loiter));
        Assert.False(AdventureRules.IsNotableSkill(SkillKinds.Sightsee));
        Assert.True(AdventureRules.IsNotableSkill(SkillKinds.Hunt));
        Assert.True(AdventureRules.IsNotableSkill(SkillKinds.Mine));
        Assert.True(AdventureRules.IsNotableSkill(SkillKinds.Dungeon));
        Assert.True(AdventureRules.IsNotableSkill(GhostSkill.SkillName));
    }

    [Fact]
    public void HealIsRescue_OnlyLowAndInAFight()
    {
        Assert.True(AdventureRules.HealIsRescue(true, LowHits, FullHits));
        Assert.False(AdventureRules.HealIsRescue(true, HighHits, FullHits));
        Assert.False(AdventureRules.HealIsRescue(false, LowHits, FullHits));
        Assert.False(AdventureRules.HealIsRescue(true, NoHits, NoHits));
    }

    [Fact]
    public void GateName_NamesTheTownItServes()
    {
        Assert.Equal("the Moonglow gate", PlaceNameRules.GateName(" Moonglow "));
        Assert.Null(PlaceNameRules.GateName(null));
        Assert.Null(PlaceNameRules.GateName("  "));
    }

    private static SosariaCharacter Person(uint serial, string name) =>
        new((Serial)serial) { Name = name, CharacterId = "Felucca:tracker-" + name.ToLowerInvariant() };

    private static EngineParty Party(SosariaCharacter leader, SosariaCharacter member)
    {
        var party = new EngineParty(leader);
        leader.Party = party;
        party.Add(member);
        return party;
    }

    // The killer's party fights a foe, the killer lands the last blow, and the next tick
    // counts the kill. The foe falls as a ghost: a test world cannot delete a bare mobile.
    private void Slay(Mobile killer, uint foeSerial, DateTime now)
    {
        var foe = new PlayerMobile((Serial)foeSerial) { Player = true };
        _tracker.Fight(killer, foe, now);
        foe.LastKiller = killer;
        foe.Body = GhostBody;
        _tracker.Tick(now);
    }
}
