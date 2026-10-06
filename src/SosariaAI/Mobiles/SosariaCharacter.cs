using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Engines.PlayerMurderSystem;
using Server.Guilds;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Misc;
using Server.Mobiles;
using Server.Multis;
using Server.Regions;
using Server.Targeting;
using Moves = Server.Movement.Movement;
using SosariaAI.Admin;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Deliberation;
using SosariaAI.Memory;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using SosariaAI.Population;
using SosariaAI.Skills;
using SosariaAI.Social;
using SosariaAI.Spawning;
using Skill = SosariaAI.Skills.Skill;
using SosariaAI.Logging;

namespace SosariaAI.Mobiles;

/// <summary>
/// A real player character with no NetState and no account. The engine treats it as a
/// player: notoriety, murder counts, guilds, parties, death, ghosts and resurrection are
/// the game's own. <see cref="CharacterPulse"/> drives it; <see cref="CharacterMotor"/>
/// walks it. Think rate drops when no player is within
/// <see cref="PresenceFocus.NearPlayerTiles"/>. Combat and ghosts stay fast.
/// </summary>
[SerializationGenerator(5)]
public partial class SosariaCharacter : PlayerMobile
{
    private static readonly ILogger logger = SosariaLog.For(typeof(SosariaCharacter));
    private static readonly HashSet<string> MissingKitTypes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> BadActionIds = new(StringComparer.OrdinalIgnoreCase);

    private const string MaleNameList = "male";
    private const string FemaleNameList = "female";

    private const double ActiveThinkSeconds = 0.25;
    public const int DefaultRangePerception = 16;
    private const int SkillTenths = 10;
    private const ulong FullSpellbookContent = ulong.MaxValue;
    private const int StartingBlankRunes = 3;
    private const int MinStrength = 60;
    private const int MaxStrength = 80;
    private const int MinDexterity = 40;
    private const int MaxDexterity = 60;
    private const int MinIntelligence = 20;
    private const int MaxIntelligence = 30;

    /// <summary>The engine's full belly and full thirst: food fills to this and no further.</summary>
    public const int FullHunger = 20;
    private const double MinProfessionSkill = 60.0;
    private const double MaxProfessionSkill = 80.0;

    private CharacterMotor _motor;
    private CharacterPulse _pulse;
    private double _thinkSeconds = PresenceFocus.NearThinkSeconds;
    private DateTime _fleeUntil;
    private string _restoreReason;
    private List<Mobile> _murderers;
    private const string EngineResurrectReason = "engine resurrect";
    private const string FallbackReason = "fallback";
    private const int ResurrectEffectSpeed = 10;
    private const int FameLossDivisor = 10;
    private Point3D _homeSpot;
    private Point3D _homeCorner;
    private TimerExecutionToken _speechTimer;
    private TimerExecutionToken _returnTimer;
    private TimerExecutionToken _ghostFallbackTimer;
    private CharacterMemory _memory;

    // The person behind the last killer, taken while the killer still stood: the death is
    // remembered even when that killer has left the world since.
    private PersonRef? _lastKillerPerson;
    private DayPart _lastDayPart;
    private bool _notedDayPart;
    private long _nextDangerScanAt;
    private long _nextAmbientScanAt;
    private long _nextWorldScanAt;

    [SerializableField(0)]
    private int _routineStepIndex;

    [SerializableField(1)]
    private TimeSpan _idleElapsed;

    [SerializableField(2)]
    private string _characterId;

    [SerializableField(3)]
    private Point3D _homeSpawn;

    [SerializableField(4)]
    private string _homeMapName;

    [SerializableField(5)]
    private DateTime _returnAt;

    [SerializableField(6)]
    private TimeSpan _returnAfterDeath;

    [SerializableField(7)]
    private string _activeRoutineId;

    [SerializableField(8)]
    private string _homeFacet;

    [SerializableField(9)]
    private DateTime _lastHuntAt;

    [SerializableField(10)]
    private DateTime _lastRestAt;

    [SerializableField(11)]
    private DateTime _lastTownAt;

    [SerializableField(12)]
    private int _guildIndex = -1;

    [SerializableField(13)]
    private uint _corpseSerial;

    [SerializableField(14)]
    private Point3D _corpseLocation;

    [SerializableField(15)]
    private string _deathPlace;

    [SerializableField(16)]
    private string _lastKillerName;

    [SerializableField(17)]
    private DateTime _ghostSince;

    [SerializableField(18)]
    private bool _careerStarted;

    [SerializableField(19)]
    private string _ambitionKind;

    [SerializableField(20)]
    private string _ambitionTarget;

    [SerializableField(21)]
    private int _ambitionGoal;

    [SerializableField(22)]
    private int _ambitionProgress;

    [SerializableField(23)]
    private string _activeGoalKind;

    [SerializableField(24)]
    private string _activeGoalTarget;

    [SerializableField(25)]
    private string _activeActionId;

    [SerializableField(26)]
    private bool _lastWalkFailed;

    [SerializableField(27)]
    private int _goldAtDeath;

    [SerializableField(28)]
    private int _houseSerial;

    [SerializableField(29)]
    private Point3D _houseLocation;

    [SerializableField(30)]
    private DateTime _lastDeathAt;

    [SerializableField(31)]
    private int _deathsAtPlace;

    [SerializableField(32)]
    private string _planId;

    [SerializableField(33)]
    private int _planStepIndex;

    [SerializableField(34)]
    private int _planStepFailures;

    [SerializableField(35)]
    private int _boatSerial;

    [SerializableField(36)]
    private int _vendorSerial;

    [SerializableField(37)]
    private List<string> _modelPlanLines = [];

    /// <summary>The one-time rune kit was packed, or judged not due: it is never packed again.</summary>
    [SerializableField(38, setter: "internal")]
    private bool _runeKitPacked;

    /// <summary>Rule clocks by name (<see cref="RuleClock"/>), as absolute UTC times: the save keeps every rest.</summary>
    [SerializableField(39, setter: "private")]
    private Dictionary<string, DateTime> _ruleClocks = new();

    /// <summary>Each job's failure streak at one target, by job and target (<see cref="JobTargetRest"/>).</summary>
    [SerializableField(40, setter: "private")]
    private Dictionary<string, string> _targetStreaks = new();

    /// <summary>Orders this crafter took, one <see cref="CraftOrderCodec"/> line each (<see cref="CraftOrders"/>).</summary>
    [SerializableField(41, setter: "private")]
    private List<string> _orderLines = [];

    [Constructible]
    public SosariaCharacter()
    {
        // Without the flag the engine deletes a dead mobile instead of making a ghost.
        Player = true;
        Team = SosariaCombat.Team;
        Build = BuildPresets.WorkerDefault;
        InitBody();
        EnsureOutfit();
        EnsureBackpack();
        EnsureHumanStats();
        KeepFed();
        SetWalkPace();
        StartSpeechTimer();
    }

    public Routine Routine { get; private set; }

    public CharacterMotor Motor => _motor ??= new CharacterMotor(this);

    public CharacterPulse Pulse => _pulse ??= new CharacterPulse(this);

    /// <summary>Time between decisions. Steps run on their own clock in <see cref="Motor"/>.</summary>
    public TimeSpan ThinkDelay => TimeSpan.FromSeconds(_thinkSeconds);

    /// <summary>The spot a skill pins the character to. Idle walks stay inside <see cref="RangeHome"/>.</summary>
    public Point3D Home { get; set; }

    public int RangeHome { get; set; }

    /// <summary>The foe a hunt scan picked. The fight begins when it becomes the combatant.</summary>
    public Mobile FocusMob { get; set; }

    /// <summary>Which people and creatures this character treats as fair targets on sight.</summary>
    public FightMode FightMode { get; set; } = FightMode.None;

    public int RangePerception { get; set; } = DefaultRangePerception;

    /// <summary>Characters of one team never pick each other in a scan.</summary>
    public int Team { get; set; }

    /// <summary>True while a dead character walks as a ghost.</summary>
    public bool IsGhost => !Alive;

    public CharacterDefinition Definition { get; set; }

    public FacetContent FacetContent { get; set; }

    public ResolvedBuild Build { get; internal set; } = BuildPresets.WorkerDefault;

    public bool IsHunting { get; private set; }

    private int _lastLoggedPower;

    public bool DecideResolved { get; set; }

    public bool SaidCombatLineThisHunt { get; set; }

    public ScoreResult LastScore { get; private set; }

    public string WatchedPlayerName { get; set; }

    public string InviteAskPlayer { get; set; }

    public DateTime InviteAskedAt { get; set; }

    public Serial InviteTarget { get; set; }

    public DateTime InviteCooldownUntil { get; set; }

    public int FailedHomeWalks { get; set; }

    /// <summary>The dungeon home walks are failing in, and since when; see <see cref="DungeonEscapeRules"/>.</summary>
    public DungeonTrouble DungeonTrouble { get; set; }

    private Point3D _lastStallAt = Point3D.Zero;

    private int _sameTileStalls;

    private DateTime _maroonedCheckAt;

    public int FailedGoalCount { get; set; }

    public string FailedGoalKind { get; set; }

    public GoalPlan CurrentPlan => GoalPlanRules.Restore(PlanId, PlanStepIndex, PlanStepFailures);

    public PlanningPath Planning { get; } = new();

    public WorldFacts FactsBeforeStep { get; private set; }

    public bool FoughtThisStep { get; set; }

    /// <summary>Kills landed since the last Hunt or Dungeon step began. A plan's "hunt-returned" success needs one.</summary>
    public int HuntKillsThisTrip { get; private set; }

    public Point3D HomeSpot =>
        _homeSpot == Point3D.Zero
            ? HomeSpotRules.Resolve(Point3D.Zero, HomeSpawn, LeashRadius)
            : _homeSpot;

    /// <summary>The copy's own corner of town. Home errands and idling go here; the
    /// shared bank anchor stays the home spot so bank errands still reach a bank.</summary>
    public Point3D HomeCorner =>
        _homeCorner == Point3D.Zero ? HomeSpot : _homeCorner;

    /// <summary>Where the character logs in and comes back after death.</summary>
    public Point3D LoginSpot => HomeSpotRules.LoginSpot(WorkSites.IsCopy(CharacterId), IsPk, _homeCorner, HomeSpawn);

    private static int LeashRadius =>
        SosariaSettings.Characters?.Career?.LeashRadius ?? CareerSettings.DefaultLeashRadius;

    /// <summary>A PK hunter lives by the law it hunts for, whatever its persona says.</summary>
    public DispositionKind Disposition =>
        IsPkHunter ? DispositionKind.Lawful : DispositionRules.Parse(Persona?.Disposition, IsPk);

    /// <summary>One of the few solid blue fighters who log in to hunt reds (<see cref="PkHunterRules"/>).</summary>
    public bool IsPkHunter =>
        PkHunterRules.IsHunter(
            RollsPkHunter(),
            Build?.Role == CharacterRole.Fighter,
            IsPk || PkRules.IsRed(Kills),
            PersonProfile.Tier
        );

    // The hunter dice of this id, rolled once: the disposition reads them on every look.
    private (string Id, bool Rolls) _pkHunterRoll;

    private bool RollsPkHunter()
    {
        if (!string.Equals(_pkHunterRoll.Id, CharacterId, StringComparison.Ordinal))
        {
            _pkHunterRoll = (CharacterId, PkHunterRules.RollsHunter(CharacterId));
        }

        return _pkHunterRoll.Rolls;
    }

    public Persona Persona { get; set; } = Configuration.Persona.CreateNeutral();

    public CharacterMemory Memory => _memory ??= new CharacterMemory(this);

    public ConversationHold Conversation => Memory.Working.Conversation;

    public string CurrentActivity
    {
        get
        {
            var skill = Routine?.CurrentSkill;

            if (skill == null)
            {
                return "idle";
            }

            if (skill is LumberjackSkill lumberjack)
            {
                return $"{skill.Name}: carrying {lumberjack.ResourceCount} logs";
            }

            if (skill is MineSkill mine)
            {
                return $"{skill.Name}: carrying {mine.ResourceCount} ore";
            }

            if (skill is FishSkill fish)
            {
                return $"{skill.Name}: carrying {fish.ResourceCount} fish";
            }

            if (skill is BankDepositSkill bank)
            {
                return $"{skill.Name}: deposited {bank.ItemsDeposited} items";
            }

            if (skill is VendorSellSkill sell)
            {
                return sell.ItemsSold > 0
                    ? $"{skill.Name}: sold {sell.ItemsSold} stacks for {sell.GoldTaken} gold"
                    : skill.Name;
            }

            if (skill is ConflictSkill { Doing: { } doing })
            {
                return $"{skill.Name}: {doing}";
            }

            return ActionDescriptions.Phrase(skill.Name, ActiveActionId);
        }
    }

    public string LocationDescription
    {
        get
        {
            var region = Region?.Name;

            if (string.IsNullOrEmpty(region))
            {
                region = Region?.Parent?.Name;
            }

            var mapName = Map?.Name;

            if (!string.IsNullOrEmpty(region) && !string.IsNullOrEmpty(mapName))
            {
                return $"{region}, {mapName}";
            }

            return region ?? mapName ?? "Sosaria";
        }
    }

    public bool IsPk { get; set; }

    public bool IsEnemy(Mobile m)
    {
        if (m == null || m == this || IsGhost || !CanSee(m))
        {
            return false;
        }

        if (m is SosariaCharacter { IsGhost: true })
        {
            return false;
        }

        // Reds, and grays who hurt people, are fair game for any blue before any monster.
        if (IsOutlawTarget(m))
        {
            return true;
        }

        // A declared guild war makes the other side a real enemy. Without this the
        // threat scan was blind to war opponents and the beaten side just stood there.
        // Out in the open only, with room and both armed: a war is not fought at a bank, a healer,
        // a moongate or a shrine, nor just stood down from at one, nor by a blue in Buccaneer's Den
        // off a raid.
        if (m is SosariaCharacter opponent &&
            SosariaSettings.GuildWars.AtWar(GuildIndex, opponent.GuildIndex, Core.Now))
        {
            return WorldPlay.OutOfDeathGrace(LastDeathAt, Core.Now) &&
                   WorldPlay.OutOfDeathGrace(opponent.LastDeathAt, Core.Now) &&
                   !FactionWar.Feuding(this, opponent) &&
                   FactionWar.HasRoomToFight(this) && FactionWar.HasRoomToFight(opponent) &&
                   FactionRules.MayFight(FactionWar.Armed(this), FactionWar.Armed(opponent)) &&
                   !StoppedAtLineLately && !opponent.StoppedAtLineLately &&
                   WorldPlay.MayStartFightInDen(this, opponent);
        }

        if (IsPk && HomeFacet == FacetNames.Felucca && People.IsLivingPlayer(m))
        {
            if (WorldPlay.InDeathGrace(this, m, Core.Now))
            {
                return false;
            }

            var courage = DispositionRules.CourageOf(Persona?.ResolvedDrives());
            var (pack, crowd) = RedGang.Surroundings(this);
            return OutlawRules.MayAttack(
                    Disposition,
                    felucca: true,
                    guards: UnderGuards(this) || UnderGuards(m),
                    pack,
                    crowd,
                    inDungeon: Region?.IsPartOf<DungeonRegion>() == true,
                    selfPower: CharacterPower.For(this),
                    targetPower: CharacterPower.For(m),
                    courage: courage
                ) &&
                PkRules.MayAttack(
                    true,
                    UnderGuards(this),
                    UnderGuards(m),
                    PkRules.IsRed(m.Kills),
                    PkRules.InBuccaneersDen(X, Y) || PkRules.InBuccaneersDen(m.X, m.Y)
                );
        }

        var isControlled = false;
        var isSummoned = false;
        var alwaysMurderer = false;
        var isInvulnerable = m.Blessed;

        if (m is BaseCreature creature)
        {
            isControlled = creature.Controlled;
            isSummoned = creature.Summoned;
            alwaysMurderer = creature.AlwaysMurderer;
            isInvulnerable = isInvulnerable || creature.IsInvulnerable;
            var master = creature.GetMaster();

            if (master is PlayerMobile)
            {
                return false;
            }
        }

        return EnemyRules.IsEnemy(
            m.Karma,
            alwaysMurderer,
            People.IsHuman(m),
            m is SosariaCharacter,
            isControlled,
            isSummoned,
            m is BaseVendor,
            isInvulnerable
        );
    }

