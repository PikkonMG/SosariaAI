using System;
using Server;
using SosariaAI.Combat;
using SosariaAI.Spawning;

namespace SosariaAI.Behaviour;

/// <summary>What a tamer tells its pets out loud.</summary>
public enum PetCommand
{
    None,
    Kill,
    Guard,
    Follow,
    Stay
}

/// <summary>What a tamer and its pets are doing, for the order it gives them.</summary>
public readonly record struct PetMoment(
    bool InFight,
    bool MayAttackFoe,
    bool AllOnFoe,
    bool AllGuarding,
    bool Taming,
    bool IdleInTown,
    bool AllFollowing,
    bool AllStaying
);

/// <summary>What a tamer with no pet out does about it.</summary>
public enum PetNeed
{
    None,
    Claim,
    Tame
}

/// <summary>
/// A tamer keeps its pets at heel: "all kill" when a fight starts, "all follow me" when it
/// ends, "all stay" while it idles in town or works a wild beast. On the road it stands while
/// a pet catches up (<see cref="Lags"/>), and a pet lost past <see cref="LostPetRelease"/> is
/// let go. It bandages and feeds the pets, claims them back from the stables, tames a new one
/// when it has none, and goes out again only for a far stronger fighter. The words are the
/// engine's own pet keywords, so the pets obey them as they obey a player.
/// </summary>
public static class PetRules
{
    public const string AllKillLine = "all kill";
    public const int AllKillKeyword = 0x168;
    public const string AllGuardMeLine = "all guard me";
    public const int AllGuardMeKeyword = 0x16B;
    public const string AllFollowMeLine = "all follow me";
    public const int AllFollowMeKeyword = 0x16C;
    public const string AllStayLine = "all stay";
    public const int AllStayKeyword = 0x170;
    public const string ClaimLine = "claim";
    public const int ClaimKeyword = 0x0009;

    /// <summary>Pets this close hear their owner. The engine's named-command reach is fourteen tiles.</summary>
    public const int PetScanRange = 14;

    public const int FeedRange = 2;
    public const double VetBelowFraction = 0.7;

    /// <summary>A tamer's heal or cure spell reaches its pet this far off, in sight: the engine's spell range.</summary>
    public const int SpellReachTiles = 12;

    /// <summary>Pre-AOS loyalty runs 0 to 100 and drops ten an hour; a pet under this is fed.</summary>
    public const int FeedBelowLoyalty = 70;

    public const int FeedAmount = 2;

    public static readonly TimeSpan CommandGap = TimeSpan.FromSeconds(8);

    /// <summary>A pet left beyond earshot is walked back to at most this often, and the walk ends this close to it.</summary>
    public static readonly TimeSpan FetchRetry = TimeSpan.FromMinutes(5);

    public const int FetchRange = 3;

    /// <summary>A walk over to bandage a pet out of reach starts at most this often.</summary>
    public static readonly TimeSpan VetWalkRetry = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan ClaimRetry = TimeSpan.FromMinutes(15);

    /// <summary>A tamer sets out on a taming trip at most this often.</summary>
    public static readonly TimeSpan TameRetry = TimeSpan.FromMinutes(10);

    /// <summary>
    /// A pet following farther back than this has dropped back: the engine's teleporter takes
    /// along only pets this close to their master (BaseCreature.TeleportPets).
    /// </summary>
    public const int PetLagTiles = 3;

    /// <summary>A tamer on its way stands this long at most for a pet that dropped back.</summary>
    public static readonly TimeSpan MaxPetWait = TimeSpan.FromSeconds(15);

    /// <summary>A pet out of its tamer's earshot this long is one it could not get back: it is let go.</summary>
    public static readonly TimeSpan LostPetRelease = TimeSpan.FromMinutes(15);

    /// <summary>
    /// While its tamer walks back for it, a lost pet is let go only this long after it was lost:
    /// the walk back from town to a dungeon floor takes longer than <see cref="LostPetRelease"/>.
    /// </summary>
    public static readonly TimeSpan LostPetFetchRelease = TimeSpan.FromMinutes(30);

    /// <summary>A following pet this far off has dropped back but still sees its master, so it still follows.</summary>
    public static bool Lags(int distance, int petSight) => distance > PetLagTiles && distance <= petSight;

    /// <summary>True while the tamer still stands for its pet to catch up.</summary>
    public static bool StillWaits(TimeSpan waited) => waited < MaxPetWait;

