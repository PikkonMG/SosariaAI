using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>
/// A tamer looks after its pets on the world scan: orders, bandages, food, and a fighter
/// claimed from the stables or tamed when none is out, or when a far stronger fighter is
/// in reach. It keeps one fighter and one mount; every other pet goes back to the wild
/// (<see cref="TameRules.Keeps"/>). On the road it lets its pets catch up
/// (<see cref="HoldsForPets"/>); a pet it loses all the same it walks back for, and lets go
/// once lost for <see cref="PetRules.LostPetRelease"/>. It walks its pets into the stables
/// when <see cref="StableRules"/> calls for it (<see cref="StableInSkill"/>) and claims them
/// when the hold ends (<see cref="StableClaimSkill"/>); a gatherer does the same with its pack
/// beast before it logs out. How long each pet has been lost and why its pets wait in the
/// stables are the tamer's own rule clocks, which the save keeps; a stabled pet is lost no
/// more. The retry gaps of its looks (claim, stable, tame, fetch, vet walk), the walk back to a
/// lost pet and the road wait for a pet are in memory only: after a restart the first look is
/// due at once. It looks on the world scan while the owner idles, and between two steps
/// (<see cref="StartsStableErrand"/>).
/// </summary>
public static class PetKeeper
{
    private static readonly ILogger logger = SosariaLog.For(typeof(PetKeeper));
    private static readonly Dictionary<Serial, DateTime> NextClaimAt = new();
    private static readonly Dictionary<Serial, DateTime> NextStableAt = new();
    private static readonly Dictionary<Serial, DateTime> NextTameAt = new();
    private static readonly Dictionary<Serial, DateTime> NextVetWalkAt = new();
    private static readonly Dictionary<Serial, DateTime> NextFetchAt = new();
    private static readonly Dictionary<Serial, Skills.Skill> FetchWalks = new();
    private static readonly Dictionary<Serial, DateTime> PetWaitSince = new();

    /// <summary>Living pets the owner controls, in earshot and on foot.</summary>
    public static List<BaseCreature> PetsOut(SosariaCharacter owner)
    {
        var pets = new List<BaseCreature>();

        foreach (var follower in owner?.AllFollowers ?? [])
        {
            if (IsPetOnFoot(owner, follower) && owner.InRange(follower, PetRules.PetScanRange))
            {
                pets.Add((BaseCreature)follower);
            }
        }

        return pets;
    }

    /// <summary>
    /// The owner's pets alive now, wherever they stand. An owner that never had a pet has no
    /// follower set at all (the engine makes it on the first pet).
    /// </summary>
    public static int LivePets(PlayerMobile owner)
    {
        var alive = 0;

        foreach (var follower in owner?.AllFollowers ?? [])
        {
            alive += follower is BaseCreature { Deleted: false, Alive: true, IsDeadPet: false } ? 1 : 0;
        }

        return alive;
    }

