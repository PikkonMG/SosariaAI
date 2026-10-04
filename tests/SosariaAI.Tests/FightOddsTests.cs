using System;
using System.Collections.Generic;
using Server;
using Server.Guilds;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A guild war draw ran "too many" in the same second, got clear a few tiles on and drew again,
/// all half an hour (Bevis in the Den). A draw now puts the run test first and leaves the side
/// it ran from be; a red's run keeps its source and its grace. The core clock does not run in
/// tests, so a run inside the grace is stamped with the wall clock.
/// </summary>
[Collection(EngineGuildsCollection.Name)]
public class FightOddsTests : IDisposable
{
    private const int MasterSkill = 100;
    private const int StrongStat = 100;
    private const int WeakStat = 10;
    private const int FoeSideMates = 3;
    private const int RunSeconds = 20;
    private const int GuildIndex = 1;
    private const int Next = 1;
    private const int SecondOver = 2;
    private const int ThirdOver = 3;
    private const int FirstMateOver = SecondOver;
    private static readonly Point3D FarFoeSpot = new(20, 60, 0);
    private static readonly Point3D WeakFoeSpot = new(60, 20, 0);
    private static readonly Point3D SideSpot = new(100, 20, 0);
    private static readonly Point3D RunSpot = new(20, 100, 0);
    private static readonly Point3D PackSpot = new(60, 100, 0);
    private static readonly Point3D GuildSpot = new(100, 100, 0);
    private static readonly Point3D MobSpot = new(60, 60, 0);
    private static readonly Point3D LoneMarkSpot = new(100, 60, 0);
    private static readonly Point3D FriendsSpot = new(20, 20, 0);
    private static readonly Point3D FarPackSpot = new(120, 90, 0);
    private static readonly Point3D DraftSpot = new(120, 40, 0);
    private static readonly Point3D LineStopSpot = new(140, 60, 0);
    private static readonly Point3D EscapedMarkSpot = new(140, 100, 0);
    private static readonly Point3D UnarmedSpot = new(140, 20, 0);
    private const int OwnGuildIndex = 2;
    private const int EnemyGuildIndex = 3;
    private const int OutOfPack = FactionRules.DraftRange + 1;
    private const int SpareMates = 1;
    private static uint _nextSerial = 0x6C01;

    private readonly List<Mobile> _placed = [];

    static FightOddsTests() => Timer.Init(0);

    public FightOddsTests()
    {
        TestMap.EnsureInternal();
        TestMap.EnsureRunningWorld();
        TestSkills.EnsureTable();
    }

    // The people stand on the shared test land, where a later class would meet them.
    public void Dispose() => TestMap.Remove(_placed);

    [Fact]
    public void WouldRunFrom_AFoeFarPastTheDare_IsNotStarted()
    {
        var spot = FarFoeSpot;
        var self = Weak(spot);
        var foe = Strong(Beside(spot, Next));

        Assert.True(CombatBrain.WouldRunFrom(self, foe, _ => false));
    }

    [Fact]
    public void WouldRunFrom_AWeakFoeAlone_IsFought()
    {
        var spot = WeakFoeSpot;
        var self = Strong(spot);
        var foe = Weak(Beside(spot, Next));

        Assert.False(CombatBrain.WouldRunFrom(self, foe, _ => false));
    }

    [Fact]
    public void WouldRunFrom_TheFoesSideCounts()
    {
        var spot = SideSpot;
        var self = Strong(spot);
        var foe = Weak(Beside(spot, Next));
        var mates = new SosariaCharacter[FoeSideMates];

        for (var i = 0; i < mates.Length; i++)
        {
            mates[i] = Strong(Beside(spot, i + FirstMateOver));
        }

        Assert.True(CombatBrain.WouldRunFrom(self, foe, mobile => Array.IndexOf(mates, mobile) >= 0));
        Assert.False(CombatBrain.WouldRunFrom(self, foe, _ => false));
    }