    public static bool UnderGuards(Mobile mobile) =>
        mobile != null && GuardCall.IsGuardedPlace(mobile.Location, mobile.Map);

    // A PlayerMobile hears nothing by default. The driver decides what a character listens to.
    public override bool HandlesOnSpeech(Mobile from) => RoutineDriver.HandlesOnSpeech(this, from);

    /// <summary>
    /// A player shoves through a body only on full stamina, so a crowd at a door or a bank
    /// jammed every walker in it. A character pushes past like a player who keeps walking.
    /// </summary>
    public override bool CheckShove(Mobile shoved) => true;

    public override void OnSpeech(SpeechEventArgs e)
    {
        base.OnSpeech(e);
        RoutineDriver.OnSpeech(this, e);
    }

    public override void AggressiveAction(Mobile aggressor, bool criminal)
    {
        base.AggressiveAction(aggressor, criminal);

        // A tamer takes the blows of the beast it tames, as players did: hitting back breaks the taming.
        if (TameSkill.TakesBlowFrom(this, aggressor))
        {
            Combatant = null;
            return;
        }

        GearEquip.EquipReadyWeapon(this);
    }

    public void JoinAgainst(Mobile aggressor)
    {
        if (IsGhost || aggressor == null || aggressor.Deleted || !aggressor.Alive)
        {
            return;
        }

        // A fight this character just walked or ran away from stays walked away from:
        // rallies and assists cannot re-light it inside the grace window.
        if (LeavesBe(aggressor))
        {
            return;
        }

        GearEquip.EquipReadyWeapon(this);
        RoutineDriver.HoldForCalledFight(this);

        Combatant = aggressor;
        Warmode = true;
        FightMode = FightMode.Evil;
        Motor.Action = CharacterAction.Combat;
    }

    public void AttachRoutine(Routine routine)
    {
        Routine = routine;
        routine.Bind(this);
        ActivityLog.Subscribe(routine);
        Brain.Watch(routine);
        EnsureRoleGear();
        KeepFed();
    }

    /// <summary>
    /// A person here stays fed. The engine's hunger clock ticks
    /// only for a mobile with a client, and only the character creation screen fills the
    /// belly, which this person never passes: every one of them stood at hunger 0, starving,
    /// for a GM who looked. Each bind fills it again, so an old save comes back fed.
    /// </summary>
    internal void KeepFed()
    {
        Hunger = FullHunger;
        Thirst = FullHunger;
    }

    public void Remember(string line) => Memory.Working.Think(line);

    /// <summary>
    /// Stands about where the character is: a fixture outside its hours, or one with no
    /// action. A character already loitering keeps its stand; one in a party or underground
    /// finishes its run first: a fixture leader put to loiter every minute broke its group's
    /// dungeon run off at the first floor.
    /// </summary>
    public void IdleAtCurrentSpot()
    {
        Persona ??= Configuration.Persona.CreateNeutral();

        if ((Routine != null && LoiterSpotRules.AlreadyLoitering(Routine.CurrentSkill?.Name, Routine.NeedsNext)) ||
            GameParty.InParty(this) || DungeonTripSkill.InDungeon(this))
        {
            return;
        }

        var loiter = new LoiterSkill(Location, CharactersFile.DefaultIdleRadius, LoiterSkill.DefaultDuration);

        if (Routine == null)
        {
            AttachRoutine(new Routine([loiter]));
        }
        else
        {
            Routine.Replace([loiter]);
        }
    }

    public void BindIdentity(
        CharacterDefinition definition,
        Map map,
        string uniqueId,
        string facetName,
        FacetContent facet,
        bool alreadyInWorld,
        GuildType side
    )
    {
        Definition = definition;
        FacetContent = facet;
        CharacterId = uniqueId;
        if (!alreadyInWorld || HomeSpawn == Point3D.Zero ||
            WorkSites.IsCopy(uniqueId) && WorkSites.NeedsNewCopyHome(HomeSpawn))
        {
            HomeSpawn = WorkSites.HomeFor(
                definition.Spawn,
                uniqueId,
                WorkSites.PrimaryWork(definition),
                side
            );
        }
        HomeFacet = string.IsNullOrWhiteSpace(facetName)
            ? map?.Name ?? CharactersFile.DefaultMapName
            : facetName;
        HomeMapName = HomeFacet;
        ReturnAfterDeath = definition.ReturnAfterDeath ?? CharactersFile.DefaultReturnAfterDeath;
        Build = BuildPresets.Resolve(definition.Build);
        var resolved = HomeSpotRules.Resolve(NearestBankTo(HomeSpawn), HomeSpawn, LeashRadius);

        // A home far from any bank is the spawn itself; one on a ledge or behind a cliff
        // moves to a spot a person can stand on and walk out of.
        _homeSpot = resolved == HomeSpawn ? HomeSpotRules.OpenSpot(map, resolved) : resolved;
        _homeCorner = HomeSpotRules.CornerFor(
            _homeSpot,
            uniqueId,
            map,
            HomeSpotRules.TownPlaces(NavWorld.DestinationsFor(HomeFacet), NavWorld.GraphFor(HomeFacet), _homeSpot)
        );
    }

    private Point3D NearestBankTo(Point3D spawn) =>
        NavWorld.DestinationsFor(HomeFacet)?.Nearest(spawn, DestinationKind.Bank)?.Arrival ?? Point3D.Zero;

    private HuntHome _huntHome;
    private int _huntHomePowerBand;
    private string _huntHomePrey;
    private int _dungeonRuns;

    /// <summary>
    /// The hunt place and dungeon hall for this character, picked for its power with its pets
    /// and party (<see cref="DungeonGround.FightingPower"/>). The catalog scan is kept until
    /// that power's band or its prey changes, the ground it named ran dry, or a dungeon run
    /// ended (<see cref="RollDungeonAgain"/>).
    /// </summary>
    public HuntHome HuntHomeNow()
    {
        var prey = CurrentAmbition().Prey;
        var power = DungeonGround.FightingPower(this);
        var band = power / HuntGround.PowerBand;

        if (_huntHome != null && band == _huntHomePowerBand && _huntHomePrey == prey && _huntHome.Home == HomeSpot &&
            !GroundRanDry(_huntHome.Ground))
        {
            return _huntHome;
        }

        var leash = HomeLeash.ConfiguredRadius();
        var catalog = NavWorld.DestinationsFor(HomeFacet);
        var seed = unchecked((int)Serial.Value);
        var homeMap = Map.Parse(HomeFacet);
        var tier = PersonProfile.Tier;
        _huntHomePowerBand = band;
        _huntHomePrey = prey;
        _huntHome = new HuntHome(
            HomeSpot,
            leash,
            HuntGround.Pick(
                catalog,
                HomeSpot,
                leash,
                power,
                tier,
                prey,
                seed,
                Navigation.Generation.CreatureStatsLookup.IsLandEnemy,
                ground => HuntPrey.StateOf(this, homeMap, ground, Core.Now)
            ),
            DungeonGround.PickFor(this, power, DungeonGround.TripSeed(seed, _dungeonRuns), prey),
            DungeonGround.KnowsDoors(homeMap),
            power,
            tier,
            catalog
        );
        return _huntHome;
    }

    /// <summary>A dungeon run ended: the next one rolls its dungeon again.</summary>
    public void RollDungeonAgain()
    {
        _dungeonRuns++;
        _huntHome = null;
    }

    private bool GroundRanDry(Destination ground) =>
        ground != null && DryGrounds.IsDry(Serial.Value, new Point2D(ground.Arrival.X, ground.Arrival.Y), Core.Now);

    public Ambition CurrentAmbition()
    {
        if (!Enum.TryParse(AmbitionKind, ignoreCase: true, out Behaviour.AmbitionKind kind))
        {
            kind = Behaviour.AmbitionKind.None;
        }

        return new Ambition(kind, AmbitionTarget, AmbitionGoal, AmbitionProgress);
    }