    /// <summary>True when any pet of the owner is out on this map, near or left behind.</summary>
    public static bool HasPetOut(SosariaCharacter owner)
    {
        foreach (var follower in owner?.AllFollowers ?? [])
        {
            if (IsPetOnFoot(owner, follower))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPetOnFoot(Mobile owner, Mobile follower) => IsOwnedPetOnFoot(owner, follower) && follower.Map == owner.Map;

    /// <summary>A living pet of the owner's out in the world on any map, not ridden, not summoned.</summary>
    private static bool IsOwnedPetOnFoot(Mobile owner, Mobile follower) =>
        follower is BaseCreature { Deleted: false, Controlled: true, IsDeadPet: false, Summoned: false } pet &&
        pet.ControlMaster == owner && pet.Map != null && pet.Map != Map.Internal && (pet as IMount)?.Rider == null;

    /// <summary>Every living pet the owner has out, near, left behind or on another map.</summary>
    private static List<BaseCreature> OwnedPetsOnFoot(PlayerMobile owner)
    {
        var pets = new List<BaseCreature>();

        foreach (var follower in owner?.AllFollowers ?? [])
        {
            if (IsOwnedPetOnFoot(owner, follower))
            {
                pets.Add((BaseCreature)follower);
            }
        }

        return pets;
    }

    private static OwnedPet Owned(BaseCreature pet) => new(TamingGrounds.ProfileOf(pet).Power, pet is BaseMount);

    private static List<OwnedPet> Owned(List<BaseCreature> pets) => pets.ConvertAll(Owned);

    /// <summary>
    /// The power of the fighting pet a tamer keeps, out or in the stables; zero with none. A
    /// tamer with a fighter goes for another only when it is far stronger (<see cref="TameRules.IsUpgrade"/>).
    /// </summary>
    public static int FighterPower(Mobile owner)
    {
        if (owner is not PlayerMobile player)
        {
            return 0;
        }

        var pets = Owned(OwnedPetsOnFoot(player));

        foreach (var stabled in player.Stabled ?? [])
        {
            if (stabled is BaseCreature { Deleted: false, IsDeadPet: false } pet)
            {
                pets.Add(Owned(pet));
            }
        }

        return TameRules.FighterPower(pets, owner.Skills.AnimalTaming.Value);
    }

    /// <summary>Which of these pets on foot the owner keeps (<see cref="TameRules.Keeps"/>).</summary>
    private static bool[] KeptOf(SosariaCharacter owner, List<BaseCreature> pets) =>
        TameRules.Keeps(Owned(pets), owner.Mounted, owner.Skills.AnimalTaming.Value);

    /// <summary>True when the owner keeps this pet of its own (<see cref="TameRules.Keeps"/>).</summary>
    public static bool Keeps(SosariaCharacter owner, BaseCreature pet)
    {
        var pets = OwnedPetsOnFoot(owner);
        var index = pets.IndexOf(pet);
        return index >= 0 && KeptOf(owner, pets)[index];
    }

    /// <summary>
    /// The pets a tamer keeps (<see cref="TameRules.Keeps"/>) out on foot on any map: the mouths
    /// it feeds and buys food for. A spare pet goes back to the wild; a pet in the stables eats
    /// nothing.
    /// </summary>
    public static List<BaseCreature> KeptPets(SosariaCharacter owner)
    {
        if (owner == null || !PetRules.KeepsPets(owner.PersonProfile.Class))
        {
            return [];
        }

        var pets = OwnedPetsOnFoot(owner);
        var kept = KeptOf(owner, pets);
        var fed = new List<BaseCreature>();

        for (var i = 0; i < pets.Count; i++)
        {
            if (kept[i])
            {
                fed.Add(pets[i]);
            }
        }

        return fed;
    }

    /// <summary>
    /// Every pet the tamer owns past its one fighter and its one mount goes back to the wild:
    /// a bull too weak for its tier, the drake a dragon replaced, a second horse.
    /// </summary>
    public static void LetGoSparePets(SosariaCharacter owner)
    {
        var pets = OwnedPetsOnFoot(owner);
        var kept = KeptOf(owner, pets);

        for (var i = 0; i < pets.Count; i++)
        {
            if (!kept[i])
            {
                Release(owner, pets[i]);
            }
        }
    }

    /// <summary>
    /// True while a tamer on its way stands for a pet that has dropped back: a pet left past its
    /// own sight stays where it is for good (the engine's follow order), and only pets close by
    /// go through a teleporter with it. A pet that has not caught up in
    /// <see cref="PetRules.MaxPetWait"/> is left to the lost-pet care. Never in a fight or a flight.
    /// </summary>
    public static bool HoldsForPets(SosariaCharacter owner)
    {
        if (owner == null || !PetRules.KeepsPets(owner.PersonProfile.Class))
        {
            return false;
        }

        if (!PetLags(owner))
        {
            PetWaitSince.Remove(owner.Serial);
            return false;
        }

        var now = Core.Now;

        if (!PetWaitSince.TryGetValue(owner.Serial, out var since))
        {
            since = now;
            PetWaitSince[owner.Serial] = since;
        }

        return PetRules.StillWaits(now - since);
    }

    /// <summary>True when the owner walks on its way and a pet told to follow it has dropped back, still in its sight.</summary>
    private static bool PetLags(SosariaCharacter owner)
    {
        if (owner.Motor.Action != CharacterAction.Wander || owner.Routine?.CurrentSkill is FleeSkill)
        {
            return false;
        }

        foreach (var follower in owner.AllFollowers ?? [])
        {
            if (IsPetOnFoot(owner, follower) && follower is BaseCreature { ControlOrder: OrderType.Follow } pet &&
                pet.ControlTarget == owner && PetRules.Lags((int)owner.GetDistanceToSqrt(pet), pet.RangePerception))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The stable errand a pet owner runs between two steps, before the scorer picks the next
    /// one (<see cref="SosariaCharacter.ScoreAndCommit"/>): a lost pet fetched, pets walked into
    /// the stables (<see cref="StablePets"/>), or pets claimed for the next job. The look on the
    /// world scan (<see cref="Consider"/>) sees an owner idle only while it stands about, because
    /// the scorer commits the next step in the tick the last one ends: with that look alone no
    /// tamer ever stabled or claimed. True when an errand started.
    /// </summary>
    public static bool StartsStableErrand(SosariaCharacter owner)
    {
        if (owner is not { Deleted: false, Alive: true } || owner.Map == null || owner.Map == Map.Internal ||
            owner.Motor.Action != CharacterAction.Wander)
        {
            return false;
        }

        if (!PetRules.KeepsPets(owner.PersonProfile.Class))
        {
            return StartsPackBeastErrand(owner);
        }

        var now = Core.Now;

        return LetGoLostPets(owner) ? FetchPet(owner)
            : StablePets(owner, PetsOut(owner), keepsPets: true) ||
              !FighterOut(owner) && NeedOf(owner, fighterOut: false, now) == PetNeed.Claim && StartClaim(owner, now);
    }

    /// <summary>A gatherer walks only its pack beast in, and claims that one back for its next shift. True when the walk starts.</summary>
    private static bool StartsPackBeastErrand(SosariaCharacter owner) =>
        StablePets(owner, PetsOut(owner).FindAll(static pet => PackAnimals.IsPackBeast(pet.GetType())), keepsPets: false) ||
        ClaimPackBeast(owner);

    /// <summary>One look after the pets. Runs on the world scan.</summary>
    public static void Consider(SosariaCharacter owner)
    {
        if (owner is not { Deleted: false, Alive: true } || owner.Map == null || owner.Map == Map.Internal)
        {
            return;
        }

        if (!PetRules.KeepsPets(owner.PersonProfile.Class))
        {
            StartsPackBeastErrand(owner);
            return;
        }

        var fighting = owner.Motor.Action != CharacterAction.Wander;

        if (!fighting)
        {
            LetGoSparePets(owner);
        }

        var pets = PetsOut(owner);
        var petLost = LetGoLostPets(owner);

        if (!HasPetOut(owner))
        {
            SeekPet(owner, fighterOut: false);
            return;
        }

        if (pets.Count == 0)
        {
            FetchPet(owner);
            return;
        }

        PetOrders.Direct(owner, force: false);

        for (var i = 0; i < pets.Count; i++)
        {
            var pet = pets[i];

            if (pet.Deleted || pet.ControlMaster != owner)
            {
                continue;
            }

            if (PetRules.NeedsVet(pet.Hits, pet.HitsMax, pet.Poisoned) && Tend(owner, pet, fighting))
            {
                return;
            }

            if (!fighting && PetRules.NeedsFeed(pet.Loyalty) && owner.InRange(pet, PetRules.FeedRange))
            {
                Feed(owner, pet);
            }
        }

        if (fighting)
        {
            return;
        }

        if (petLost)
        {
            FetchPet(owner);
            return;
        }

        if (!StablePets(owner, pets, keepsPets: true))
        {
            SeekPet(owner, FighterOut(owner));
        }
    }

    /// <summary>True when a pet strong enough to fight beside the tamer is out (<see cref="TameRules.FighterPower"/>).</summary>
    private static bool FighterOut(SosariaCharacter owner) =>
        TameRules.FighterPower(Owned(OwnedPetsOnFoot(owner)), owner.Skills.AnimalTaming.Value) > 0;

    /// <summary>
    /// An idle owner walks pets into the stables when <see cref="StableRules.ReasonToStable"/>
    /// calls for it: for a tamer, a badly hurt pet with no care at hand, or every pet on foot when its slots are too full for the beast it would tame next.
    /// A loaded pack beast stays out: the trainer takes none. True when the walk starts.
    /// </summary>
    private static bool StablePets(SosariaCharacter owner, List<BaseCreature> pets, bool keepsPets)
    {
        var now = Core.Now;

        if (pets.Count == 0 || !WorldPlay.IsIdle(owner) || owner.Motor.Action != CharacterAction.Wander ||
            owner.RestsSkill(StableInSkill.SkillName) ||
            NextStableAt.TryGetValue(owner.Serial, out var stableAt) && now < stableAt)
        {
            return false;
        }

        var stableable = pets.FindAll(static pet => !IsLoadedPackBeast(pet));
        var hurt = keepsPets ? stableable.FindAll(pet => NeedsRest(owner, pet)) : [];
        var reason = StableRules.ReasonToStable(
            StableRules.SinceClaim(owner.ClockAt(RuleClock.PetsClaimed), now),
            hurt.Count > 0,
            () => keepsPets && SlotsBlockTaming(owner, stableable, now)
        );
        var chosen = reason == StableReason.Rest ? hurt : stableable;

        if (reason == StableReason.None || chosen.Count == 0)
        {
            return false;
        }

        // A reason with no stable in reach looks again after the retry, as a failed walk does.
        NextStableAt[owner.Serial] = now + StableRules.StableRetry;

        if (StableWalk.Find(owner, StableInSkill.SkillName) == null)
        {
            return false;
        }

        WorldPlay.StartWork(owner, new StableInSkill(reason, chosen));
        return true;
    }

    /// <summary>
    /// True when the tamer's taming is due and no ground holds a beast its slots take now, while
    /// one would with these pets in the stables: a dragon and a ridden horse fill four of five
    /// slots, and the drakes a master practises on take two. With no ground either way the next
    /// look waits a taming retry; with one, the stable retry paces it.
    /// </summary>
    private static bool SlotsBlockTaming(SosariaCharacter owner, List<BaseCreature> pets, DateTime now)
    {
        if (pets.Count == 0 || !StableRules.SlotsMayBlock(owner.Followers, owner.FollowersMax) || !TameDue(owner, now) ||
            TamingGrounds.Pick(owner) != null)
        {
            return false;
        }

        var freed = 0;

        for (var i = 0; i < pets.Count; i++)
        {
            freed += pets[i].ControlSlots;
        }

        if (TamingGrounds.Ranked(owner, owner.Map, owner.HomeSpot, 1, freed, liveOnly: true).Count > 0)
        {
            return true;
        }

        NextTameAt[owner.Serial] = now + PetRules.TameRetry;
        return false;
    }

    /// <summary>A badly hurt pet the tamer has neither a bandage nor a heal spell for (<see cref="StableRules.NeedsRest"/>).</summary>
    private static bool NeedsRest(SosariaCharacter owner, BaseCreature pet) =>
        StableRules.NeedsRest(
            pet.Hits,
            pet.HitsMax,
            pet.Poisoned,
            owner.Backpack?.FindItemByType<Bandage>() != null,
            PetRules.PetSpell(poisoned: false, wounded: true, owner.Skills.Magery.Value, owner.ManaMax) != null
        );

    /// <summary>The engine's trainer refuses a pack beast with a load in its pack.</summary>
    private static bool IsLoadedPackBeast(BaseCreature pet) =>
        pet is PackLlama or PackHorse or Beetle && pet.Backpack?.Items.Count > 0;

    /// <summary>What keeps the owner's pets in the stables now (<see cref="StableRules.Hold"/>).</summary>
    private static StableHold HoldOf(SosariaCharacter owner, DateTime now) =>
        StableRules.Hold(
            now,
            owner.ClockAt(RuleClock.StabledToRest),
            RestingPetHurt(owner),
            owner.ClockAt(RuleClock.StabledForTaming),
            owner.ClockAt(RuleClock.TamingSession)
        );

    /// <summary>True while a pet in the owner's stables is short of its hits (<see cref="StableRules.StillHurt"/>).</summary>
    private static bool RestingPetHurt(SosariaCharacter owner)
    {
        foreach (var stabled in owner.Stabled ?? [])
        {
            if (stabled is BaseCreature { Deleted: false } pet && StableRules.StillHurt(pet.Hits, pet.HitsMax))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The trainer took the pets: the hold of the reason starts.</summary>
    public static void NoteStabled(SosariaCharacter owner, StableReason reason, DateTime now)
    {
        switch (reason)
        {
            case StableReason.Taming:
            {
                owner.StartClock(RuleClock.StabledForTaming, now);
                return;
            }
            case StableReason.Rest:
            {
                owner.StartClock(RuleClock.StabledToRest, now);
                return;
            }
        }
    }

    /// <summary>The pets came out: every hold ends, and no stable-in comes before <see cref="StableRules.RestableGap"/>.</summary>
    public static void NoteClaim(SosariaCharacter owner, DateTime now)
    {
        owner.StopClock(RuleClock.StabledForTaming);
        owner.StopClock(RuleClock.StabledToRest);
        owner.StartClock(RuleClock.PetsClaimed, now);
    }

    private static bool TameDue(SosariaCharacter owner, DateTime now) =>
        (!NextTameAt.TryGetValue(owner.Serial, out var tameAt) || now >= tameAt) && !owner.RestsSkill(SkillKinds.Tame);

    /// <summary>
    /// Notes each pet out of the tamer's earshot, here or on another map, and lets go one lost
    /// for <see cref="PetRules.LostPetRelease"/>, or <see cref="PetRules.LostPetFetchRelease"/>
    /// while the tamer walks back for it: the tamer could not get it back, and a pet left behind
    /// stands where it was for good. True while a pet is still lost.
    /// </summary>
    private static bool LetGoLostPets(SosariaCharacter owner)
    {
        var now = Core.Now;
        List<string> lost = null;
        var pets = OwnedPetsOnFoot(owner);
        var fetching = Fetches(owner);

        for (var i = 0; i < pets.Count; i++)
        {
            var pet = pets[i];

            if (pet.Map == owner.Map && owner.InRange(pet, PetRules.PetScanRange))
            {
                continue;
            }

            var clock = RuleClock.LostPet(pet.Serial);
            var since = owner.ClockAt(clock);

            if (since == default)
            {
                since = now;
                owner.StartClock(clock, since);
            }

            if (PetRules.GivesUpLostPet(now - since, fetching))
            {
                Release(owner, pet);
                continue;
            }

            (lost ??= []).Add(clock);
        }

        // A pet found again, let go, stabled or gone is lost no more.
        owner.StopClocks(RuleClock.LostPetPrefix, lost);
        return lost != null;
    }

    /// <summary>
    /// True when a recall would strand a tamer's pet: the engine's recall takes only bonded
    /// pets along, and pets bond only from Lord Blackthorn's Revenge on.
    /// </summary>
    public static bool RecallStrandsPets(SosariaCharacter owner)
    {
        if (owner == null || !PetRules.KeepsPets(owner.PersonProfile.Class))
        {
            return false;
        }

        return PetsOut(owner).Exists(static pet => !pet.IsBonded);
    }

    /// <summary>
    /// How hard the owner's pets out beside it fight, on the scale the danger check rates foes
    /// by: a tamer with a dragon at heel stands where a lone mage would run.
    /// </summary>
    public static int PetsPower(SosariaCharacter owner)
    {
        if (owner == null || !PetRules.KeepsPets(owner.PersonProfile.Class))
        {
            return 0;
        }

        var power = 0;
        var pets = PetsOut(owner);

        for (var i = 0; i < pets.Count; i++)
        {
            power += TameRules.BeastPower(pets[i].Hits, pets[i].Str, pets[i].DamageMin, pets[i].DamageMax);
        }

        return power;
    }

    /// <summary>
    /// Lets a pet go the way the engine's release gump does on "continue": the pet goes back
    /// to the wild. A pet that would not obey its owner is not let go. The tame took it off
    /// its spawner, which has spawned its place again, so a let-go beast with no spawner is
    /// gone <see cref="PetRules.ReleasedBeastLinger"/> later; the engine keeps one three days,
    /// and practice tames added 530 beasts to the world in one night.
    /// </summary>
    public static void Release(SosariaCharacter owner, BaseCreature pet)
    {
        if (pet is not { Deleted: false, Controlled: true } || pet.ControlMaster != owner || !pet.CanBeControlledBy(owner))
        {
            return;
        }

        var kind = pet.GetType().Name;
        pet.IssueOrder(OrderType.Release, owner);

        if (pet is { Deleted: false, Controlled: false, Spawner: null })
        {
            Timer.StartTimer(PetRules.ReleasedBeastLinger, () => RemoveIfStillWild(pet));
        }

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} let its {Beast} go at {Location}", owner.Name, kind, owner.Location);
        }
    }

    /// <summary>Deletes a let-go beast that nobody tamed again. True when it went.</summary>
    internal static bool RemoveIfStillWild(BaseCreature beast)
    {
        if (!PetRules.LeavesAfterRelease(beast.Deleted, beast.Controlled))
        {
            return false;
        }

        beast.Delete();
        return true;
    }

    /// <summary>
    /// A pet of its own is out on this map past its earshot: an idle tamer walks back to the
    /// nearest such pet, which then hears "all follow me". A pet stays put once its master is
    /// out of its sight. True when the walk starts.
    /// </summary>
    private static bool FetchPet(SosariaCharacter owner)
    {
        var now = Core.Now;

        if (!WorldPlay.IsIdle(owner) || owner.Motor.Action != CharacterAction.Wander ||
            NextFetchAt.TryGetValue(owner.Serial, out var fetchAt) && now < fetchAt)
        {
            return false;
        }

        Mobile nearest = null;

        foreach (var follower in owner.AllFollowers ?? [])
        {
            if (IsPetOnFoot(owner, follower) && !owner.InRange(follower, PetRules.PetScanRange) &&
                (nearest == null || owner.GetDistanceToSqrt(follower) < owner.GetDistanceToSqrt(nearest)))
            {
                nearest = follower;
            }
        }

        if (nearest == null)
        {
            return false;
        }

        var walk = new TravelSkill(nearest.Location, PetRules.FetchRange);
        NextFetchAt[owner.Serial] = now + PetRules.FetchRetry;
        FetchWalks[owner.Serial] = walk;
        WorldPlay.StartWork(owner, walk);
        return true;
    }

    /// <summary>True while the owner's step is the walk back to a lost pet (<see cref="FetchPet"/>).</summary>
    private static bool Fetches(SosariaCharacter owner)
    {
        if (!FetchWalks.TryGetValue(owner.Serial, out var walk))
        {
            return false;
        }

        if (owner.Routine is { NeedsNext: false } routine && ReferenceEquals(routine.CurrentSkill, walk))
        {
            return true;
        }

        FetchWalks.Remove(owner.Serial);
        return false;
    }

    /// <summary>
    /// A pet in reach gets a bandage now; one farther off gets a heal or cure spell, in a fight
    /// too (<see cref="CombatBrain.CastOnPet"/>); out of a fight, an idle tamer with no spell for
    /// it walks over with a bandage.
    /// </summary>
    private static bool Tend(SosariaCharacter owner, BaseCreature pet, bool fighting)
    {
        if (VetBandage.IsTending(owner, pet) || VetBandage.TryApply(owner, pet) || CombatBrain.CastOnPet(owner, pet))
        {
            return true;
        }

        if (fighting || !WorldPlay.IsIdle(owner) || owner.Backpack?.FindItemByType<Bandage>() == null ||
            NextVetWalkAt.TryGetValue(owner.Serial, out var walkAt) && Core.Now < walkAt)
        {
            return false;
        }

        NextVetWalkAt[owner.Serial] = Core.Now + PetRules.VetWalkRetry;
        WorldPlay.StartWork(owner, new VetSkill(pet));
        return true;
    }

    /// <summary>
    /// A mouthful of the food the pet likes, dropped on it as a player would. Each feed is one
    /// activity line: a pet is fed only once its loyalty drops (<see cref="PetRules.NeedsFeed"/>).
    /// </summary>
    private static void Feed(SosariaCharacter owner, BaseCreature pet)
    {
        var food = FoodFor(owner, pet);

        if (food == null)
        {
            return;
        }

        if (food.Amount > PetRules.FeedAmount)
        {
            Mobile.LiftItemDupe(food, PetRules.FeedAmount);
        }

        var meal = food.GetType().Name;

        if (pet.OnDragDrop(owner, food) && SosariaSettings.LogActivity)
        {
            logger.Information("{Name} fed its {Beast} {Food} at {Location}", owner.Name, pet.GetType().Name, meal, owner.Location);
        }
    }

    /// <summary>
    /// The units of this stack a tamer keeps as pet food, never sold: up to the pantry target
    /// (<see cref="PetFoodRules.PantryTarget"/>) when a pet it keeps, out or in the stables, eats
    /// it. Food none of its pets eats is goods like any other.
    /// </summary>
    public static int FoodKept(SosariaCharacter owner, Item stack)
    {
        if (!IsPetFood(stack) || owner == null || !PetRules.KeepsPets(owner.PersonProfile.Class))
        {
            return 0;
        }

        if (KeptPets(owner).Exists(pet => pet.CheckFoodPreference(stack)))
        {
            return PetFoodRules.PantryTarget;
        }

        foreach (var stabled in owner.Stabled ?? [])
        {
            if (stabled is BaseCreature { Deleted: false, IsDeadPet: false } pet && pet.CheckFoodPreference(stack))
            {
                return PetFoodRules.PantryTarget;
            }
        }

        return 0;
    }

    /// <summary>Units in the owner's pack the pet would eat: what <see cref="Feed"/> has left to give it.</summary>
    public static int FoodOnHand(SosariaCharacter owner, BaseCreature pet)
    {
        var units = 0;

        foreach (var item in owner?.Backpack?.Items ?? [])
        {
            units += Eats(pet, item) ? item.Amount : 0;
        }

        return units;
    }

    private static bool IsPetFood(Item item) => item is Food or CookableFood;

    // Raw ribs are cookable food, not food: the engine's pets eat both.
    private static bool Eats(BaseCreature pet, Item item) => IsPetFood(item) && !item.Deleted && pet.CheckFoodPreference(item);

    private static Item FoodFor(SosariaCharacter owner, BaseCreature pet)
    {
        foreach (var item in owner.Backpack?.Items ?? [])
        {
            if (Eats(pet, item))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// A miner or woodcutter whose pack beast waits in the stables claims it when idle, as
    /// a gatherer led its llama out before the shift. True when the walk starts.
    /// </summary>
    private static bool ClaimPackBeast(SosariaCharacter owner)
    {
        var now = Core.Now;

        return WorldPlay.IsIdle(owner) && owner.Motor.Action == CharacterAction.Wander &&
               PackAnimals.BeastOf(owner) == null && PackAnimals.IsGatherer(owner) &&
               PackAnimals.HasStabledBeast(owner) && ClaimDue(owner, now) &&
               StableWalk.Find(owner, StableClaimSkill.SkillName) != null && StartClaim(owner, now);
    }

    /// <summary>A claim walk is due: none began within <see cref="PetRules.ClaimRetry"/>, and claims do not rest after repeated failure.</summary>
    private static bool ClaimDue(SosariaCharacter owner, DateTime now) =>
        (!NextClaimAt.TryGetValue(owner.Serial, out var claimAt) || now >= claimAt) &&
        !owner.RestsSkill(StableClaimSkill.SkillName);

    /// <summary>The walk to the stables to claim the pets starts. Always true.</summary>
    private static bool StartClaim(SosariaCharacter owner, DateTime now)
    {
        NextClaimAt[owner.Serial] = now + PetRules.ClaimRetry;
        WorldPlay.StartWork(owner, new StableClaimSkill());
        return true;
    }

    /// <summary>
    /// What an idle tamer does about its pets (<see cref="PetRules.NextNeed"/>): with no fighter
    /// out it claims its pets from the stables once their hold is over (<see cref="StableRules.Hold"/>),
    /// or goes taming; with a fighter out it goes again while a strong beast fits its free slots.
    /// </summary>
    private static PetNeed NeedOf(SosariaCharacter owner, bool fighterOut, DateTime now)
    {
        if (!WorldPlay.IsIdle(owner) || owner.Motor.Action != CharacterAction.Wander)
        {
            return PetNeed.None;
        }

        var stabled = owner.Stabled?.Count ?? 0;
        var hold = HoldOf(owner, now);

        // A job that cools down after repeated failure is not started around the planner either.
        var tameDue = TameDue(owner, now);

        return PetRules.NextNeed(
            stabled,
            stableInReach: stabled > 0 && !fighterOut && hold == StableHold.None &&
                           StableWalk.Find(owner, StableClaimSkill.SkillName) != null,
            fighterOut,
            ClaimDue(owner, now),
            tameDue,
            fighterOut && tameDue && hold == StableHold.None && WantsStrongerPets(owner),
            hold
        );
    }

    /// <summary>An idle tamer claims its pets or goes taming (<see cref="NeedOf"/>).</summary>
    private static void SeekPet(SosariaCharacter owner, bool fighterOut)
    {
        var now = Core.Now;

        switch (NeedOf(owner, fighterOut, now))
        {
            case PetNeed.Claim:
            {
                StartClaim(owner, now);
                return;
            }
            case PetNeed.Tame:
            {
                NextTameAt[owner.Serial] = now + PetRules.TameRetry;

                // With no beast in sight and no ground to go to, it looks again after the retry.
                if (TameSkill.CanSeek(owner))
                {
                    WorldPlay.StartWork(owner, new TameSkill());
                }

                return;
            }
        }
    }

    /// <summary>
    /// True when the ground the tamer would go to holds a beast it wants to keep: strong for its
    /// tier and far stronger than the fighter it has (<see cref="TameRules.IsWantedKeeper"/>).
    /// </summary>
    private static bool WantsStrongerPets(SosariaCharacter owner) =>
        owner.Followers < owner.FollowersMax && TamingGrounds.Pick(owner) is { } ground &&
        TameRules.IsWantedKeeper(ground.Beast.Power, TamingGrounds.FactsOf(owner));
}
