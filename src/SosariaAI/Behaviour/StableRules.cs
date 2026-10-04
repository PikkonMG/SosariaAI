using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Behaviour;

/// <summary>Why a tamer walks its pets into the stables.</summary>
public enum StableReason
{
    None,

    /// <summary>Its follower slots are too full for the beast it would tame next.</summary>
    Taming,

    /// <summary>A pet is badly hurt and the tamer has neither bandage nor heal spell for it.</summary>
    Rest
}

/// <summary>What keeps a tamer's pets in the stables for now.</summary>
public enum StableHold
{
    None,

    /// <summary>The pets wait while the tamer tames with the slots they freed.</summary>
    Taming,

    /// <summary>A hurt pet rests until it is well or its rest is over.</summary>
    Rest
}

/// <summary>
/// When a 1999 tamer put its pets into the stables and took them out again, on the engine's own
/// animal trainer: "stable" and the pet under the cursor, 30 gold a pet from the pack or the bank,
/// as many pets as the trainer's own count of its Taming, Lore and Veterinary allows; "claim"
/// brings every stabled pet out while the follower slots hold it. A tamer stables its pets when
/// its slots are too full for the beast it would tame next, and when a pet is badly hurt with no
/// bandage or heal spell for it. It claims them when the hold ends: when its taming session
/// closes, when the hurt pet is well again. A tamer
/// that just claimed stables again only after <see cref="RestableGap"/>, so the pets never go in
/// and out on one errand. The holds read the tamer's own rule clocks, so a restart keeps them.
/// </summary>
public static class StableRules
{
    public const string StableLine = "stable";
    public const int StableKeyword = 0x0008;

    /// <summary>The engine's animal trainer takes this much gold for each pet (AnimalTrainer.EndStable).</summary>
    public const int StableFee = 30;

    /// <summary>
    /// The trainer hears the tamer this far off: the reach of its own stable and claim entries.
    /// A walk that stops at the edge of the shop's arrival box is not yet across the counter:
    /// the trainer roams five tiles round its spawn spot, and 23 of 32 claims stood seven tiles
    /// from where it had been with "no animal trainer at the stable".
    /// </summary>
    public const int TrainerHearingTiles = 12;

    /// <summary>A tamer that has not reached the trainer's hearing walks up to it this close.</summary>
    public const int TrainerCloseTiles = 2;

    /// <summary>After a claim the tamer stables again only this long later.</summary>
    public static readonly TimeSpan RestableGap = TimeSpan.FromMinutes(30);

    /// <summary>A stable trip starts at most this often.</summary>
    public static readonly TimeSpan StableRetry = TimeSpan.FromMinutes(10);

    /// <summary>The pets wait in the stables this long at most while the tamer tames: a trip lasts half an hour.</summary>
    public static readonly TimeSpan TamingHold = TimeSpan.FromMinutes(45);

    /// <summary>A hurt pet rests at least this long, and at most <see cref="PetRestMax"/>.</summary>
    public static readonly TimeSpan PetRestMin = TimeSpan.FromMinutes(10);

    public static readonly TimeSpan PetRestMax = TimeSpan.FromHours(1);

    /// <summary>A pet below this share of its hits, with no care at hand, goes to the stables to rest.</summary>
    public const double RestBelowFraction = 0.5;

    /// <summary>A resting pet at this share of its hits is well again.</summary>
    public const double RestedFraction = 0.9;

    /// <summary>The most follower slots a beast of this era takes: a dragon, a white wyrm.</summary>
    public const int MostBeastSlots = 3;

    /// <summary>
    /// What keeps the pets in: a hurt pet's rest that has not run out, a taming trip whose
    /// session has not closed since the pets went in.
    /// </summary>
    public static StableHold Hold(
        DateTime now,
        DateTime stabledToRest,
        bool restingPetHurt,
        DateTime stabledForTaming,
        DateTime tamingSessionClosed
    )
    {
        if (stabledToRest != default && now - stabledToRest < PetRestMax &&
            (now - stabledToRest < PetRestMin || restingPetHurt))
        {
            return StableHold.Rest;
        }

        return stabledForTaming != default && now - stabledForTaming < TamingHold && tamingSessionClosed <= stabledForTaming
            ? StableHold.Taming
            : StableHold.None;
    }

    /// <summary>
    /// Why the tamer stables its pets now, or <see cref="StableReason.None"/>: not within
    /// <see cref="RestableGap"/> of its last claim; a hurt pet before a full set of slots. The
    /// slot look reads the taming grounds, so it runs only when asked.
    /// </summary>
    public static StableReason ReasonToStable(TimeSpan sinceClaim, bool petNeedsRest, Func<bool> slotsBlockTaming)
    {
        if (sinceClaim < RestableGap)
        {
            return StableReason.None;
        }

        return petNeedsRest ? StableReason.Rest
            : slotsBlockTaming() ? StableReason.Taming
            : StableReason.None;
    }

    /// <summary>How long since the last claim; forever with none.</summary>
    public static TimeSpan SinceClaim(DateTime claimedAt, DateTime now) => claimedAt == default ? TimeSpan.MaxValue : now - claimedAt;

    /// <summary>A pet badly hurt, not poisoned (the poison would run on in the stables), with no bandage and no heal spell for it.</summary>
    public static bool NeedsRest(int hits, int hitsMax, bool poisoned, bool hasBandage, bool hasHealSpell) =>
        !poisoned && !hasBandage && !hasHealSpell && hitsMax > 0 && hits < hitsMax * RestBelowFraction;

    /// <summary>A resting pet still short of <see cref="RestedFraction"/> of its hits.</summary>
    public static bool StillHurt(int hits, int hitsMax) => hitsMax > 0 && hits < hitsMax * RestedFraction;

    /// <summary>
    /// True when a spare slot count can still be too few: with <see cref="MostBeastSlots"/> free,
    /// every beast of this era fits and no ground needs to be looked over for room.
    /// </summary>
    public static bool SlotsMayBlock(int followers, int followersMax) => followersMax - followers < MostBeastSlots;

    /// <summary>How many more pets the trainer takes: its limit less the pets already in.</summary>
    public static int StableRoom(int maxStabled, int stabled) => Math.Max(0, maxStabled - stabled);

    /// <summary>True when the tamer's pack and bank gold pay the fee for each pet.</summary>
    public static bool CanPay(int gold, int pets) => pets > 0 && gold >= StableFee * pets;

    /// <summary>The line a stable-in writes, easy to count.</summary>
    public static string StabledLine(string name, IReadOnlyList<string> pets, Point3D stable, StableReason reason) =>
        $"{name} stabled {PetCount(pets)} at {stable} {Purpose(reason)}: {string.Join(", ", pets)}";

    /// <summary>The line a claim-out writes, easy to count.</summary>
    public static string ClaimedLine(string name, IReadOnlyList<string> pets, Point3D stable) =>
        $"{name} claimed {PetCount(pets)} at {stable}: {string.Join(", ", pets)}";

    private static string PetCount(IReadOnlyList<string> pets) => pets.Count == 1 ? "1 pet" : $"{pets.Count} pets";

    private static string Purpose(StableReason reason) =>
        reason switch
        {
            StableReason.Taming => "to free its slots for taming",
            StableReason.Rest   => "to rest a hurt pet",
            _                   => throw new ArgumentOutOfRangeException(nameof(reason), reason, "A stable-in always has a reason.")
        };
}