    public void EnsureLife()
    {
        var raw = AmbitionRules.SeedFromBackground(Persona?.Background);
        var seed = AmbitionRules.FinalizeSeed(
            raw,
            SkillValue(raw.Target),
            Persona?.Background,
            unchecked((int)Serial.Value)
        );

        if (!string.IsNullOrWhiteSpace(AmbitionKind) && !AmbitionRules.ShouldReseed(CurrentAmbition(), seed))
        {
            TickAmbition();

            if (SosariaSettings.LogActivity && AmbitionProgress > 0)
            {
                logger.Information("{Name} ambition is now {Want}", Name, AmbitionRules.Describe(CurrentAmbition()));
            }

            return;
        }

        AmbitionKind = seed.Kind.ToString();
        AmbitionTarget = seed.Target;
        AmbitionGoal = seed.Goal;
        AmbitionProgress = seed.Progress;

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} wants {Want}", Name, AmbitionRules.Describe(seed));
        }
    }

    public void TickAmbition()
    {
        var current = CurrentAmbition();

        if (current.Kind == Behaviour.AmbitionKind.None || current.Goal <= 0)
        {
            return;
        }

        var progress = current.Kind switch
        {
            Behaviour.AmbitionKind.Gold => (BankBox?.GetAmount(typeof(Gold)) ?? 0) + (Backpack?.GetAmount(typeof(Gold)) ?? 0),
            Behaviour.AmbitionKind.Gear => GearScore.Of(this),
            Behaviour.AmbitionKind.Skill => SkillValue(current.Target),
            _ => current.Progress
        };

        var updated = AmbitionRules.WithProgress(current, progress);
        AmbitionProgress = updated.Progress;

        if (!AmbitionRules.IsComplete(updated))
        {
            if (updated.Progress != current.Progress)
            {
                NoteMusingEvent(MusingRules.AmbitionMoved(AmbitionRules.Describe(updated)));

                if (SosariaSettings.LogActivity)
                {
                    logger.Information("{Name} ambition is now {Want}", Name, AmbitionRules.Describe(updated));
                }
            }

            return;
        }

        TellNews(AmbitionRules.TalkLine(updated));

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} finished {Want}", Name, AmbitionRules.Describe(updated));
        }

        var next = AmbitionRules.NextAfterComplete(
            updated,
            unchecked((int)Serial.Value),
            Persona?.Background
        );
        AmbitionKind = next.Kind.ToString();
        AmbitionTarget = next.Target;
        AmbitionGoal = next.Goal;
        AmbitionProgress = next.Progress;

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} now wants {Want}", Name, AmbitionRules.Describe(next));
        }
    }

    /// <summary>When this rule clock (<see cref="RuleClock"/>) started, or default when it does not run.</summary>
    public DateTime ClockAt(string clock) => _ruleClocks.GetValueOrDefault(clock);

    /// <summary>Starts a rule clock at this UTC time; the save keeps it.</summary>
    public void StartClock(string clock, DateTime at) => _ruleClocks[clock] = at;

    public void StopClock(string clock) => _ruleClocks.Remove(clock);

    /// <summary>Stops every rule clock whose name starts with the prefix, except the ones kept.</summary>
    public void StopClocks(string prefix, List<string> keep)
    {
        List<string> spent = null;

        foreach (var clock in _ruleClocks.Keys)
        {
            if (clock.StartsWith(prefix, StringComparison.Ordinal) && keep?.Contains(clock) != true)
            {
                (spent ??= []).Add(clock);
            }
        }

        for (var i = 0; i < (spent?.Count ?? 0); i++)
        {
            _ruleClocks.Remove(spent[i]);
        }
    }

    /// <summary>A Hunt or Dungeon step began: a new trip, so its kill count starts again.</summary>
    public void BeginHuntTrip(DateTime now)
    {
        LastHuntAt = now;
        HuntKillsThisTrip = 0;
    }

    public void NoteKill(string creatureName)
    {
        HuntKillsThisTrip++;

        if (!string.IsNullOrWhiteSpace(creatureName))
        {
            NoteMusingEvent(MusingRules.Killed(creatureName));
        }

        var current = CurrentAmbition();

        if (current.Kind != Behaviour.AmbitionKind.Kills ||
            string.IsNullOrWhiteSpace(creatureName) ||
            string.IsNullOrWhiteSpace(current.Target))
        {
            return;
        }

        if (!creatureName.Contains(current.Target, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AmbitionProgress = current.Progress + 1;
        TickAmbition();
    }

    public void MarkPlace(string place)
    {
        if (!Memory.SawPlace(place))
        {
            return;
        }

        NoteMusingEvent(MusingRules.Arrived(place));

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} saw {Place}", Name, place);
        }

        var current = CurrentAmbition();

        if (current.Kind == Behaviour.AmbitionKind.Place &&
            !string.IsNullOrWhiteSpace(current.Target) &&
            place.Contains(current.Target, StringComparison.OrdinalIgnoreCase))
        {
            AmbitionProgress = current.Goal;
            TickAmbition();
        }
    }

    public void ApplyPk(bool isPk)
    {
        IsPk = isPk;
        Team = isPk ? SosariaCombat.PkTeam : SosariaCombat.Team;
        FightMode = isPk ? FightMode.Closest : FightMode.None;
    }

    public bool MayUseThisBank() => PkRules.MayBankAt(PkRules.IsRed(Kills), X, Y);

    /// <summary>
    /// The engine moves every player to Map.Internal when the world loads and waits for a
    /// client to log it back in. A character that stood in the world at save time returns
    /// to the same spot, as a player would after a server restart.
    /// </summary>
    private void ReturnFromLoad()
    {
        // A world waiting for First Time Setup has no bankers or gates yet: the population
        // clock logs saved characters back in after the setup instead.
        if (Deleted || WorldSetup.Waiting || Map != Map.Internal || LogoutMap == null || LogoutMap == Map.Internal)
        {
            return;
        }

        var map = LogoutMap;
        LogoutMap = null;
        MoveToWorld(LogoutLocation, map);
    }

    /// <summary>
    /// Logs the character back in: inside the dungeon it logged out in, at a quiet spot on
    /// that floor, as a player's client puts it back where it stood; anywhere else at its
    /// login spot in its home town.
    /// </summary>
    public void ResumeSession()
    {
        var map = Map.Parse(string.IsNullOrEmpty(HomeFacet) ? CharactersFile.DefaultMapName : HomeFacet);
        var location = LoginSpot;
        var dungeon = DungeonReturn.SpotFor(map, LogoutLocation);

        if (dungeon is { } underground)
        {
            location = underground.Spot;
        }
        else if (map != null && map != Map.Internal)
        {
            if (!map.CanSpawnMobile(location))
            {
                location = new Point3D(location.X, location.Y, map.GetAverageZ(location.X, location.Y));
            }

            location = RescuedSpawn(map, location);
            location = MarkedSpawn(map, location);
        }

        MoveToWorld(location, map);

        if (dungeon is { } floor && SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Line}",
                DungeonReturnRules.LoggedBackInLine(Name, floor.Floor.Dungeon, floor.Floor.Level)
            );
        }
        Pulse.Start();

        // Saves from before hand-layer checks can still wear a weapon in each hand.
        GearEquip.FreeBusyHands(this);

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} resumed session at {Location} on {Map}", Name, Location, Map);
        }
    }

    /// <summary>
    /// A saved spawn inside a walled yard or sealed room is standable but has no
    /// walk to the world: every plan from it fails. Resume on the closest node the
    /// walk engine proves can reach the character's home spot.
    /// </summary>
    private Point3D RescuedSpawn(Map map, Point3D location)
    {
        var node = RoadStart(map, location, onlyWhenSealed: true);

        if (node == null)
        {
            return location;
        }

        logger.Information("{Name} was sealed in at {Location}; resumed at {Node} instead", Name, location, node.Location);
        return node.Location;
    }

    /// <summary>
    /// A marked character — murderer or criminal — placed inside a guarded region
    /// is a guard kill on the next sweep. Resume on the nearest routable node
    /// outside every guarded region instead.
    /// </summary>
    private Point3D MarkedSpawn(Map map, Point3D location)
    {
        if (map == null || map == Map.Internal)
        {
            return location;
        }

        var region = Region.Find(location, map)?.GetRegion<Server.Regions.GuardedRegion>();

        if (region == null || region.IsDisabled() || !region.IsGuardCandidate(this))
        {
            return location;
        }

        bool OutsideGuards(NavNode node)
        {
            var spot = Region.Find(node.Location, map)?.GetRegion<Server.Regions.GuardedRegion>();
            return spot == null || spot.IsDisabled();
        }

        var node = RoadStart(map, location, onlyWhenSealed: false, OutsideGuards);

        if (node == null)
        {
            return location;
        }

        logger.Information(
            "{Name} is marked at {Location}; resumed at {Node} outside the guarded zone instead",
            Name,
            location,
            node.Location
        );
        return node.Location;
    }

    /// <summary>
    /// Stall logs count as trapped only while they cluster on one tile: a walker who
    /// stalls, moves, and stalls again is on bad ground somewhere ahead, not sealed
    /// in. Repeated stalls from one spot mean the character cannot leave the tile —
    /// the same sealed-pocket case <see cref="TryMaroonedRescue"/> fixes for a failed
    /// home walk, raised here for any stalled skill.
    /// </summary>
    internal void NoteTravelStall()
    {
        _sameTileStalls = HomeLeash.SameSpotStalls(_sameTileStalls, Location, _lastStallAt);
        _lastStallAt = Location;

        if (!HomeLeash.MayCheckMarooned(_sameTileStalls, Core.Now, _maroonedCheckAt))
        {
            return;
        }

        _maroonedCheckAt = Core.Now + HomeLeash.MaroonedCheckCooldown;
        _sameTileStalls = 0;
        TryMaroonedRescue();
    }

    /// <summary>
    /// Repeated home-walk failures from one tile mean the ground itself is the
    /// trap — a pocket or ledge the router cannot leave, the same failure
    /// <see cref="RescuedSpawn"/> fixes at spawn. When no route out exists at
    /// all, move the character to the nearest node the walk engine proves can
    /// reach the road, like the stuck help option a live player had. Returns
    /// false when a route out exists: then the planner, not the ground, is the
    /// problem.
    /// </summary>
    internal bool TryMaroonedRescue()
    {
        var map = Map;

        // The search runs on the world thread and is paid from the planning budget; over it,
        // no rescue this time, and the caller's own fallback runs.
        if (IsGhost || !PlanBudget.TryEnter())
        {
            return false;
        }

        var started = PlanBudget.Start();
        var node = RoadStart(map, Location, onlyWhenSealed: true);
        PlanBudget.Charge(started);

        if (node == null)
        {
            return false;
        }

        logger.Information("{Name} was stuck at {Location}; found the road again at {Node}", Name, Location, node.Location);
        MoveToWorld(node.Location, map);
        return true;
    }

    /// <summary>
    /// Home walks have failed inside a dungeon, or a place dropped from this world
    /// (<see cref="DungeonGround.PlacesToLeave(Server.Map)"/>), for five minutes, and neither Recall
    /// nor a pad got the character out: stand it at the door outside, off the pad, like the
    /// stuck help a player called. False outside such a place, for a ghost, or
    /// when the world records no door for it.
    /// </summary>
    internal bool TryDungeonDoorRescue()
    {
        var map = Map;

        if (IsGhost || map == null || map == Map.Internal)
        {
            return false;
        }

        var dungeon = DungeonGround.PlacesToLeave(map)(Location);
        var door = DungeonGround.DoorOutOf(map, Location);

        if (door == Point3D.Zero)
        {
            return false;
        }

        var spot = DungeonGround.StandBy(NavWorld.GraphFor(HomeFacet), door);
        logger.Information("{Line}", DungeonEscapeRules.DoorLine(Name, dungeon, Location, spot));
        MoveToWorld(spot, map);
        DungeonTrouble = default;
        FailedHomeWalks = 0;
        return true;
    }

    /// <summary>
    /// The nearest node this character can be put back on the road at, judged on the
    /// real floors of <paramref name="map"/> the way the engine walks them, so a bridge
    /// or a raised floor counts as ground. Null when there is no graph or no such node,
    /// or, with <paramref name="onlyWhenSealed"/>, when a plan already starts from
    /// <paramref name="from"/>.
    /// </summary>
    private NavNode RoadStart(Map map, Point3D from, bool onlyWhenSealed, Func<NavNode, bool> nodeFilter = null)
    {
        var graph = NavWorld.GraphFor(HomeFacet);

        if (graph == null || map == null || map == Map.Internal)
        {
            return null;
        }

        var walker = Standable.Walker(map);
        bool Indoor(int x, int y, int z) => IndoorTiles.IsBuilding(map, x, y, z);

        if (onlyWhenSealed && Traveler.HasClearStart(graph, from, walker, Indoor))
        {
            return null;
        }

        return Traveler.NearestRoutableStart(
            graph,
            from,
            HomeSpot,
            NodeHeight.Stands(walker.FloorNear),
            walker,
            Indoor,
            nodeFilter
        );
    }

    public override bool CheckTarget(Mobile from, Target targ, object targeted)
    {
        // A bandage or spell aimed at this ghost is watched; the raise happens only when
        // the engine's own attempt succeeds.
        if (IsGhost && targeted == this)
        {
            ResurrectWatch.OnTargeted(this, from, targ);
        }

        return base.CheckTarget(from, targ, targeted);
    }

    public override void OnAfterResurrect()
    {
        base.OnAfterResurrect();
        FinishRestore(_restoreReason ?? EngineResurrectReason);
        _restoreReason = null;
    }

    internal void MarkDeath(DateTime at) => _lastDeathAt = at;

    /// <summary>Stands the ghost up; a person who raised it is thanked in its bond and in a rescue.</summary>
    public void RestoreLife(string reason, Mobile helper = null)
    {
        if (!IsGhost)
        {
            return;
        }

        _restoreReason = reason;
        Resurrect();

        if (IsGhost)
        {
            _restoreReason = null;
            return;
        }

        // The same cost a player accepts in the resurrect window: sparkle, murderer
        // stat loss, and a tenth of the fame.
        PlaySound(GhostRules.ResurrectSound);
        FixedEffect(GhostRules.ResurrectEffect, ResurrectEffectSpeed, GhostRules.ResurrectEffectDuration);
        ResurrectGump.TryGiveStatLoss(this);

        if (Fame > 0)
        {
            Titles.AwardFame(this, -Fame / FameLossDivisor, true);
        }

        if (helper != null && helper != this)
        {
            Memory.ShiftBond(helper, BondRules.HealBonus, BondRules.HealedReason);
            AdventureTracker.Shared.Rescued(helper, this, true, Core.Now);
        }
    }

    // A heal from another person that kept this one up, low, in a fight is a rescue.
    public override void OnHeal(ref int amount, Mobile from)
    {
        base.OnHeal(ref amount, from);

        if (amount > 0 && from != null && from != this &&
            AdventureRules.HealIsRescue(Combatant is { Deleted: false, Alive: true }, Hits, HitsMax))
        {
            AdventureTracker.Shared.Rescued(from, this, false, Core.Now);
        }
    }

    /// <summary>The last resort for a ghost with no way back: it stands up at home. Logged with why.</summary>
    public void FallbackFromGhost(string why)
    {
        logger.Warning(
            "{Name} ghost was stuck {Minutes} minutes ({Why}); falling back to spawn return",
            Name,
            (int)(Core.Now - _ghostSince).TotalMinutes,
            why
        );
        TryReclaimCorpse();
        LogIfPoorer();
        RestoreLife(FallbackReason);
        ScheduleReturn(Core.Now);
        Internalize();
    }

    /// <summary>
    /// The body the next corpse run goes for: this death's, unless an earlier body still lies in
    /// the world with more of the gear (<see cref="CorpseReclaim.KeepsEarlierBody"/>).
    /// </summary>
    private void TrackBody(Container body)
    {
        if (OwnCorpse is { } earlier && body is Corpse newer &&
            CorpseReclaim.KeepsEarlierBody(WornLeftIn(earlier), earlier.Items.Count, WornLeftIn(newer), newer.Items.Count))
        {
            return;
        }

        _corpseSerial = body?.Serial.Value ?? 0;
        _corpseLocation = body?.Location ?? Location;
    }

    /// <summary>The pieces worn at death that still lie in the body: a looter takes them out.</summary>
    private static int WornLeftIn(Corpse body)
    {
        var left = 0;

        foreach (var item in body.EquipItems ?? [])
        {
            if (item is { Deleted: false } && item.Parent == body)
            {
                left++;
            }
        }

        return left;
    }

    /// <summary>The body the corpse run goes for while it lies in the world as this person's own, or null.</summary>
    public Corpse OwnCorpse =>
        World.FindItem((Serial)_corpseSerial) is Corpse { Deleted: false } corpse && corpse.Owner == this ? corpse : null;

    public CorpseReclaimResult TryReclaimCorpse()
    {
        var corpse = World.FindItem((Serial)_corpseSerial) as Corpse;
        var exists = corpse is { Deleted: false };
        var ownerIsSelf = exists && corpse.Owner == this;
        var remaining = exists ? corpse.Items.Count : 0;
        var decision = CorpseReclaim.Decide(exists, ownerIsSelf, remaining);

        if (!CorpseReclaim.CanLoot(decision) || corpse == null)
        {
            if (SosariaSettings.LogActivity)
            {
                logger.Information("{Name} found no corpse to reclaim ({Result})", Name, decision);
            }

            LogIfPoorer();
            return decision;
        }

        var moving = new List<Item>(corpse.Items);

        for (var i = 0; i < moving.Count; i++)
        {
            var item = moving[i];

            if (item == null || item.Deleted)
            {
                continue;
            }

            if (IsCarryBag(item) && item is Container pack)
            {
                MovePackContents(pack, Backpack);
                pack.Delete();
                continue;
            }

            AddToBackpack(item);
        }

        FlattenNestedBackpacks();
        CorpseSanitizer.Clean(corpse);
        EnsureWorkerSupport(freshStart: false);
        var recovered = moving.Count;

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} recovered {Count} items from the corpse", Name, recovered);
        }

        LogIfPoorer();
        return CorpseReclaim.AfterLoot(corpse.Items.Count);
    }

    private static void MovePackContents(Container from, Container to)
    {
        if (from == null || to == null)
        {
            return;
        }

        var moving = new List<Item>(from.Items);

        for (var i = 0; i < moving.Count; i++)
        {
            var item = moving[i];

            if (item == null || item.Deleted)
            {
                continue;
            }

            if (IsCarryBag(item) && item is Container nested)
            {
                MovePackContents(nested, to);
                continue;
            }

            to.AddItem(item);
        }
    }

    public void FlattenNestedBackpacks() => FlattenExtraBags(Backpack);

    public static void FlattenExtraBags(Container root)
    {
        if (root == null)
        {
            return;
        }

        var extra = new List<Container>();
        CollectExtraBags(root, root, extra);

        for (var i = extra.Count - 1; i >= 0; i--)
        {
            var pack = extra[i];

            if (pack == null || pack.Deleted)
            {
                continue;
            }

            MovePackContents(pack, root);
            pack.Delete();
        }
    }

    /// <summary>A bag a character carries things in. A character never owns two: nested packs are emptied and dropped.</summary>
    public static bool IsCarryBag(Item item) => item is Server.Items.Backpack or Bag or Pouch;

    private static void CollectExtraBags(Container root, Container current, List<Container> extra)
    {
        var items = new List<Item>(current.Items);

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is not Container pack || pack == root || !IsCarryBag(pack))
            {
                continue;
            }

            extra.Add(pack);
            CollectExtraBags(root, pack, extra);
        }
    }

    private void LogIfPoorer()
    {
        if (!SosariaSettings.LogActivity)
        {
            return;
        }

        var goldNow = (Backpack?.GetAmount(typeof(Gold)) ?? 0) + (BankBox?.GetAmount(typeof(Gold)) ?? 0);

        if (goldNow < _goldAtDeath)
        {
            logger.Information(
                "{Name} came back poorer ({GoldNow} of {GoldAtDeath} gold)",
                Name,
                goldNow,
                _goldAtDeath
            );
        }
    }

    public bool NotePowerGrowth(int power)
    {
        if (!PowerRating.CrossedThreshold(_lastLoggedPower, power))
        {
            return false;
        }

        _lastLoggedPower = power;
        return true;
    }

    public void EnsureFightingForm()
    {
        if (IsGhost || Build == null)
        {
            return;
        }

        var oldMax = HitsMax;

        if (RawStr < Build.Strength)
        {
            SetStr(Build.Strength, MaxStrength);
        }

        if (RawDex < Build.Dexterity)
        {
            SetDex(Build.Dexterity, MaxDexterity);
        }

        if (RawInt < Build.Intelligence)
        {
            SetInt(Build.Intelligence, MaxIntelligence);
        }

        foreach (var pair in Build.Skills)
        {
            if (Enum.TryParse(pair.Key, true, out SkillName skill) && Skills[skill].Base < pair.Value)
            {
                SetSkill(skill, pair.Value);
            }
        }

        EnsureRoleGear();

        if (HitsMax > oldMax)
        {
            Hits = Math.Min(HitsMax, Hits + HitsMax - oldMax);
        }
    }

    public void ApplyFreshBuild()
    {
        if (!KitOnceRules.ShouldApplyFreshBuild(_careerStarted))
        {
            return;
        }

        Build ??= BuildPresets.WorkerDefault;
        SetStr(Build.Strength);
        SetDex(Build.Dexterity);
        SetInt(Build.Intelligence);

        foreach (var pair in Build.Skills)
        {
            if (Enum.TryParse(pair.Key, true, out SkillName skill))
            {
                SetSkill(skill, pair.Value);
            }
        }

        EnsureRoleGear();
    }

    public void EnterHuntStance()
    {
        IsHunting = true;
        SaidCombatLineThisHunt = false;
        FightMode = FightMode.Evil;
        RangePerception = SosariaCombat.HuntRangePerception;
        SetRunPace();
    }

    public void RestoreTownStance()
    {
        var foe = Combatant is { Deleted: false, Alive: true } && Combatant.Map == Map;

        if (!DefendRules.MayRestoreTownStance(foe))
        {
            return;
        }

        IsHunting = false;
        FightMode = FightMode.None;
        RangePerception = DefaultRangePerception;
        Combatant = null;
        Warmode = false;
        SetWalkPace();
    }

    /// <summary>The foe this character last stood down from, for the re-engage grace.</summary>
    public Serial LastStandDownFoe { get; set; }

    /// <summary>When this character last stood down from a fight.</summary>
    public DateTime LastStandDownAt { get; set; }

    /// <summary>
    /// The person this character last ran from, for its own grace. Kept apart from the
    /// stand-down foe: a run wrote over it and ended the guard-line grace for the foe before.
    /// </summary>
    public Serial LastRanFrom { get; set; }

    /// <summary>When this character last ran from a person.</summary>
    public DateTime LastRanFromAt { get; set; }

    /// <summary>When this character last stood down at the guard line or a place of peace, from any foe.</summary>
    public DateTime LastLineStopAt { get; set; }

    /// <summary>
    /// True inside the grace after a stop at the line: a new foe at a crowded gate is no reason to
    /// draw and stop again (<see cref="FactionWar.StoppedAtLineLately"/>).
    /// </summary>
    public bool StoppedAtLineLately => GuardLineRules.InStandDownGrace(LastLineStopAt, Core.Now);

    /// <summary>True inside the grace for the foe this character stood down from at the guard line.</summary>
    public bool StoodDownFrom(Mobile foe) =>
        foe != null && foe.Serial == LastStandDownFoe && GuardLineRules.InStandDownGrace(LastStandDownAt, Core.Now);

    /// <summary>True inside the grace for the person this character last ran from.</summary>
    public bool RanFromLately(Mobile foe) =>
        foe != null && foe.Serial == LastRanFrom && GuardLineRules.InStandDownGrace(LastRanFromAt, Core.Now);

    /// <summary>
    /// A foe this character leaves be for now: rallies, watches and hunts do not hand it the
    /// fight it stood down from or ran from.
    /// </summary>
    public bool LeavesBe(Mobile foe) => StoodDownFrom(foe) || RanFromLately(foe);

    /// <summary>
    /// Ends a fight without touching hunt stance. FightMode must disarm too:
    /// an armed Closest or Evil lets the AI re-acquire the adjacent foe on the
    /// next think, and a guard-line stand-down becomes an endless loop of two
    /// fighters dropping and re-picking the same fight.
    /// </summary>
    public void StandDown()
    {
        LastStandDownFoe = Combatant?.Serial ?? Serial.Zero;
        LastStandDownAt = Core.Now;
        Combatant = null;
        Warmode = false;
        FightMode = FightMode.None;
        FocusMob = null;
        Motor.Action = CharacterAction.Wander;
    }

    public void SetRunPace()
    {
        _thinkSeconds = ActiveThinkSeconds;
        Motor.Running = true;
    }

    /// <summary>Runs from the current combatant until <paramref name="duration"/> passes.</summary>
    public void BeginFlee(TimeSpan duration)
    {
        _fleeUntil = Core.Now + duration;
        Motor.Action = CharacterAction.Flee;
        SetRunPace();
    }

    /// <summary>Keeps a running flee going for at least <paramref name="atLeast"/> more; a shorter wait leaves it as it is.</summary>
    public void KeepFleeing(TimeSpan atLeast)
    {
        var until = Core.Now + atLeast;

        if (Motor.Action == CharacterAction.Flee && until > _fleeUntil)
        {
            _fleeUntil = until;
        }
    }

    public void StopFlee()
    {
        _fleeUntil = default;

        if (Motor.Action == CharacterAction.Flee)
        {
            Motor.Action = CharacterAction.Wander;
        }
    }

    /// <summary>True while a flee runs. An expired flee ends here.</summary>
    public bool CheckFlee()
    {
        if (_fleeUntil == default)
        {
            return false;
        }

        if (Core.Now < _fleeUntil)
        {
            return true;
        }

        StopFlee();
        return false;
    }

    public void TrySayCombatLine()
    {
        if (SaidCombatLineThisHunt || !IsHunting)
        {
            return;
        }

        var line = Persona?.PickCombatLine();

        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        SaidCombatLineThisHunt = true;
        SpeakAloud(line);
    }

    public void BeginDecide()
    {
        DecideResolved = false;
        Brain.RequestDecide(this);
    }

    /// <param name="source">The activity log's source; null says "model" or "fallback" from <paramref name="fromModel"/>.</param>
    public void ApplyChosenRoutine(string id, bool fromModel, string source = null)
    {
        DecideResolved = true;
        var catalog = NavWorld.DestinationsFor(HomeFacet);
        var result = ScoreNow();

        if (result == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(id))
        {
            for (var i = 0; i < result.Ranked.Count; i++)
            {
                var scored = result.Ranked[i];

                if (scored.Score <= ActionScorer.IneligibleScore)
                {
                    continue;
                }

                if (scored.RoutineId.Equals(id, StringComparison.OrdinalIgnoreCase) ||
                    scored.Id.Value.Equals(id, StringComparison.OrdinalIgnoreCase))
                {
                    CommitScored(scored, result, fromModel, catalog, source: source);
                    return;
                }
            }
        }

        CommitScored(result.Winner, result, fromModel, catalog, source: source);
    }

    /// <summary>True when the last skill ended and nothing new has started: a next-job pick is due.</summary>
    public bool AwaitsNextSkill => Routine == null || Routine.NeedsNext;

    /// <summary>
    /// Runs the job Jev picked. The ranking is taken again, so a pick the rules no longer allow
    /// falls back to the scorer's winner; a null id is the scorer's winner with Jev's reason.
    /// </summary>
    public void CommitJobChoice(string actionId, string source)
    {
        var result = ScoreNow();

        if (result == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(actionId))
        {
            for (var i = 0; i < result.Ranked.Count; i++)
            {
                var scored = result.Ranked[i];

                if (ActionScorer.IsEligible(scored) &&
                    scored.Id.Value.Equals(actionId, StringComparison.OrdinalIgnoreCase))
                {
                    CommitScored(scored, result, fromModel: false, NavWorld.DestinationsFor(HomeFacet), source: source);
                    return;
                }
            }

            source = JevDecisionRules.StaleSource;
        }

        CommitFallbackScore(result, source);
    }

    public ScoreResult ScoreNow()
    {
        if (Definition == null)
        {
            return null;
        }

        var situation = BuildSituation();
        var seed = JobSeed(Core.Now);
        var goal = GoalRules.Pick(situation, seed);
        var catalog = NavWorld.DestinationsFor(HomeFacet);
        var result = GoalLoop.Score(Definition, situation, goal, seed, catalog, CurrentPlan, HuntHomeNow());
        LastScore = result;
        return result;
    }

    /// <summary>
    /// Picks and starts the next job. A character waiting on <see cref="Map.Internal"/> is not
    /// scored: no town holds it there, so every town job began with a walk home. Two hundred
    /// people picked "go home" at boot that way. The pulse scores it once it stands in the world.
    /// </summary>
    private DateTime _remountTryAt;

    public void ScoreAndCommit()
    {
        if (IsGhost || Map == null || Map == Map.Internal)
        {
            return;
        }

        var power = CharacterPower.For(this);

        if (NotePowerGrowth(power) && SosariaSettings.LogActivity)
        {
            logger.Information("{Name} power is now {Power}", Name, power);
        }

        var now = Core.Now;
        var situation = BuildSituation();
        Planning.ResumeIfBlocked();

        if (situation.MustFlee || situation.IsGhost)
        {
            Brain.ForgetJobChoice(this);
            Planning.Interrupt(StepProof.DetailInterrupted, now);
            SaveModelPlan();
            CommitFallbackScore();
            return;
        }

        if (MountRules.RemountsFirst(Mounted, situation.CanMount, Combatant != null, now >= _remountTryAt))
        {
            _remountTryAt = now + MountRules.RemountRetry;
            WorldPlay.StartWork(this, new MountSkill());
            return;
        }

        if (PetKeeper.StartsStableErrand(this))
        {
            return;
        }

        if (!Planning.ScorerMayReplace(situation, now))
        {
            var catalog = ActionCatalog.From(Definition, NavWorld.DestinationsFor(HomeFacet), HuntHomeNow());
            var work = Planning.Next(catalog, situation, now);
            var unavailable = work.FromModel && work.Candidate != null
                ? ActionScorer.Unavailable(work.Candidate, situation, NavWorld.DestinationsFor(HomeFacet))
                : null;

            if (work.FromModel && work.Candidate != null && unavailable == null)
            {
                CommitPlanWork(work);
                return;
            }

            if (work.FromModel)
            {
                var facts = CaptureFacts();
                var observed = Planning.FinishStep(
                    StepProof.Observe(work.SkillKind, facts, facts, skillDone: false, interrupted: false, unavailable: true),
                    now
                );
                SaveModelPlan();

                if (observed.RequestRevision)
                {
                    Brain.RequestPlan(this, PlanTrigger.WorldBlocked);
                }
            }
        }
        else
        {
            // Jev picks the next job when it is the decision route. The skill has ended, so the
            // character stands until the answer comes or the wait runs out.
            if (Brain.WaitsForJobChoice(this, now))
            {
                return;
            }

            var scored = ScoreNow();

            if (Brain.AskJobChoice(this, scored, situation, now, out var source))
            {
                return;
            }

            Brain.RequestPlan(this, PlanTrigger.NoPlan);
            CommitFallbackScore(scored, source);
            return;
        }

        CommitFallbackScore();
    }

    /// <param name="result">A ranking already taken this tick, or null to take one.</param>
    /// <param name="source">The activity log's source, or null for a plain fallback.</param>
    private void CommitFallbackScore(ScoreResult result = null, string source = null)
    {
        result ??= ScoreNow();

        if (result == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(result.Winner.Id.Value))
        {
            Planning.Diag.NoteFallback("idle");
            CommitIdleFallback(result);
            return;
        }

        Planning.Diag.NoteFallback(PlanControl.FallbackNoPlan);
        CommitScored(result.Winner, result, fromModel: false, NavWorld.DestinationsFor(HomeFacet), source: source);
    }

    private void CommitPlanWork(PlanWork work)
    {
        var catalog = NavWorld.DestinationsFor(HomeFacet);
        var scored = new ScoredAction(work.Candidate.Id, work.Candidate.SkillKind, work.Candidate.RoutineId, GoalPlanRules.PlanBoost, work.Why);
        var result = new ScoreResult
        {
            Goal = new Goal(GoalKind.Work, work.SkillKind),
            Ranked = [scored],
            Winner = scored,
            WinnerReason = work.Why,
            Plan = CurrentPlan
        };
        FactsBeforeStep = CaptureFacts();
        FoughtThisStep = false;
        CommitScored(scored, result, fromModel: true, catalog, work.Candidate);
        SaveModelPlan();
    }

    // Every action the rules forbid: wander where the character stands until the next score.
    private void CommitIdleFallback(ScoreResult result)
    {
        LastScore = result;
        DecideResolved = true;

        if (Routine != null &&
            FleeRules.ShouldReuseIdle(Routine.CurrentSkill?.Name, Routine.WasAborted) &&
            Routine.ContinueAfterAbort())
        {
            return;
        }

        // A wander whose walks to its spot keep failing strolls where it stands until its rest runs out.
        Skill fallback = IdleFallbackRules.FleesInstead(MustRunFrom(HuntSkill.SightThreat(this)), MayStillFlee())
            ? new FleeSkill()
            : new IdleWanderSkill(
                Location,
                CharactersFile.DefaultIdleRadius,
                CharactersFile.DefaultIdleDuration,
                aroundHere: RestsSkill(SkillKinds.IdleWander)
            );

        if (Routine == null)
        {
            AttachRoutine(new Routine([fallback]));
        }
        else
        {
            Routine.Replace([fallback], keepFailPause: true);
        }
    }

    /// <summary>
    /// The murder report a lawful fighter would ride to, or null: never a stale one, one enough
    /// blues ride to already, one in the reds' town, beyond its leash, at a spot it just failed to reach, or where it just ran from a threat
    /// (<see cref="ConflictRules.BlueRides"/>).
    /// </summary>
    public ShardEvent PkReportToAnswer()
    {
        var journal = SosariaSettings.Journal;

        if (journal == null)
        {
            return null;
        }

        var from = Location;
        var leash = HomeLeash.ConfiguredRadius();
        var unreachable = Memory.Unreachable.Active(Core.Now);
        var fledFrom = Memory.Danger.Active(Core.Now);
        var now = Core.Now;

        return journal.FindReport(
            Name,
            from,
            now,
            ShardEventType.Pk,
            HomeFacet,
            accepts: report => ConflictRules.BlueRides(report, from, leash, unreachable, fledFrom, now)
        );
    }

    /// <summary>
    /// True when this character must run from the threat in sight. A worker runs from any
    /// real threat. A fighter runs when the threat is more than it can take.
    /// </summary>
    public bool MustRunFrom(int threat)
    {
        var hits = HitsMax <= 0 ? 1 : (double)Hits / HitsMax;
        var multiple = SosariaSettings.Characters?.Career?.ThreatMultiple ?? ThreatRating.DefaultThreatMultiple;
        var intolerable = DangerRules.IsIntolerable(
            CharacterPower.For(this),
            threat,
            hits,
            hasHealing: HuntSkill.HasHealing(this),
            Behaviour.Party.AlliesPower(this),
            multiple
        );
        return FleeRules.MustRun(UnderGuards(this), Build?.Role ?? CharacterRole.Worker, threat, intolerable);
    }

    /// <summary>
    /// False once the character ran often enough, or its flee failed often enough, that the
    /// scorer offers no flee. A step that stops at a threat then would stop for good.
    /// </summary>
    public bool MayStillFlee() =>
        !FleeRules.FledEnough(Memory.Danger.RecentRuns(Core.Now)) &&
        !FleeRules.FleeIsBlocked(FailedRoutineId, FailedRoutineCount) &&
        !RestsSkill(SkillKinds.Flee);

    /// <summary>
    /// Places the router found no way to, plus every bank this person can neither walk to nor
    /// recall to from here: 30 town trips failed for banks on islands with no road (Serpent's
    /// Hold, Delucia, Papua), while a mage with a rune still takes its trip. A bank whose trip
    /// this person just gave up for danger on the road rests too (<see cref="TownTripRules.GivenUpRest"/>).
    /// </summary>
    private IReadOnlyList<Point3D> UnreachableGoals()
    {
        var now = Core.Now;

        // A walk that failed the same way three times at one goal rests for that goal too.
        var found = new List<Point3D>(Memory.Unreachable.Active(now));
        found.AddRange(JobTargetRest.RestingSpots(this, SkillKinds.GoTo, now));
        var banks = NavWorld.DestinationsFor(HomeFacet)?.All;

        if (banks == null)
        {
            return found;
        }

        List<Point3D> goals = null;

        foreach (var bank in banks)
        {
            if (bank == null || bank.Arrival == Point3D.Zero ||
                !string.Equals(bank.Kind, TownTripRules.BankKind, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var givenUp = TownTripRules.RestsAfterGivingUp(ClockAt(RuleClock.TownTripGivenUp(bank.Arrival)), now);

            if (!givenUp && (RedGangReach.Walks(this, Location, bank.Arrival) || RedGangReach.Recalls(this, bank.Arrival)))
            {
                continue;
            }

            goals ??= [.. found];
            goals.Add(bank.Arrival);
        }

        return goals ?? found;
    }

    public Situation BuildSituation()
    {
        var pack = Backpack;

        // Goods count only when a live shop in reach buys them, by its own buy list: a
        // Magincia seller with loot no shop there bought walked to the smith marker and
        // failed 153 times. The pack beast's load counts as the pack does.
        var (hasGoods, hasLoot) = SaleGoods.BuyableInReach(this);

        var needs = Brain.SnapshotOf(this);
        var threat = HuntSkill.SightThreat(this);
        var recentRuns = Memory.Danger.RecentRuns(Core.Now);
        var mustFlee = MustRunFrom(threat);

        // A threat escaped by walking home, not by Flee, still owes the map its
        // mark: without it the next errand routes straight back to the hostile.
        // A threat that stays in sight is one run, not one per score tick — counting
        // it each time pins the character to GoHome forever.
        if (mustFlee && HuntSkill.StrongestHostile(this) is { Deleted: false } hostile)
        {
            Memory.Danger.NoteSighting(hostile.Location, Core.Now);
        }

        var dangerSpots = Memory.Danger.Active(Core.Now);
        var gold = (Backpack?.GetAmount(typeof(Gold)) ?? 0) + (BankBox?.GetAmount(typeof(Gold)) ?? 0);
        var ownedMount = Mounted ? null : OwnedMounts.Nearest(this, MountRules.SearchTiles);
        var houseGold = SosariaSettings.Characters?.Career?.HouseGold ?? CareerSettings.DefaultHouseGold;
        return new Situation
        {
            Needs = needs,
            Location = Location,
            InTownRegion = Region?.GetRegion<TownRegion>() != null,
            LastWalkFailed = LastWalkFailed,
            IsGhost = IsGhost,
            InCombat = Combatant != null,
            Role = Build?.Role ?? CharacterRole.Worker,
            HasHarvestGoods = hasGoods,
            HasLootGoods = hasLoot,
            CurrentActionId = ActiveActionId,
            RecentSkillKinds = Memory.Working.RecentSkillKinds,
            CurrentGoal = new Goal(
                Enum.TryParse(ActiveGoalKind, out GoalKind kind) ? kind : GoalKind.Work,
                ActiveGoalTarget
            ),
            MustFlee = mustFlee,
            KeepsAwayFromParty = FleeRules.KeepsAwayFromParty(recentRuns),
            RecentRuns = recentRuns,
            DangerSpots = dangerSpots,
            UnreachableGoals = UnreachableGoals(),
            BlockedActionId = FailedRoutineId,
            BlockedCount = FailedRoutineCount,
            BlockedSkillKinds = BlockedSkillKinds(Core.Now),
            UnmetSkillKinds = SkillReadiness.Unmet(this),
            Gold = gold,
            HasHouse = HouseOwned(),
            HasBoat = BoatOwned(),
            HasVendor = VendorOwned(),
            VendorErrand = PlayerVendorRules.HasErrand(this),
            CanBuyHouse = HouseRules.CanBuy(gold, houseGold, HouseOwned()) &&
                          CurrentAmbition().Kind != Behaviour.AmbitionKind.Gold,
            HouseDue = HouseVisit.Due(HouseRules.BySerial(HouseSerial)),
            Disposition = Disposition,
            BeyondLeash = HomeLeash.BeyondLeash(
                Location,
                HomeSpot,
                SosariaSettings.Characters?.Career?.LeashRadius ?? CareerSettings.DefaultLeashRadius
            ),
            DistanceFromHome = HomeLeash.DistanceFromHome(Location, HomeSpot),
            AvoidHuntPlace = DeathAvoid.ShouldAvoid(
                _deathPlace,
                _deathPlace,
                _lastDeathAt,
                Core.Now,
                SosariaSettings.Characters?.Career?.DeathAvoidHours ?? CareerSettings.DefaultDeathAvoidHours,
                _deathsAtPlace,
                CharacterPower.For(this),
                CharactersFile.GraveyardRequiredPower
            ) || FriendFellAtHunt(),
            CanUpgradeGear = UpgradeGearSkill.OfferFor(this) != null,
            OpenOrders = CraftOrders.Count,
            CanMount = !Mounted && MayClimbOn(ownedMount),
            CanBuyMount = MountBuyRules.MayBuy(Mounted, ownedMount != null, Followers, MountBuyRules.HorseSlots, FollowersMax) &&
                          MountBuyRules.CanAfford(gold),
            HasWeapon = GearScore.HasWeapon(this),
            NothingToBank = !BankDepositSkill.HasErrand(this),
            FailedGoalCount = FailedGoalCount,
            AmbitionSkill = AmbitionRules.SkillOf(CurrentAmbition()),
            DiedRecently = !TimeRules.Rested(
                _lastDeathAt,
                Core.Now,
                TimeSpan.FromHours(SosariaSettings.Characters?.Career?.DeathAvoidHours ?? CareerSettings.DefaultDeathAvoidHours)
            ),
            FriendName = MemoryChoiceRules.FriendName(MemoryStore.Shared, Recall.IdOf(this)),
            AvoidName = MemoryChoiceRules.EnemyName(MemoryStore.Shared, Recall.IdOf(this)),
            HasTreasureMap = TreasureMaps.FirstOpen(pack) != null || TreasureMaps.UnlootedDig(this) != null,
            HasPkReport = Disposition == DispositionKind.Lawful && PkReportToAnswer() != null,
            FollowsPartyLeader = GameParty.LeaderToFollow(this) != null,
            CrewDisbanded = Behaviour.Party.FindByMember(CharacterId) is { Disbanded: true },
            PlaceDanger = DangerMap.Shared.VisitFactor(HomeFacet, HuntHomeNow()?.Ground?.Arrival ?? HomeSpot, Core.Now),
            BankCrowdOpen = BankCrowd.Wants(this),
            SuppliesLow = SupplyCheck.WantsShopping(this) && VendorBuySkill.HasSupplyErrand(this),
            MustReArm = SpareKit.MustReArm(this),
            MustRestock = Disposition == DispositionKind.Outlaw && ConflictSkill.WhyNotSetOut(this) != HuntEndReason.None,
            LeavesDen = LawfulRules.LeavesDen(
                IsPk || PkRules.IsRed(Kills),
                PkRules.InBuccaneersDen(X, Y),
                PartyRoads.RidesAgainstDen(this)
            ),
            IsPkHunter = IsPkHunter,
            HasShoppingErrand = VendorBuySkill.HasErrand(this),
            NeedsTool = WorkerTools.MissesAny(this),
            NeedsPetFood = PetPantry.IsLow(this),
            ManaFraction = ManaMax <= 0 ? 1 : (double)Mana / ManaMax,
            IsCaster = SpellBook.IsCaster(Skills.Magery.Value),
            Tendencies = PersonProfile.Tendencies,
            CraftTrade = CraftCareerRules.TradeOf(PersonProfile.Class, kind => Definition?.UsesSkill(kind) == true),
            InDungeon = DungeonTripSkill.InDungeon(this),
            DungeonDemand = DungeonShare.Demand
        };
    }

    /// <summary>A close friend fell lately at this character's hunt ground or dungeon.</summary>
    private bool FriendFellAtHunt()
    {
        var hours = DeathAvoid.WindowHours(SosariaSettings.Characters?.Career?.DeathAvoidHours ?? CareerSettings.DefaultDeathAvoidHours);
        var places = MemoryChoiceRules.FriendDeathPlaces(MemoryStore.Shared, Recall.IdOf(this), Core.Now, TimeSpan.FromHours(hours));

        if (places.Count == 0)
        {
            return false;
        }

        var hunt = HuntHomeNow();
        var map = Map.Parse(HomeFacet);
        return DeathAvoid.FriendFellAt(
            places,
            hunt?.Ground == null ? null : PlaceNames.Of(hunt.Ground.Arrival, map),
            hunt?.Dungeon == null ? null : PlaceNames.Of(hunt.Dungeon.Arrival, map)
        );
    }

    public bool HouseOwned()
    {
        var atFeet = BaseHouse.FindHouseAt(this) != null;
        var live = HouseRules.BySerial(HouseSerial) is { Deleted: false };
        return HouseRules.Stands(HouseSerial, atFeet, live);
    }

    public bool BoatOwned() => BoatRules.OwnedBoat(BoatSerial) is { Deleted: false };

    public bool VendorOwned() => PlayerVendorRules.OwnedVendor(VendorSerial) is { Deleted: false };

    public void SavePlan(GoalPlan plan)
    {
        if (plan == null || plan.IsComplete)
        {
            PlanId = null;
            PlanStepIndex = 0;
            PlanStepFailures = 0;
            return;
        }

        PlanId = plan.Id;
        PlanStepIndex = plan.Index;
        PlanStepFailures = plan.StepFailures;
    }

    public void ConsumeNearbyNotice(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        Memory.Working.LastNearbyName = name;
        Memory.Working.PendingMusingEvent = null;
        Memory.Working.LastMusingEvent = MusingRules.Noticed(name);
    }

    private void CommitScored(
        ScoredAction scored,
        ScoreResult result,
        bool fromModel,
        DestinationCatalog catalog,
        ActionCandidate bound = null,
        string source = null
    )
    {
        if (string.IsNullOrWhiteSpace(scored.Id.Value) || Definition == null || FacetContent == null)
        {
            return;
        }

        var candidate = bound ?? GoalLoop.Find(Definition, scored.Id, catalog, HuntHomeNow());

        if (candidate == null)
        {
            return;
        }

        var same = candidate.Id.Value.Equals(ActiveActionId, StringComparison.OrdinalIgnoreCase);
        Skill skill;

        try
        {
            skill = GoalLoop.CreateSkill(candidate, FacetContent, HomeFacet);
        }
        catch (FormatException bad)
        {
            // A bad characters.json step must not stop the AI timer. Say so once, then
            // wander as the rules do for any action that cannot run.
            if (BadActionIds.Add(candidate.Id.Value))
            {
                logger.Warning("{Name} cannot run {Action}: {Reason}", Name, candidate.Id.Value, bad.Message);
            }

            CommitIdleFallback(result);
            return;
        }

        if (skill == null)
        {
            return;
        }

        if (same && Routine is { NeedsNext: false } && Routine.CurrentSkill != null)
        {
            LastScore = result;
            return;
        }

        if (!same)
        {
            RememberRecentSkill(ActionId.SkillKindOf(ActiveActionId));
        }

        ActiveActionId = candidate.Id.Value;
        ActiveRoutineId = candidate.RoutineId;
        ActiveGoalKind = result.Goal.Kind.ToString();
        ActiveGoalTarget = result.Goal.Target;
        if (!fromModel)
        {
            SavePlan(result.Plan);
        }

        FactsBeforeStep = CaptureFacts();
        FoughtThisStep = false;
        LastScore = result;
        DecideResolved = true;

        if (Routine == null)
        {
            AttachRoutine(new Routine([skill]));
        }
        else
        {
            Routine.Replace([skill], keepFailPause: true);
        }

        if (same)
        {
            return;
        }

        GoalLoop.LogChoice(Name, scored, result, source ?? (fromModel ? GoalLoop.ModelSource : GoalLoop.FallbackSource));
    }

    private void InitBody()
    {
        Female = Utility.RandomBool();
        Body = Race.Human.AliveBody(Female);
        Hue = Race.Human.RandomSkinHue();
        Name = NameList.RandomName(Female ? FemaleNameList : MaleNameList);
        HairItemID = Race.Human.RandomHair(Female);
        HairHue = Race.Human.RandomHairHue();
    }

    // Clothes are movable, so death moves them to the corpse. Dress again on return, in
    // dye-tub colours; clothes back from the corpse were already worn by EnsureWornGear.
    private void EnsureOutfit()
    {
        if (FindItemOnLayer(Layer.Shirt) == null)
        {
            AddItem(new Shirt(Utility.RandomDyedHue()));
        }

        if (FindItemOnLayer(Layer.Pants) == null)
        {
            AddItem(new LongPants(Utility.RandomDyedHue()));
        }

        if (FindItemOnLayer(Layer.Shoes) == null)
        {
            AddItem(new Boots(Utility.RandomDyedHue()));
        }
    }

    /// <summary>A living person dresses like a player (see <see cref="GearEquip.DressForBuild"/>).</summary>
    private void EnsureWornGear()
    {
        if (Alive)
        {
            GearEquip.DressForBuild(this);
        }
    }

    private void EnsureBackpack()
    {
        if (Deleted)
        {
            return;
        }

        if (Backpack == null)
        {
            AddItem(new Backpack());
        }

        FlattenNestedBackpacks();
    }

    private void EnsureHumanStats()
    {
        if (RawStr < MinStrength)
        {
            SetStr(MinStrength, MaxStrength);
        }

        if (RawDex < MinDexterity)
        {
            SetDex(MinDexterity, MaxDexterity);
        }

        if (RawInt < MinIntelligence)
        {
            SetInt(MinIntelligence, MaxIntelligence);
        }
    }

    private void EnsureRoleGear()
    {
        if (!KitOnceRules.ShouldDress(IsGhost))
        {
            return;
        }

        var freshStart = KitOnceRules.ShouldGrantCombatKit(_careerStarted);
        EnsureWorkerSupport(freshStart);

        if (freshStart)
        {
            EnsureKit();
            _careerStarted = true;
        }
    }

    /// <summary>
    /// Skill floors and dress every time; tools, bandages and craft piles only on a
    /// character's first day. After that, anything used up or broken is bought.
    /// </summary>
    private void EnsureWorkerSupport(bool freshStart)
    {
        EnsureBackpack();
        EnsureWornGear();
        EnsureOutfit();
        EnsureWorkerDefense();

        if (WorksAt(SkillKinds.Lumberjack))
        {
            EnsureTool<Hatchet>(freshStart);
            EnsureSkill(SkillName.Lumberjacking);
        }

        if (WorksAt(SkillKinds.Mine))
        {
            EnsureTool<Pickaxe>(freshStart);
            EnsureSkill(SkillName.Mining);
        }

        if (WorksAt(SkillKinds.Fish))
        {
            EnsureTool<FishingPole>(freshStart);
            EnsureSkill(SkillName.Fishing);
        }

        if (freshStart)
        {
            EnsureBandages();
        }

        GearEquip.EquipReadyWeapon(this);

        if (WorksAt(SkillKinds.Alchemy))
        {
            EnsureTool<MortarPestle>(freshStart);
            EnsureSkill(SkillName.Alchemy);

            if (freshStart)
            {
                EnsurePile(new Bottle(CraftSupply.StartingBottles));
                EnsurePile(new Ginseng(CraftSupply.StartingReagents));
            }
        }

        if (WorksAt(SkillKinds.Smith))
        {
            EnsureSkill(SkillName.Blacksmith);

            if (freshStart)
            {
                EnsurePile(new IronIngot(CraftSupply.StartingIngots));
            }
        }

        if (WorksAt(SkillKinds.Mage) || WorksAt(SkillKinds.Mark) || WorksAt(SkillKinds.Recall) ||
            WorksAt(SkillKinds.Gate))
        {
            EnsureSkill(SkillName.Magery);

            if (freshStart)
            {
                EnsureSpellbook();
                EnsureBlankRunes();
                EnsurePile(new Garlic(CraftSupply.StartingReagents));
                EnsurePile(new Ginseng(CraftSupply.StartingReagents));
                EnsurePile(new SpidersSilk(CraftSupply.StartingReagents));
                EnsurePile(new SulfurousAsh(CraftSupply.StartingReagents));
                EnsurePile(new BlackPearl(CraftSupply.StartingReagents));
                EnsurePile(new Bloodmoss(CraftSupply.StartingReagents));
                EnsurePile(new MandrakeRoot(CraftSupply.StartingReagents));
            }
        }

        if (WorksAt(SkillKinds.Cook))
        {
            EnsureTool<Skillet>(freshStart);
            EnsureSkill(SkillName.Cooking);
        }

        if (WorksAt(SkillKinds.Inscription))
        {
            EnsureTool<ScribesPen>(freshStart);
            EnsureSkill(SkillName.Inscribe);

            // A scroll is written only for a spell the scribe's own book holds.
            if (freshStart)
            {
                EnsureSpellbook();
            }
        }

        if (WorksAt(SkillKinds.Tailor))
        {
            EnsureTool<SewingKit>(freshStart);
            EnsureSkill(SkillName.Tailoring);
        }

        if (WorksAt(SkillKinds.Carpentry))
        {
            EnsureTool<Saw>(freshStart);
            EnsureSkill(SkillName.Carpentry);
        }

        if (WorksAt(SkillKinds.Fletch))
        {
            EnsureTool<FletcherTools>(freshStart);
            EnsureSkill(SkillName.Fletching);
        }

        if (WorksAt(SkillKinds.Tinker))
        {
            EnsureTool<TinkerTools>(freshStart);
            EnsureSkill(SkillName.Tinkering);
        }

        if (WorksAt(SkillKinds.Cartography))
        {
            EnsureTool<MapmakersPen>(freshStart);
            EnsureSkill(SkillName.Cartography);
        }

        if (WorksAt(SkillKinds.Camp))
        {
            if (freshStart && Backpack?.FindItemByType<Kindling>() == null)
            {
                AddToBackpack(new Kindling(CampRules.StartingAmount));
            }

            EnsureSkill(SkillName.Camping);
        }

        if (WorksAt(SkillKinds.Peace) || WorksAt(SkillKinds.Discord) || WorksAt(SkillKinds.Provoke) ||
            WorksAt(SkillKinds.Music))
        {
            EnsureTool<Lute>(freshStart);
            EnsureSkill(SkillName.Musicianship);

            if (WorksAt(SkillKinds.Peace))
            {
                EnsureSkill(SkillName.Peacemaking);
            }

            if (WorksAt(SkillKinds.Discord))
            {
                EnsureSkill(SkillName.Discordance);
            }

            if (WorksAt(SkillKinds.Provoke))
            {
                EnsureSkill(SkillName.Provocation);
            }
        }

        if (WorksAt(SkillKinds.Lockpick))
        {
            if (freshStart && Backpack?.FindItemByType<Lockpick>() == null)
            {
                AddToBackpack(new Lockpick(LockpickRules.StartingAmount));
            }

            EnsureSkill(SkillName.Lockpicking);
        }
    }

    // A copy wears its class build and outfit from its profile. Only an authored
    // worker fixture still gets the old leather and fighting floor.
    private void EnsureWorkerDefense()
    {
        if (Build?.Role != CharacterRole.Worker || WorkSites.IsCopy(CharacterId))
        {
            return;
        }

        if (RawStr < BuildPresets.WorkerStrength)
        {
            SetStr(BuildPresets.WorkerStrength, MaxStrength);
        }

        EnsureSkill(SkillName.Swords, BuildPresets.WorkerFightSkill);
        EnsureSkill(SkillName.Tactics, BuildPresets.WorkerFightSkill);
        EnsureSkill(SkillName.Healing, BuildPresets.WorkerFightSkill);
        EnsureSkill(SkillName.Anatomy, BuildPresets.WorkerAnatomySkill);
        EnsureSkill(SkillName.MagicResist, BuildPresets.WorkerResistSkill);

        if (FindItemOnLayer(Layer.InnerTorso) == null)
        {
            AddItem(new LeatherChest());
        }

        if (FindItemOnLayer(Layer.Gloves) == null)
        {
            AddItem(new LeatherGloves());
        }

        if (FindItemOnLayer(Layer.Neck) == null)
        {
            AddItem(new LeatherGorget());
        }

        if (FindItemOnLayer(Layer.Helm) == null)
        {
            AddItem(new LeatherCap());
        }
    }

    private void EnsureBandages()
    {
        if (Backpack?.FindItemByType<Bandage>() != null)
        {
            return;
        }

        AddToBackpack(new Bandage(HealRules.StartingBandageAmount));
    }

    private bool WorksAt(string kind) =>
        Routine?.HasSkill(kind) == true || Definition?.UsesSkill(kind) == true;

    private void EnsureKit()
    {
        var kit = Build?.Kit;

        if (kit == null || kit.Count == 0)
        {
            return;
        }

        for (var i = 0; i < kit.Count; i++)
        {
            var name = kit[i];

            if (HasKitItem(name))
            {
                continue;
            }

            var item = KitResolver.Create(name, n => AssemblyHandler.FindTypeByName(n));

            if (item == null)
            {
                if (MissingKitTypes.Add(name))
                {
                    logger.Warning("Kit type {Type} was not found", name);
                }

                continue;
            }

            if (KitOnceRules.IsNewbiedKitPiece(item is BaseWeapon, item is Spellbook))
            {
                item.LootType = LootType.Newbied;
            }

            if (KitOnceRules.GoesInBackpack(item.Layer))
            {
                AddToBackpack(item);
            }
            else
            {
                GearEquip.WearKitPiece(this, item);
            }
        }
    }

    /// <summary>True when the pack holds, or the body wears, an item of the kit type <paramref name="name"/>.</summary>
    internal bool HasKitItem(string name)
    {
        var type = AssemblyHandler.FindTypeByName(name);

        if (type == null)
        {
            return false;
        }

        if (Backpack?.FindItemByType(type) != null)
        {
            return true;
        }

        foreach (var item in Items)
        {
            if (type.IsInstanceOfType(item))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when the build has combat kit pieces and the person carries none of them: a death took them.</summary>
    internal bool IsCombatKitMissing()
    {
        var kit = Build?.Kit;

        if (kit == null || kit.Count == 0)
        {
            return false;
        }

        var expected = false;

        for (var i = 0; i < kit.Count; i++)
        {
            var name = kit[i];

            if (!KitOnceRules.IsCombatKitPiece(name))
            {
                continue;
            }

            expected = true;

            if (HasKitItem(name))
            {
                return false;
            }
        }

        return expected;
    }

    private void EnsureTool<T>(bool freshStart) where T : Item, new()
    {
        if (!freshStart ||
            FindItemOnLayer(Layer.OneHanded) is T ||
            FindItemOnLayer(Layer.TwoHanded) is T ||
            Backpack?.FindItemByType<T>() != null)
        {
            return;
        }

        AddToBackpack(new T());
    }

    // Casting needs the spell in a book. A caster's first day brings a full one. A
    // paladin's or ninja's book holds no magery, so only a mage's book counts.
    private void EnsureSpellbook()
    {
        if (Spellbook.FindRegular(this) != null)
        {
            return;
        }

        AddToBackpack(new Spellbook(FullSpellbookContent));
    }

    // Mark needs a blank rune: one for home and a few for hunt spots.
    private void EnsureBlankRunes()
    {
        if (Backpack?.FindItemByType<RecallRune>() != null)
        {
            return;
        }

        for (var i = 0; i < StartingBlankRunes; i++)
        {
            AddToBackpack(new RecallRune());
        }
    }

    private void EnsurePile(Item pile)
    {
        if (pile == null)
        {
            return;
        }

        EnsureBackpack();
        var have = Backpack.FindItemByType(pile.GetType());

        if (have != null)
        {
            if (have.Amount < pile.Amount)
            {
                have.Amount = pile.Amount;
            }

            pile.Delete();
            return;
        }

        AddToBackpack(pile);
    }

    private void EnsureSkill(SkillName name) => EnsureSkill(name, MinProfessionSkill);

    // A raise that would push the skill total past the era cap is skipped: a player
    // had to lower a skill before a new one could grow.
    private void EnsureSkill(SkillName name, double min)
    {
        var skill = Skills[name];

        if (skill.Base >= min)
        {
            return;
        }

        var raise = (int)Math.Ceiling((MaxProfessionSkill - skill.Base) * SkillTenths);

        if (Skills.Total + raise > SkillsCap)
        {
            return;
        }

        SetSkill(name, min, MaxProfessionSkill);
    }

    private int SkillValue(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName) ||
            !Enum.TryParse(skillName, ignoreCase: true, out SkillName skill))
        {
            return 0;
        }

        return (int)Math.Max(Skills[skill].Value, Skills[skill].Base);
    }

    private void TellNews(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        Remember(line);
        SpeakAloud(line);
    }

    public void SetWalkPace() => ApplyPresencePace(watched: true);

    public void ApplyPresencePace(bool watched)
    {
        _thinkSeconds = watched ? PresenceFocus.NearThinkSeconds : PresenceFocus.FarThinkSeconds;
        Motor.Running = false;
    }

    /// <summary>Sets the raw strength and fills hits to the new maximum.</summary>
    public void SetStr(int value)
    {
        RawStr = value;
        Hits = HitsMax;
    }

    public void SetStr(int min, int max) => SetStr(Utility.RandomMinMax(min, max));

    public void SetDex(int value)
    {
        RawDex = value;
        Stam = StamMax;
    }

    public void SetDex(int min, int max) => SetDex(Utility.RandomMinMax(min, max));

    public void SetInt(int value)
    {
        RawInt = value;
        Mana = ManaMax;
    }

    public void SetInt(int min, int max) => SetInt(Utility.RandomMinMax(min, max));

    /// <summary>Sets a skill's base, raising its cap when the value is above it.</summary>
    public void SetSkill(SkillName name, double value)
    {
        var skill = Skills[name];

        if (value > skill.Cap)
        {
            skill.Cap = value;
        }

        skill.Base = value;
    }

    public void SetSkill(SkillName name, double min, double max) =>
        SetSkill(name, min + Utility.RandomDouble() * (max - min));

    // Every character loads at the same moment, so a shared first delay would have the whole
    // town speak in the same second every interval. A random first delay spreads them out,
    // the way people in a room do not all talk at once.
    private void StartSpeechTimer()
    {
        var delay = SpeechTiming.NextDelay(
            SosariaSettings.MusingInterval,
            Utility.Random(SpeechTiming.PercentRange + 1)
        );
        Timer.StartTimer(delay, SayLine, out _speechTimer);
    }

    private void SayLine()
    {
        StartSpeechTimer();
        NoticeLifeForMusing();
        TryMusing();
    }

    public string LastMusingEvent => Memory.Working.LastMusingEvent;

    public void NoteMusingEvent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Memory.Working.PendingMusingEvent = text.Trim();
        TryMusing();
    }

    // Ambient sector scans run on a per-person cadence, not every think: six scans
    // per think times a crowd of walkers was the loop's whole cost.
    public bool DangerScanDue(bool watched = true) =>
        ScanPace.Due(
            Core.TickCount,
            ref _nextDangerScanAt,
            watched ? ScanPace.DangerMs : PresenceFocus.FarDangerMs,
            Serial.Value
        );

    public bool AmbientScanDue() =>
        ScanPace.Due(Core.TickCount, ref _nextAmbientScanAt, ScanPace.AmbientMs, Serial.Value);

    public bool WorldScanDue() =>
        ScanPace.Due(Core.TickCount, ref _nextWorldScanAt, ScanPace.WorldMs, Serial.Value);

    /// <summary>The seed of this person's current job phase, scaled by its traits.</summary>
    public int JobSeed(DateTime now) => ChoiceSeed.For(Serial.Value, now, PersonProfile.PhaseLengthMultiplier);

    /// <summary>Morning, day or night by this person's own waking hours, not a shared clock.</summary>
    public DayPart DayPartAt(DateTime now)
    {
        var (start, end) = SessionHours.Resolve(Serial.Value, Persona?.ActiveStartHour, Persona?.ActiveEndHour);
        return DayShapeRules.Part(DayShapeRules.LocalHour(now), start, end);
    }

    public void NoticeLifeForMusing()
    {
        if (IsGhost || Map == null || Map == Map.Internal)
        {
            return;
        }

        var part = DayPartAt(Core.Now);

        if (_notedDayPart && part != _lastDayPart)
        {
            NoteMusingEvent(MusingRules.DayTurned(part.ToString()));
        }

        _notedDayPart = true;
        _lastDayPart = part;

        string nearest = null;

        foreach (var mobile in Map.GetMobilesInRange(Location, Brain.HearRange))
        {
            if (mobile != this && People.IsLivingPlayer(mobile) && CanSee(mobile))
            {
                nearest = mobile.Name;
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(nearest) &&
            !string.Equals(nearest, Memory.Working.LastNearbyName, StringComparison.Ordinal))
        {
            NoteMusingEvent(MusingRules.Noticed(nearest));
        }

        Memory.Working.LastNearbyName = nearest;
    }

    public void TryMusing()
    {
        if (IsGhost || Map == null || Map == Map.Internal)
        {
            return;
        }

        if (Conversation.IsActive(Core.Now))
        {
            return;
        }

        var now = Core.Now;

        if (!MusingRules.ShouldAttempt(
                Memory.Working.PendingMusingEvent,
                Memory.Working.LastMusingEvent,
                Memory.Working.LastMusingAttempt,
                now,
                SosariaSettings.MusingInterval))
        {
            return;
        }

        var eventText = Memory.Working.PendingMusingEvent;
        Memory.Working.LastMusingEvent = eventText;
        Memory.Working.PendingMusingEvent = null;
        Memory.Working.LastMusingAttempt = now;

        if (!MusingRules.PassesSpeakChance(Utility.Random(100)))
        {
            return;
        }

        EnsureLife();

        if (Brain.RequestMusing(this, eventText))
        {
            return;
        }

        if (!MusingRules.MayUseWrittenLine(Memory.Working.LastWrittenMusingAt, now))
        {
            return;
        }

        // A written musing is ambient speech: only a line that can be said unprovoked.
        var line = AmbientTalk.PickAmbientLine(this);

        if (string.IsNullOrEmpty(line) || SpokenRepeat.IsNearRepeat(line, Memory.Working.RecentSpeech))
        {
            return;
        }

        Memory.Working.LastWrittenMusingAt = now;
        SpeakAloud(line);
    }

    public IReadOnlyList<string> RecentSpeech() => Memory.Working.RecentSpeech;

    /// <param name="ran">How long the skill ran from its start; zero when it would not start.</param>
    public void NoteRoutineOutcome(string skillName, SkillStatus status, TimeSpan ran)
    {
        if (status is SkillStatus.Done or SkillStatus.Failed)
        {
            SavePlan(GoalPlanRules.AfterOutcome(CurrentPlan, skillName, status == SkillStatus.Done));
            ObservePlanStep(skillName, status);
        }

        if (status == SkillStatus.Failed && (RepeatFailure.CoolsDown(skillName) || RepeatFailure.Counts(skillName)))
        {
            NoteSkillFailure(skillName, Core.Now, ran);
        }

        // A skill's own success ends its failure run, whatever the errand streak below keeps.
        if (status == SkillStatus.Done && !string.IsNullOrWhiteSpace(skillName))
        {
            Memory.Working.SkillFailures.Remove(skillName);
        }

        if (status == SkillStatus.Done && RepeatFailure.RestsAfterDone(skillName))
        {
            Memory.Working.SkillRestUntil[skillName] = Core.Now + RepeatFailure.DoneRest;
        }

        if (!RepeatFailure.Counts(skillName))
        {
            return;
        }

        var id = !string.IsNullOrWhiteSpace(ActiveActionId)
            ? ActiveActionId
            : string.IsNullOrWhiteSpace(ActiveRoutineId) ? skillName : ActiveRoutineId;

        if (status == SkillStatus.Failed)
        {
            Memory.Working.FailedCount = RepeatFailure.AfterFailure(Memory.Working.FailedRoutine, id, Memory.Working.FailedCount);
            Memory.Working.FailedRoutine = id;

            if (RepeatFailure.IsBlocked(Memory.Working.FailedCount) && skillName != SkillKinds.GoHome)
            {
                ActivityPulse.NoteGaveUp();

                if (SosariaSettings.LogActivity)
                {
                    logger.Information(
                        "{Name} gave up {Routine} after {Count} failures",
                        Name,
                        ActionDescriptions.Phrase(ActionId.SkillKindOf(id), id),
                        Memory.Working.FailedCount
                    );
                }

                // Said like a player, never in planner words: see SpeechLines.FailedLine.
                var line = SpeechLines.FailedLine(id, Utility.Random(int.MaxValue));

                if (!SpokenRepeat.IsNearRepeat(line, Memory.Working.RecentSpeech))
                {
                    SpeakAloud(line);
                }
            }

            var goalKind = Enum.TryParse(ActiveGoalKind, out GoalKind parsed) ? parsed : GoalKind.Work;
            FailedGoalCount = GoalSwitch.AfterFailure(
                Enum.TryParse(FailedGoalKind, out GoalKind last) ? last : goalKind,
                goalKind,
                FailedGoalCount
            );
            FailedGoalKind = goalKind.ToString();

            if (skillName == SkillKinds.GoHome)
            {
                FailedHomeWalks++;
                DungeonTrouble = DungeonEscapeRules.Track(DungeonTrouble, DungeonGround.PlacesToLeave(Map)(Location), Core.Now);

                if (FailedHomeWalks >= HomeLeash.FailedWalksBeforeMoongate && SosariaSettings.LogActivity)
                {
                    logger.Information("{Line}", HomeLeash.CannotFindHomeLine(Name, Location));
                }
            }

            return;
        }

        if (status == SkillStatus.Done && RepeatFailure.Clears(skillName))
        {
            Memory.Working.FailedCount = RepeatFailure.AfterSuccess();
            Memory.Working.FailedRoutine = null;
            FailedGoalCount = GoalSwitch.AfterSuccess();
            FailedHomeWalks = skillName == SkillKinds.GoHome ? 0 : FailedHomeWalks;
        }
    }

    /// <summary>
    /// A step with a target of its own ended: work there ends its streak, and a failure adds
    /// to it (<see cref="JobTargetRest"/>). The third failure the same way rests the step for
    /// that target, and says so once.
    /// </summary>
    public void NoteJobTarget(Skill skill, SkillStatus status)
    {
        if (skill?.AimedAt is not { Key: not null } target)
        {
            return;
        }

        if (status == SkillStatus.Done)
        {
            JobTargetRest.NoteSuccess(this, skill.Name, target);
            return;
        }

        if (status != SkillStatus.Failed)
        {
            return;
        }

        var streak = JobTargetRest.NoteFailure(this, skill.Name, target, skill.FailReason, Core.Now);

        if (RepeatFailure.IsBlocked(streak.Count) && skill.FailReason != JobTargetRest.RestingWhy && SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} rests {Skill} for {Target} {Minutes} minutes after {Count} failures the same way ({Why})",
                Name,
                skill.Name,
                target.Key,
                (int)RepeatFailure.CooldownAfter(streak.Count).TotalMinutes,
                streak.Count,
                streak.Reason
            );
        }
    }

    /// <summary>
    /// An owned mount in reach that the person may walk to and climb on now: its mounting does
    /// not cool down, and it does not rest that mount after failing it the same way three
    /// times. Darian the Grim tried a mount he could not walk to 36 times, every minute or two.
    /// </summary>
    /// <summary>True when a mount of its own stands in reach and the Mount job does not rest.</summary>
    public bool MayRemount() => !Mounted && MayClimbOn(OwnedMounts.Nearest(this, MountRules.SearchTiles));

    private bool MayClimbOn(BaseMount mount) =>
        mount != null && !RestsSkill(SkillKinds.Mount) &&
        !JobTargetRest.Rests(this, SkillKinds.Mount, JobTargetRest.KeyOf(mount), Core.Now);

    /// <summary>True while a skill kind cools down after repeated failure, or rests after it last finished.</summary>
    public bool RestsSkill(string skillKind) => RepeatFailure.Lists(BlockedSkillKinds(Core.Now), skillKind);

    private void NoteSkillFailure(string skillName, DateTime now, TimeSpan ran)
    {
        var failures = RepeatFailure.AfterSkillFailure(Memory.Working.SkillFailures.GetValueOrDefault(skillName), now, ran, Location);
        Memory.Working.SkillFailures[skillName] = failures;

        if (RepeatFailure.IsBlocked(failures.Count) && SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} leaves {Skill} for {Minutes} minutes after {Count} failures",
                Name,
                skillName,
                (int)RepeatFailure.CooldownAfter(failures.Count).TotalMinutes,
                failures.Count
            );
        }
    }

    private IReadOnlyList<string> BlockedSkillKinds(DateTime now)
    {
        var blocked = new List<string>();

        foreach (var (skill, failures) in Memory.Working.SkillFailures)
        {
            if (RepeatFailure.IsSkillBlocked(failures, now, Location))
            {
                blocked.Add(skill);
            }
        }

        foreach (var (skill, until) in Memory.Working.SkillRestUntil)
        {
            if (until > now && !blocked.Contains(skill))
            {
                blocked.Add(skill);
            }
        }

        return blocked;
    }

    private void ObservePlanStep(string skillName, SkillStatus status)
    {
        if (Planning.Plan == null)
        {
            return;
        }

        var after = CaptureFacts();
        var interrupted = status != SkillStatus.Done && (FoughtThisStep && MustRunFrom(HuntSkill.SightThreat(this)) || !Alive);
        var unavailable = status == SkillStatus.Failed && Routine?.CurrentSkill == null;
        var observation = StepProof.Observe(
            skillName,
            FactsBeforeStep,
            after,
            status == SkillStatus.Done,
            interrupted,
            unavailable
        );
        var result = Planning.FinishStep(observation, Core.Now);
        SaveModelPlan();
        Remember(PlanMemoryLine(observation));

        if (result.RequestRevision)
        {
            var trigger = result.Kind == StepResultKind.GoalCompleted
                ? PlanTrigger.GoalComplete
                : observation.Detail == StepProof.DetailDead
                    ? PlanTrigger.Died
                    : PlanTrigger.StepFailed;
            Brain.RequestPlan(this, trigger);
        }
    }

    private static string PlanMemoryLine(StepObservation observation)
    {
        return observation.Kind switch
        {
            StepResultKind.GoalCompleted => "I finished that goal: " + observation.Detail,
            StepResultKind.StepCompleted => "I did " + observation.SkillKind + " (" + observation.Detail + ").",
            StepResultKind.NoUsefulProgress => "I tried " + observation.SkillKind + " but nothing useful came of it.",
            StepResultKind.ActionUnavailable => "I could not do " + observation.SkillKind + ".",
            StepResultKind.Interrupted => "I was interrupted during " + observation.SkillKind + ".",
            _ => "I failed at " + observation.SkillKind + "."
        };
    }

    public WorldFacts CaptureFacts()
    {
        var pack = Backpack;
        var goods = 0;
        var tools = 0;
        var obtained = false;
        var obtainedName = string.Empty;

        if (pack != null)
        {
            foreach (var item in pack.Items)
            {
                if (HarvestPack.IsHarvest(item))
                {
                    goods += item.Amount;
                }

                if (item is Pickaxe or Hatchet or FishingPole)
                {
                    tools += item.Amount;
                }
            }
        }

        if (Routine?.CurrentSkill is VendorBuySkill buy && buy.ItemsBought > 0)
        {
            obtained = true;
            obtainedName = "bought";
        }
        else if (Routine?.CurrentSkill is UpgradeGearSkill upgrade && upgrade.Bought)
        {
            obtained = true;
            obtainedName = "gear";
        }

        var partner = GameParty.MemberNames(this);
        var armor = FindItemOnLayer(Layer.InnerTorso) is BaseArmor ||
                    FindItemOnLayer(Layer.OuterTorso) is BaseArmor;
        return new WorldFacts(
            pack?.GetAmount(typeof(Gold)) ?? 0,
            BankBox?.GetAmount(typeof(Gold)) ?? 0,
            goods,
            tools,
            armor,
            obtained || armor && FactsBeforeStep.ArmorEquipped == false,
            obtainedName,
            LocationDescription,
            HomeLeash.DistanceFromHome(Location, HomeCorner) <= CharactersFile.DefaultGoToRange,
            !string.IsNullOrWhiteSpace(partner),
            partner,
            Alive && !IsGhost,
            FoughtThisStep,
            HuntKillsThisTrip,
            Hits,
            !IsGhost && Alive && _corpseSerial == 0 && _lastDeathAt != default
        );
    }

    public void SaveModelPlan()
    {
        _modelPlanLines = Planning.Plan?.ToLines() ?? [];
    }

    private SpeechFacts.Snapshot SpeechSnapshot()
    {
        var pack = 0;

        if (Backpack != null)
        {
            foreach (var item in Backpack.Items)
            {
                if (HarvestPack.IsHarvest(item))
                {
                    pack += item.Amount;
                }
            }
        }

        return new SpeechFacts.Snapshot
        {
            PackCount = pack,
            Gold = Backpack?.GetAmount(typeof(Gold)) ?? 0,
            Hits = Hits,
            HitsMax = HitsMax,
            IsAlive = Alive,
            LeadsGroup = BacksInvite(),
            Travelling = OnTrip(),
            TakesOrders = Routine?.CurrentSkill is CraftStationSkill && OrderDesk.MayTakeOrder(this)
        };
    }

    /// <summary>
    /// True when a real group stands behind an invite from this character: an engine party it
    /// is in, a group call it leads or gathers, a road group it leads, a crew gathering or on its
    /// trip, or its red gang's muster in the Den. A gang camping a spot is no group a player can
    /// join: a red patrolling Destard asked a player along to Minoc and never left.
    /// </summary>
    private bool BacksInvite() =>
        GameParty.InParty(this) || LfgBoard.IsRecruiting(this) || LfgBoard.LeadsRun(this) || PartyRoads.Leads(this) ||
        Routine?.CurrentSkill is ConflictSkill { Mustering: true } ||
        Behaviour.Party.FindByMember(CharacterId) is { IsGathering: true } or { TripActive: true };

    /// <summary>
    /// True when the step running now really takes this character somewhere: a trip kind
    /// (<see cref="SpeechFacts.IsTripKind"/>), a road group's trip, a dungeon run on its way in
    /// or home, a hunt still walking to its ground, or a red gang mustering for or riding to its
    /// camp. A banker at the Britain bank asked a player there whether they were heading to
    /// Britain too.
    /// </summary>
    private bool OnTrip() =>
        Routine?.CurrentSkill switch
        {
            null => false,
            PartyRoadTrip => true,
            DungeonTripSkill dungeon => !dungeon.ReachedInside || dungeon.HeadingHome,
            HuntSkill hunt => !hunt.IsHunting,
            ConflictSkill conflict => conflict.Mustering || RidesToCamp(conflict),
            var skill => SpeechFacts.IsTripKind(skill.Name)
        };

    /// <summary>True while a red's run is on its way to the camp of a hot spot on this facet.</summary>
    private bool RidesToCamp(ConflictSkill run)
    {
        var spots = HotSpots.For(Map);

        for (var i = 0; i < spots.Count; i++)
        {
            if (run.IsOn(spots[i].Name, GangRunPhase.Ride))
            {
                return true;
            }
        }

        return false;
    }

    public int FailedRoutineCount => Memory.Working.FailedCount;

    public string FailedRoutineId => Memory.Working.FailedRoutine;

    /// <summary>
    /// Model output. A near repeat or a line that contradicts the facts is dropped: an invite
    /// with no group behind it and a trip claim off any trip among them (<see cref="SpeechFacts.Contradicts"/>).
    /// </summary>
    public void SpeakAloud(string line)
    {
        if (!CanSpeakHere(line))
        {
            return;
        }

        line = SpeechSanitizer.StripDashes(line);

        if (SpokenRepeat.IsNearRepeat(line, Memory.Working.RecentSpeech) ||
            SpeechFacts.Contradicts(line, SpeechSnapshot()))
        {
            return;
        }

        DeliverInVoice(line, scripted: false);
    }

    /// <summary>A fixed template line. It is always said, even when it was said before.</summary>
    public void SpeakScripted(string line)
    {
        if (!CanSpeakHere(line))
        {
            return;
        }

        DeliverInVoice(SpeechSanitizer.StripDashes(line), scripted: true);
    }

    /// <summary>
    /// A reply in a conversation: the same checks as <see cref="SpeakAloud"/>, but a line that
    /// someone nearby just said still lands. An answer that drops because a crowd heard "hey"
    /// a second ago looks like the character ignored the person talking to it.
    /// </summary>
    public void SpeakDirected(string line)
    {
        if (!CanSpeakHere(line))
        {
            return;
        }

        line = SpeechSanitizer.StripDashes(line);

        if (SpokenRepeat.IsNearRepeat(line, Memory.Working.RecentSpeech) ||
            SpeechFacts.Contradicts(line, SpeechSnapshot()))
        {
            return;
        }

        DeliverInVoice(line, scripted: false, directed: true);
    }

    private bool CanSpeakHere(string line) =>
        !string.IsNullOrEmpty(line) && Map != null && Map != Map.Internal;

    private void Deliver(string line)
    {
        RememberSpeech(line);

        if (Talk.EmoteBody(line) is { } emote)
        {
            if (SosariaSettings.LogActivity)
            {
                logger.Information("{Name} emotes: {Line}", Name, line);
            }

            Emote(emote);
            return;
        }

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} says: {Line}", Name, line);
        }

        Say(line);
    }

    private void RememberSpeech(string line)
    {
        Memory.Working.RecentSpeech.Add(line);

        while (Memory.Working.RecentSpeech.Count > SpokenRepeat.RememberedLines)
        {
            Memory.Working.RecentSpeech.RemoveAt(0);
        }
    }

    private void RememberRecentSkill(string skillKind)
    {
        if (string.IsNullOrWhiteSpace(skillKind))
        {
            return;
        }

        Memory.Working.RecentSkillKinds.Add(skillKind);

        while (Memory.Working.RecentSkillKinds.Count > ActionScorer.RecentSkillLimit)
        {
            Memory.Working.RecentSkillKinds.RemoveAt(0);
        }
    }

    public override void OnDamage(int amount, Mobile from, bool willKill)
    {
        base.OnDamage(amount, from, willKill);

        // Guild war starts on this same hit, so IsEnemy is still false for the first
        // blow. A person who is already landing hits must be answered.
        if (!willKill &&
            from is { Deleted: false, Alive: true } &&
            DefendRules.ShouldAnswerHit(
                Alive,
                IsGhost,
                from.Alive,
                from is PlayerMobile,
                IsEnemy(from)
            ))
        {
            RoutineDriver.OnAggressiveAction(this, from);
        }

        if (People.IsHuman(from))
        {
            Brain.Consider(Brain.Attacked(this, from));
        }

        // Every war blow keeps the feud hot; a duel, Order against Chaos, a murder or a blow on a
        // red starts no war (GuildWarRegistry.FeedsWar).
        if (from is SosariaCharacter attacker && !Duels.AreFighting(this, from) && !EngineGuilds.Opposed(this, from) &&
            GuildWarRegistry.FeedsWar(PkRules.IsRed(attacker.Kills), PkRules.IsRed(Kills)) &&
            SosariaSettings.GuildWars?.RecordAggression(attacker.GuildIndex, GuildIndex, Core.Now) == true)
        {
            EngineGuilds.DeclareWar(attacker.GuildIndex, GuildIndex, GuildWarRegistry.WarDuration);
            SosariaSettings.Journal?.Record(
                new ShardEvent
                {
                    At = Core.Now,
                    Type = ShardEventType.GuildWar,
                    Actor = attacker.Name,
                    Other = Name,
                    Place = PlaceNames.Of(this),
                    Facet = HomeFacet,
                    X = X,
                    Y = Y,
                    Z = Z
                }
            );
            Talk.Say(attacker, TalkCategory.GuildWarStart, new TalkSlots { Name = Name, Guild = Guild?.Abbreviation });
        }
    }

    public override bool OnBeforeDeath()
    {
        FlattenNestedBackpacks();
        LastKiller = MurderReport.KillerOf(LastKiller, FindMostRecentDamager(false));
        _lastKillerPerson = PersonRef.Of(LastKiller?.GetDamageMaster(this) ?? LastKiller);
        _murderers = MurderReport.Collect(this);
        return base.OnBeforeDeath();
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (c is Corpse corpse)
        {
            FlattenExtraBags(corpse);
            CorpseSanitizer.Clean(corpse);
        }

        Planning.Interrupt(StepProof.DetailDead, Core.Now);
        SaveModelPlan();
        Brain.RequestPlan(this, PlanTrigger.Died);
        Routine?.AbortActive();
        DungeonTrouble = default;
        Warmode = false;
        Combatant = null;

        if (LastKiller is SosariaCharacter killer && !killer.Deleted && killer.Combatant == this)
        {
            killer.Combatant = null;
            killer.Warmode = false;
            killer.Motor.Action = CharacterAction.Wander;
        }

        _lastKillerName = LastKiller?.Name;

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} died to {Killer} at {Location} (kills {Kills}, criminal {Criminal})",
                Name,
                _lastKillerName ?? "something",
                Location,
                Kills,
                Criminal
            );
        }

        _goldAtDeath =
            (c?.GetAmount(typeof(Gold)) ?? 0) +
            (Backpack?.GetAmount(typeof(Gold)) ?? 0) +
            (BankBox?.GetAmount(typeof(Gold)) ?? 0);
        AdventureTracker.Shared.PersonDied(this, LastKiller, _lastKillerPerson, Core.Now);
        ReportMurderers();
        LetOutlawLoot(c);
        TrackBody(c);
        _deathsAtPlace = DeathAvoid.AfterDeath(_deathPlace, LocationDescription, _deathsAtPlace, _lastDeathAt, Core.Now);
        _deathPlace = LocationDescription;
        MarkDeath(Core.Now);
        _ghostSince = Core.Now;
        Motor.Stop();
        Motor.Action = CharacterAction.Wander;

        var deathType = ShardNews.DeathType(
            PkRules.IsRed(Kills),
            People.IsHuman(LastKiller) || LastKiller is SosariaCharacter { IsPk: true }
        );
        SosariaSettings.Journal?.Record(
            new ShardEvent
            {
                At = Core.Now,
                Type = deathType,
                Actor = Name,
                Other = _lastKillerName,
                Place = PlaceNames.Of(this),
                Facet = HomeFacet,
                X = X,
                Y = Y,
                Z = Z
            }
        );
        if (Map != null && Map != Map.Internal)
        {
            foreach (var mobile in Map.GetMobilesInRange(Location, Brain.SpeechCarryRange))
            {
                if (mobile is SosariaCharacter other && other != this && other.Alive)
                {
                    other.NoteMusingEvent(MusingRules.DiedNearby(Name));
                }
            }
        }

        Memory.ShiftBond(_lastKillerPerson, -BondRules.KillPenalty, BondRules.KilledReason);

        AttachRoutine(new Routine([new GhostSkill()], 0));
        StartGhostFallback();
        Pulse.WakeNow();
    }

    /// <summary>
    /// A human victim answers the engine's report gump. A character has no client, so it
    /// reports its murderers itself, the way a player of the era always did. Its friends and
    /// the PK hunters near then ride after the killer (<see cref="PartyRoads.RaisePosse"/>).
    /// </summary>
    private void ReportMurderers()
    {
        var murderers = _murderers;
        _murderers = null;

        if (murderers == null)
        {
            return;
        }

        for (var i = 0; i < murderers.Count; i++)
        {
            var killer = murderers[i];
            var wasRed = killer.Murderer;

            if (!PlayerMurderSystem.ReportMurder(this, killer) || killer is not SosariaCharacter character)
            {
                continue;
            }

            AdventureTracker.Shared.FirstKill(character, this, Core.Now);

            if (wasRed || !killer.Murderer)
            {
                continue;
            }

            if (SosariaSettings.LogActivity)
            {
                logger.Information("{Name} went red", character.Name);
            }

            Talk.Say(character, TalkCategory.PkWentRed);
        }

        PartyRoads.RaisePosse(this, murderers.Contains(LastKiller) ? LastKiller : murderers[0]);
    }

    /// <summary>
    /// An outlaw strips the body it just made: the gold first, then whatever else its pack
    /// can still carry. The victim's corpse run finds what was left.
    /// </summary>
    private void LetOutlawLoot(Container corpse)
    {
        if (LastKiller is not SosariaCharacter { Deleted: false, Alive: true } killer ||
            killer.Disposition != DispositionKind.Outlaw || PkRules.IsRed(Kills) && !killer.IsPk)
        {
            return;
        }

        var gold = corpse?.GetAmount(typeof(Gold)) ?? 0;

        if (gold > 0 && corpse.ConsumeTotal(typeof(Gold), gold))
        {
            killer.AddToBackpack(new Gold(gold));
        }

        var taken = StripInto(corpse, killer.Backpack);

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} looted {Victim} ({Count} items)", killer.Name, Name, taken);
        }

        Talk.Say(killer, TalkCategory.PkLoot, new TalkSlots { Target = Name });
    }

    private static int StripInto(Container corpse, Container pack)
    {
        if (corpse == null || pack == null)
        {
            return 0;
        }

        var taken = 0;
        var items = new List<Item>(corpse.Items);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];

            if (item is not { Deleted: false, Movable: true } || item.LootType != LootType.Regular ||
                pack.TotalWeight + item.TotalWeight > pack.MaxWeight)
            {
                continue;
            }

            pack.DropItem(item);
            taken++;
        }

        return taken;
    }

    /// <summary>
    /// The backstop for a ghost whose way back never runs: long, and never shorter than the
    /// stuck wait, so a ghost kept over a restart gets its walk before any stand-up.
    /// </summary>
    private void StartGhostFallback()
    {
        _ghostFallbackTimer.Cancel();
        var delay = GhostRules.BackstopDelay(Core.Now, _ghostSince);
        Timer.StartTimer(delay, () => FallbackFromGhost(GhostRules.BackstopReason), out _ghostFallbackTimer);
    }

    private void FinishRestore(string reason)
    {
        _ghostFallbackTimer.Cancel();

        // The next death names its own killer; a stale one would blame this death's.
        LastKiller = null;
        Hits = Math.Max(1, HitsMax / 2);
        Stam = StamMax;
        Mana = ManaMax;
        Warmode = false;
        Combatant = null;
        Poison = null;
        ProcessDelta();

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} restored from ghost ({Reason})", Name, reason);
        }

        // A red far from its body recalls home by its book; the death took the scrolls that charge it.
        RuneKit.ChargeForHome(this);
        StartCorpseRunAfterRaise(reason);
    }

    /// <summary>
    /// Raised while no ghost step ran (a player's spell, a staff command): the same corpse run
    /// the ghost's own way back ends in starts here. Annora, a red raised beside her body, walked
    /// off in the death robe and left her gear on it.
    /// </summary>
    private void StartCorpseRunAfterRaise(string reason)
    {
        var ghostStepRuns = Routine is { NeedsNext: false, CurrentSkill: GhostSkill };

        if (!CorpseRunRules.RunsAfterRaise(reason == FallbackReason, ghostStepRuns, _corpseSerial != 0))
        {
            return;
        }

        var run = GhostSkill.AfterRaise();

        if (Routine == null)
        {
            AttachRoutine(new Routine([run]));
        }
        else
        {
            Routine.Replace([run]);
        }

        Pulse.WakeNow();
    }

    private void ScheduleReturn(DateTime now)
    {
        var delay = ReturnAfterDeath <= TimeSpan.Zero
            ? CharactersFile.DefaultReturnAfterDeath
            : ReturnAfterDeath;
        ReturnAt = ReturnSchedule.At(now, delay);
        StartReturnTimer(delay);
    }

    private void StartReturnTimer(TimeSpan delay)
    {
        _returnTimer.Cancel();
        Timer.StartTimer(delay, ReturnFromDeath, out _returnTimer);
    }

    private void ReturnFromDeath()
    {
        _returnTimer.Cancel();
        ReturnAt = default;

        var map = Map.Parse(string.IsNullOrEmpty(HomeMapName) ? CharactersFile.DefaultMapName : HomeMapName);
        var location = LoginSpot;

        if (map != null && map != Map.Internal && !map.CanSpawnMobile(location))
        {
            location = new Point3D(location.X, location.Y, map.GetAverageZ(location.X, location.Y));
        }

        location = MarkedSpawn(map, location);

        RestoreLife(FallbackReason);
        Hits = HitsMax;
        Stam = StamMax;
        Mana = ManaMax;
        Poison = null;
        Warmode = false;
        Combatant = null;
        RoutineStepIndex = 0;
        Routine?.Restart();
        EnsureWorkerSupport(freshStart: false);
        MoveToWorld(location, map);

        var line = Persona?.PickReturnLine();

        if (!string.IsNullOrEmpty(line))
        {
            SpeakAloud(line);
        }

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} returned to {Location} on {Map}", Name, location, map);

            if (IsCombatKitMissing())
            {
                logger.Information("{Name} returned without combat kit", Name);
            }
        }
    }

    // Construction and the [add command both start on Map.Internal. The pulse runs only
    // while the character stands in the world, so every map change starts or stops it.
    protected override void OnMapChange(Map oldMap)
    {
        base.OnMapChange(oldMap);

        if (Map != null && Map != Map.Internal)
        {
            Pulse.Start();
        }
        else
        {
            Pulse.Stop();
            Motor.Stop();
        }
    }

    // A walk home that failed in one dungeon says nothing once the character stands anywhere
    // else: out by a pad, a gate or a recall, or into another dungeon.
    public override void OnRegionChange(Region old, Region @new)
    {
        base.OnRegionChange(old, @new);

        if (DungeonTrouble.Since != default)
        {
            DungeonTrouble = DungeonEscapeRules.AfterMove(DungeonTrouble, DungeonGround.PlacesToLeave(Map)(Location));
        }

        AdventureTracker.Shared.Moved(this);
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        _memory = new CharacterMemory(this);
        Planning.Restore(ModelPlan.FromLines(_modelPlanLines), Core.Now, CharacterId);
        Persona ??= Configuration.Persona.CreateNeutral();
        Build ??= BuildPresets.WorkerDefault;
        Team = IsPk ? SosariaCombat.PkTeam : SosariaCombat.Team;

        // The world loads every mobile before any item, so carried items are still blank here.
        // Item and map work waits for a timer, which first runs after the whole world is loaded.
        Timer.StartTimer(EnsureBackpack);
        Timer.StartTimer(FoldStaleTrades);
        Timer.StartTimer(ReturnFromLoad);
        StartSpeechTimer();

        if (IsGhost)
        {
            AttachRoutine(new Routine([new GhostSkill()], 0));
            StartGhostFallback();
        }

        if (ReturnSchedule.IsScheduled(ReturnAt))
        {
            StartReturnTimer(ReturnSchedule.DelayUntil(ReturnAt, Core.Now));
        }
    }

    public override void OnAfterDelete()
    {
        Pulse.Stop();
        _speechTimer.Cancel();
        _returnTimer.Cancel();
        _ghostFallbackTimer.Cancel();
        base.OnAfterDelete();
    }
}