    /// <summary>
    /// The two sides at the spot are weighed: a crowd it would run from alone is fought with the
    /// free fighters of its own guild beside it. Its workers do not count; they would not join.
    /// </summary>
    [Fact]
    public void WouldRunFrom_ACrowdItWouldRunFromAlone_IsFoughtWithItsFightersBeside()
    {
        var spot = FriendsSpot;
        var self = Fighter(Strong(spot));
        var foe = Weak(Beside(spot, Next));
        var mates = new SosariaCharacter[FoeSideMates];

        for (var i = 0; i < mates.Length; i++)
        {
            mates[i] = Weak(Beside(spot, i + FirstMateOver));
        }

        bool OnFoeSide(Mobile mobile) => Array.IndexOf(mates, mobile) >= 0;
        var guild = new Guild(self, GuildCatalog.All[OwnGuildIndex].Name, GuildCatalog.All[OwnGuildIndex].Tag);

        try
        {
            for (var i = 0; i < FoeSideMates; i++)
            {
                guild.AddMember(Strong(Beside(spot, -(i + Next))));
            }

            Assert.True(CombatBrain.WouldRunFrom(self, foe, OnFoeSide));

            for (var i = 0; i < FoeSideMates; i++)
            {
                guild.AddMember(Fighter(Strong(Beside(spot, -(i + Next)))));
            }

            Assert.False(CombatBrain.WouldRunFrom(self, foe, OnFoeSide));
        }
        finally
        {
            guild.Disband();
        }
    }

    /// <summary>
    /// A draw in a guild war brings in the free fighters of the guild near, up to a side of
    /// four with the one who drew; its workers and strangers stay out.
    /// </summary>
    [Fact]
    public void DraftGuild_BringsFreeGuildFightersIn_UpToASide()
    {
        var spot = DraftSpot;
        var caller = Fighter(Strong(spot));
        var foe = Weak(Beside(spot, Next));
        var own = new Guild(caller, GuildCatalog.All[OwnGuildIndex].Name, GuildCatalog.All[OwnGuildIndex].Tag);
        var enemy = new Guild(foe, GuildCatalog.All[EnemyGuildIndex].Name, GuildCatalog.All[EnemyGuildIndex].Tag);
        SosariaSettings.GuildWars.RecordAggression(OwnGuildIndex, EnemyGuildIndex, Core.Now);
        var fighters = new SosariaCharacter[FactionRules.MaxSide + SpareMates];

        try
        {
            var worker = Strong(Beside(spot, -Next));
            own.AddMember(worker);
            var stranger = Fighter(Strong(Beside(spot, -SecondOver)));
            caller.JoinAgainst(foe);

            FactionWar.DraftGuild(caller, foe);

            Assert.Null(worker.Combatant);
            Assert.Null(stranger.Combatant);

            for (var i = 0; i < fighters.Length; i++)
            {
                fighters[i] = Fighter(Strong(Beside(spot, -(i + ThirdOver))));
                own.AddMember(fighters[i]);
            }

            FactionWar.DraftGuild(caller, foe);

            Assert.Equal(FactionRules.MatesBesideDrawer, Array.FindAll(fighters, mate => mate.Combatant == foe).Length);
        }
        finally
        {
            own.Disband();
            enemy.Disband();
        }
    }

    [Fact]
    public void WouldRunFrom_ARedBeforeAMob_IsNotStarted()
    {
        var spot = MobSpot;
        var red = Outlaw(spot);
        var mark = Weak(Beside(spot, Next));

        for (var i = 0; i < OutlawRules.CrowdRetreat; i++)
        {
            Weak(Beside(spot, i + FirstMateOver));
        }

        Assert.True(WorldPlay.WouldRunFrom(red, mark, _ => false));
    }

    [Fact]
    public void WouldRunFrom_ARedOnALoneMark_Starts()
    {
        var spot = LoneMarkSpot;
        var red = Outlaw(spot);

        Assert.False(WorldPlay.WouldRunFrom(red, Weak(Beside(spot, Next)), _ => false));
    }

