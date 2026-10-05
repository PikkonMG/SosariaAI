using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Server;
using SosariaAI.Mobiles;
using SosariaAI.Skills;

namespace SosariaAI.Combat;

public static partial class CombatBrain
{
    /// <summary>A body holds at most this many things in its hands.</summary>
    public const int HandCount = 2;

    /// <summary>Hostiles one scan keeps. The same cap the hunt threat scan rates.</summary>
    public const int MaxSightings = HuntSkill.MaxRatedHostiles;

    // Weak keys: a deleted character's fight memory goes with it, no cleanup hook needed.
    private static readonly ConditionalWeakTable<SosariaCharacter, Memory> Memories = new();

    private static Memory MemoryOf(SosariaCharacter character) =>
        Memories.GetValue(character, static _ => new Memory());

    /// <summary>The last call this brain made, so each change is logged once.</summary>
    private enum Decision
    {
        None,
        Engage,
        Pull,
        Decline,
        Retreat,
        Turn,
        Clear,
        Switch
    }

    /// <summary>One hostile seen by the last scan.</summary>
    private struct Sighting
    {
        public Mobile Mobile;
        public int Distance;
        public int Tier;
        public bool Acquirable;
        public bool AttacksSelf;
        public bool IsPerson;
        public bool Harmless;
        public HostileStats Stats;
        public int SoloThreat;
        public double HitsFraction;
        public int Packmates;
        public bool OutOfSight;
    }

    /// <summary>The blows coming in, read every think from the last scan's foes and the target.</summary>
    private struct ThreatRead
    {
        public long SoonestBlowMs;
        public int Adjacent;
        public int Close;
        public int NearestDistance;
        public int CenterX;
        public int CenterY;
    }

    /// <summary>What the pack holds for the stance rules, read once per scan.</summary>
    private struct Supplies
    {
        public bool HealPotion;
        public bool Bandage;
        public bool HealReagents;
        public bool WardReagents;
        public bool ParalyzeReagents;
    }

    /// <summary>Per-character fight state. Lists and tallies are reused between scans.</summary>
    private sealed class Memory
    {
        public readonly List<Sighting> Foes = new(MaxSightings);
        public readonly List<Mobile> AllyTargets = new(MaxSightings);
        public readonly RoomTally Room = new();
        public readonly RoomTally FoeRoom = new();
        public readonly HostileStats[] One = new HostileStats[1];
        public readonly FightLedger Ledger = new();
        public readonly List<Point3D> StalledEscapes = new();
        public Dictionary<Mobile, int> FoeHits = new();
        public Dictionary<Mobile, int> SeenHits = new();

        public RoomPicture Picture;
        public long NextScanAt;
        public double Nerve;
        public Decision Decision;
        public long FoeLostSince;
        public bool Unreachable;
        public long NextSwitchAt;
        public int Turns;
        public bool PickingOff;
        public Point3D EscapeAt;
        public IPoint3D EscapeGoal;
        public int EscapeStalls;
        public long NextEscapePickAt;
        public int CorneredTicks;
        public long AtBayUntil;

        /// <summary>Cornered by the foe hunting it: the runner cannot outrun it and fights the rest of this fight out (<see cref="RetreatRules.StandsFast"/>).</summary>
        public bool StandsFast;
        public bool FleeOrdered;

        /// <summary>True once this fight's recall out began or was refused for good (see <see cref="RecallOutRules"/>).</summary>
        public bool RecallOutOver;
        public FightTrend Trend;
        public FightOutlook Outlook;
        public Mobile CastTarget;
        public SpellKind CastKind;
        public Item RearmWeapon;
        public long NextCastAt;
        public long NextSpellCareAt;
        public long NextPotionAt;
        public long NextBandageAt;
        public bool Meditating;
        public long NextMeditateAt;
        public readonly List<Item> StashedHands = new(HandCount);
        public readonly InterruptWindow Interrupts = new();
        public ThreatRead Threats;
        public Supplies Kit;
        public long AdjacentSince;
        public long KitingSince;
        public bool Pinned;
        public bool Committed;
        public SpellKind? Planned;
        public long UnreachableSince;
        public Point3D UnreachableFoeAt;
        public int FightId;
        public CombatStance? JevStance;
        public string StanceSource;
        public long StanceUntil;
        public long NextStanceAskAt;
        public string StanceAskKey;
        public bool StanceAskInFlight;
        public int StanceAsks;
        public int CastsThisFight;
        public int BrokenThisFight;

        /// <summary>What the current run started from, null for a room; and whether it is hunting the runner.</summary>
        public Mobile FleeFrom;
        public bool Hunted;

        /// <summary>The last run, kept past the fight: a second one from the same thing soon after is a hunt.</summary>
        public Mobile LastFleeFrom;
        public long LastFleeAt;

        /// <summary>The ground the last run left, and until when fresh fights on it are declined.</summary>
        public Point3D AvoidAt;
        public long AvoidUntil;

        /// <summary>Where the foe stood that the last engage call left alone, and when; zero when it took the fight.</summary>
        public Point3D DeclinedNear;
        public long DeclinedAt;

        /// <summary>The middle of the group a melee pull backs off, and when the draw began.</summary>
        public Point3D? PullFrom;
        public long PullSince;

        public long NextSongAt;

        /// <summary>Drops everything tied to the fight that just ended. Cooldowns stay.</summary>
        public void ForgetFight()
        {
            Foes.Clear();
            AllyTargets.Clear();
            Picture = default;
            NextScanAt = 0;
            Nerve = 0;
            Decision = Decision.None;
            FoeLostSince = 0;
            Unreachable = false;
            Turns = 0;
            PickingOff = false;
            EscapeGoal = null;
            EscapeStalls = 0;
            NextEscapePickAt = 0;
            CorneredTicks = 0;
            AtBayUntil = 0;
            StandsFast = false;
            FleeOrdered = false;
            RecallOutOver = false;
            StalledEscapes.Clear();
            Ledger.Clear();
            FoeHits.Clear();
            SeenHits.Clear();
            Trend = default;
            Outlook = FightOutlook.Unknown;
            Interrupts.Clear();
            Threats = default;
            Kit = default;
            AdjacentSince = 0;
            KitingSince = 0;
            Pinned = false;
            Committed = false;
            Planned = null;
            UnreachableSince = 0;
            FightId++;
            JevStance = null;
            StanceSource = null;
            StanceUntil = 0;
            NextStanceAskAt = 0;
            StanceAskKey = null;
            StanceAskInFlight = false;
            StanceAsks = 0;
            CastsThisFight = 0;
            BrokenThisFight = 0;
            FleeFrom = null;
            Hunted = false;
            PullFrom = null;
            PullSince = 0;
        }
    }
}