    /// <summary>True once a pet has been lost long enough for its tamer to let it go; longer while the tamer walks back for it.</summary>
    public static bool GivesUpLostPet(TimeSpan lostFor, bool fetching) => lostFor >= (fetching ? LostPetFetchRelease : LostPetRelease);

    /// <summary>How long a let-go beast with no spawner roams before it leaves the world.</summary>
    public static readonly TimeSpan ReleasedBeastLinger = TimeSpan.FromMinutes(1);

    /// <summary>A let-go beast leaves the world unless someone tamed it again meanwhile.</summary>
    public static bool LeavesAfterRelease(bool deleted, bool controlled) => !deleted && !controlled;

    public static bool KeepsPets(PersonClass personClass) => personClass == PersonClass.Tamer;

    /// <summary>
    /// Under the guards a pet goes only for a foe the owner may lawfully strike: a criminal, a
    /// murderer, an enemy, or one that attacked first. Out of town any foe will do.
    /// </summary>
    public static bool MayAttackFoe(bool underGuards, int notoriety) =>
        !underGuards || notoriety is not (Notoriety.Innocent or Notoriety.Ally or Notoriety.Invulnerable);

    /// <summary>
    /// The spell a tamer casts on its hurt pet from where it stands, or null: Cure on a poisoned
    /// pet, Greater Heal on a wounded one, and Heal while Greater Heal is past its Magery or mana.
    /// A tamer kept its dragon up from the back of the fight with the spell and closed in with a
    /// bandage (Veterinary) when the pet stood near; with the bandage alone, pets in a melee two
    /// tiles past the bandage's reach were never healed.
    /// </summary>
    public static SpellKind? PetSpell(bool poisoned, bool wounded, double magery, int mana)
    {
        if (poisoned && Casts(SpellBook.Cure, magery, mana))
        {
            return SpellKind.Cure;
        }

        if (!wounded)
        {
            return null;
        }

        return Casts(SpellBook.GreaterHeal, magery, mana) ? SpellKind.GreaterHeal
            : Casts(SpellBook.Heal, magery, mana) ? SpellKind.Heal
            : null;
    }

    private static bool Casts(SpellEntry spell, double magery, int mana) => magery >= spell.MinMagery && mana >= spell.Mana;

    public static bool NeedsVet(int hits, int hitsMax, bool poisoned) =>
        poisoned || hitsMax > 0 && hits < hitsMax * VetBelowFraction;

    public static bool NeedsFeed(int loyalty) => loyalty < FeedBelowLoyalty;

    /// <summary>
    /// In a fight every pet goes for the owner's foe, unless the guards would take a pet set on
    /// an innocent: then they guard their owner. Out of one they stay put while the owner works
    /// a wild beast, so none of them wounds it and breaks the taming, or while it idles under
    /// the guards; else they follow. Nothing is said when they already obey.
    /// </summary>
    public static PetCommand Wanted(PetMoment moment)
    {
        if (moment.InFight)
        {
            if (!moment.MayAttackFoe)
            {
                return moment.AllGuarding ? PetCommand.None : PetCommand.Guard;
            }

            return moment.AllOnFoe ? PetCommand.None : PetCommand.Kill;
        }

        if (moment.Taming || moment.IdleInTown)
        {
            return moment.AllStaying ? PetCommand.None : PetCommand.Stay;
        }

        return moment.AllFollowing ? PetCommand.None : PetCommand.Follow;
    }

    /// <summary>
    /// A tamer with no fighter out claims its pets from a stable in reach, else tames one: Runa
    /// stood in Jhelom, with no trainer within her leash, and failed "no stable in reach"
    /// sixteen times. One with a fighter out goes taming again while its slots have room for a
    /// strong beast it can take. A stable hold (<see cref="StableRules.Hold"/>) comes first: a
    /// tamer whose hurt pet rests waits for it, and one whose pets wait for its taming trip tames.
    /// </summary>
    public static PetNeed NextNeed(
        int stabled,
        bool stableInReach,
        bool fighterOut,
        bool claimDue,
        bool tameDue,
        bool wantsMore,
        StableHold hold
    )
    {
        if (fighterOut)
        {
            return tameDue && wantsMore ? PetNeed.Tame : PetNeed.None;
        }

        if (hold == StableHold.Rest)
        {
            return PetNeed.None;
        }

        if (hold == StableHold.None && stabled > 0 && stableInReach)
        {
            return claimDue ? PetNeed.Claim : PetNeed.None;
        }

        return tameDue ? PetNeed.Tame : PetNeed.None;
    }
}