    [Fact]
    public void RunDecided_RunsAndLeavesTheOneRunFromBe()
    {
        var spot = RunSpot;
        var self = Strong(spot);
        var foe = Strong(Beside(spot, Next));
        self.Combatant = foe;
        self.Warmode = true;

        CombatBrain.RunDecided(self, foe, TimeSpan.FromSeconds(RunSeconds));

        Assert.Null(self.Combatant);
        Assert.False(self.Warmode);
        Assert.True(self.CheckFlee());
        Assert.Equal(CharacterAction.Flee, self.Motor.Action);
        Assert.Equal(foe.Serial, self.LastRanFrom);
        Assert.Equal(Core.Now, self.LastRanFromAt);
    }

    [Fact]
    public void RanFromGroup_TheRedPackOfTheOneRunFrom_OnlyAfterARun()
    {
        var spot = PackSpot;
        var self = Strong(spot);
        var red = Red(Weak(Beside(spot, Next)));
        var packmate = Red(Weak(Beside(spot, SecondOver)));
        var stranger = Weak(Beside(spot, ThirdOver));
        self.LastRanFrom = red.Serial;
        self.LastRanFromAt = DateTime.UtcNow;

        Assert.True(WorldPlay.RanFromGroup(self, red, packmate));
        Assert.False(WorldPlay.RanFromGroup(self, red, stranger));

        self.LastRanFromAt = default;

        Assert.False(WorldPlay.RanFromGroup(self, red, packmate));
    }

    /// <summary>
    /// The run grace covers the pack that stood with the one run from, not its whole guild or
    /// every red on the facet: one run from one member left a guild of dozens alone for two minutes.
    /// </summary>
    [Fact]
    public void RanFromGroup_OnlyThePackNearTheOneRunFrom()
    {
        var spot = FarPackSpot;
        var self = Strong(spot);
        var ranFrom = Red(Weak(Beside(spot, Next)));
        var nearMate = Weak(Beside(spot, SecondOver));
        var farMate = Weak(new Point3D(spot.X + Next, spot.Y - OutOfPack, spot.Z));
        var farRed = Red(Weak(new Point3D(spot.X + Next, spot.Y + OutOfPack, spot.Z)));
        var guild = new Guild(ranFrom, GuildCatalog.All[OwnGuildIndex].Name, GuildCatalog.All[OwnGuildIndex].Tag);

        try
        {
            guild.AddMember(nearMate);
            guild.AddMember(farMate);
            self.LastRanFrom = ranFrom.Serial;
            self.LastRanFromAt = DateTime.UtcNow;

            Assert.True(WorldPlay.RanFromGroup(self, ranFrom, nearMate));
            Assert.False(WorldPlay.RanFromGroup(self, ranFrom, farMate));
            Assert.False(WorldPlay.RanFromGroup(self, ranFrom, farRed));
        }
        finally
        {
            guild.Disband();
        }
    }

    [Fact]
    public void MayEngage_NotTheGuildOfTheOneRunFrom()
    {
        var spot = GuildSpot;
        var self = Strong(spot);
        var ranFrom = Weak(Beside(spot, Next));
        var guildmate = Weak(Beside(spot, SecondOver));
        var stranger = Weak(Beside(spot, ThirdOver));
        var farGuildmate = Weak(new Point3D(spot.X, spot.Y - OutOfPack, spot.Z));
        var guild = new Guild(ranFrom, GuildCatalog.All[GuildIndex].Name, GuildCatalog.All[GuildIndex].Tag);
        guild.AddMember(guildmate);
        guild.AddMember(farGuildmate);
        World.AddEntity(ranFrom);

        try
        {
            CombatBrain.RunFrom(self, ranFrom);
            self.StopFlee();
            self.LastRanFromAt = DateTime.UtcNow;
            var ownSide = FactionWar.GuildSide(self.Guild);

            Assert.True(WorldPlay.RanFromSideLately(self, guildmate));
            Assert.False(FactionWar.MayEngage(self, ranFrom, ownSide));
            Assert.False(FactionWar.MayEngage(self, guildmate, ownSide));
            Assert.True(FactionWar.MayEngage(self, stranger, ownSide));
            Assert.True(FactionWar.MayEngage(self, farGuildmate, ownSide));
        }
        finally
        {
            World.RemoveEntity(ranFrom);
            guild.Disband();
        }
    }

    [Fact]
    public void MayEngage_AndTheDraft_LeaveBeWhoJustStoppedAtTheLine()
    {
        var spot = LineStopSpot;
        var self = Strong(spot);
        var foe = Weak(Beside(spot, Next));
        var mate = Fighter(Strong(Beside(spot, SecondOver)));
        var ownSide = FactionWar.GuildSide(self.Guild);

        Assert.True(FactionWar.MayEngage(self, foe, ownSide));
        Assert.True(FactionWar.FreeToJoin(mate, foe));

        foe.LastLineStopAt = DateTime.UtcNow;
        Assert.False(FactionWar.MayEngage(self, foe, ownSide));

        foe.LastLineStopAt = default;
        self.LastLineStopAt = DateTime.UtcNow;
        Assert.False(FactionWar.MayEngage(self, foe, ownSide));

        mate.LastLineStopAt = DateTime.UtcNow;
        Assert.False(FactionWar.FreeToJoin(mate, foe));
    }

    [Fact]
    public void MayEngage_NeverAnUnarmedFoeNorWhenUnarmed()
    {
        var spot = UnarmedSpot;
        var self = Strong(spot);
        var foe = Weak(Beside(spot, Next));
        var ownSide = FactionWar.GuildSide(self.Guild);

        Assert.True(FactionWar.MayEngage(self, foe, ownSide));

        foe.Backpack.Delete();
        foe.FindItemOnLayer(Layer.OneHanded)?.Delete();
        Assert.False(FactionWar.MayEngage(self, foe, ownSide));
        Assert.False(FactionWar.MayEngage(foe, self, ownSide));
    }

    [Fact]
    public void PassesOverMark_OneThatJustGotAwayUnderTheGuards()
    {
        var spot = EscapedMarkSpot;
        var red = Outlaw(spot);
        var mark = Weak(Beside(spot, Next));

        Assert.False(WorldPlay.PassesOverMark(red, mark));

        mark.LastLineStopAt = DateTime.UtcNow;
        Assert.True(WorldPlay.PassesOverMark(red, mark));
    }

    private static Point3D Beside(Point3D spot, int tiles) => new(spot.X + tiles, spot.Y, spot.Z);

    private SosariaCharacter Outlaw(Point3D at)
    {
        var red = Red(Strong(at));
        red.IsPk = true;
        red.HomeFacet = FacetNames.Felucca;
        return red;
    }

    private static SosariaCharacter Red(SosariaCharacter character)
    {
        character.Kills = PkRules.MurdersToRed;
        return character;
    }

    private SosariaCharacter Strong(Point3D at)
    {
        var character = Person(at, StrongStat);
        character.Skills[SkillName.Swords].Base = MasterSkill;
        character.Skills[SkillName.Tactics].Base = MasterSkill;
        character.Skills[SkillName.Anatomy].Base = MasterSkill;
        return character;
    }

    private SosariaCharacter Weak(Point3D at) => Person(at, WeakStat);

    private static SosariaCharacter Fighter(SosariaCharacter character)
    {
        character.Build = BuildPresets.SwordsmanNovice();
        return character;
    }

    private SosariaCharacter Person(Point3D at, int stat)
    {
        var character = new SosariaCharacter((Serial)_nextSerial++);
        character.DefaultMobileInit();
        character.RawStr = stat;
        character.RawDex = stat;
        character.RawInt = stat;
        character.Hits = character.HitsMax;
        TestArms.Arm(character);
        character.MoveToWorld(at, TestMap.EnsureLand());
        _placed.Add(character);
        return character;
    }
}
