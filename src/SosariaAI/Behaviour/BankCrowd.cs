using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.SkillHandlers;
using SosariaAI.Combat;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using SosariaAI.Spawning;

namespace SosariaAI.Behaviour;

/// <summary>One place in a bank crowd: who holds it, in what role, where it stands.</summary>
public sealed class BankCrowdSeat
{
    public BankCrowdSeat(SosariaCharacter member, string bankKey, Point3D bank, BankCrowdRole role, int seat, Point3D spot)
    {
        Member = member;
        BankKey = bankKey;
        Bank = bank;
        Role = role;
        Seat = seat;
        Spot = spot;
        Since = Core.Now;
    }

    public SosariaCharacter Member { get; }

    public string BankKey { get; }

    public Point3D Bank { get; }

    public BankCrowdRole Role { get; }

    public int Seat { get; }

    public Point3D Spot { get; }

    public DateTime Since { get; }

    /// <summary>True once the member has walked to its bank.</summary>
    public bool Arrived { get; private set; }

    public void MarkArrived() => Arrived = true;
}

/// <summary>What a hawker holds up and what it asks: the real item and the spoken price.</summary>
public readonly record struct HawkerOffer(Serial Item, int Asking, string Noun);

/// <summary>
/// The standing crowd at every bank. It hands a person a role when its bank has room and the
/// role fits, keeps the seats, and forgets members who died, left, or finished. A hawker's
/// real item and asking price are kept here for buyers and the haggling that reads them.
/// World thread only.
/// </summary>
public static class BankCrowd
{
    private static readonly Dictionary<string, List<BankCrowdSeat>> Crowds = new(StringComparer.Ordinal);
    private static readonly Dictionary<Serial, (SosariaCharacter Hawker, HawkerOffer Offer)> Offers = new();
    private static readonly Dictionary<string, int> Residents = new(StringComparer.Ordinal);
    private static readonly Dictionary<Destination, Destination> CanonicalBanks = new();
    private static readonly Dictionary<Serial, (string Bank, DateTime Until)> Promises = new();
    private static DateTime _residentsCountedAt;

    /// <summary>
    /// True when the person's bank is short of its crowd and a role there fits the person.
    /// The job picker weights a bank job with this. A yes promises the person a seat until it
    /// can claim it (<see cref="BankCrowdRules.PromiseHold"/>): the job begins a tick or more
    /// after the choice, and a second person who chose the last seat in between took it first.
    /// </summary>
    public static bool Wants(SosariaCharacter person)
    {
        if (!BankOf(person, out var key, out _) || RoleFor(person, key) == null)
        {
            return false;
        }

        Promises[person.Serial] = (key, Core.Now + BankCrowdRules.PromiseHold);
        return true;
    }

    /// <summary>Takes a place in the person's bank crowd. False when the crowd is full or nothing fits.</summary>
    public static bool TryClaim(SosariaCharacter person, out BankCrowdSeat seat)
    {
        seat = null;
        Release(person);

        if (!BankOf(person, out var key, out var bank))
        {
            return false;
        }

        var role = RoleFor(person, key);
        var free = BankCrowdRules.FreeSeat(TakenSeats(key));

        if (role is not { } picked || free == BankCrowdRules.NoSeat)
        {
            return false;
        }

        // The seat stands on the ground floor the walker finds there, not at the height of the
        // banker spawner the bank was drafted from: at Moonglow and Yew that height put the
        // crowd on the roof. A seat on a wall has no ground but the roof over it, so it moves
        // to the tile beside.
        var spot = ArrivalHeight.StreetSpot(
            person.Map,
            HomeSpotRules.GroundedAt(person.Map, BankCrowdRules.SeatSpot(bank, free))
        );
        seat = new BankCrowdSeat(person, key, bank, picked, free, spot);
        SeatsOf(key).Add(seat);
        return true;
    }

    public static void Release(SosariaCharacter person)
    {
        if (person == null)
        {
            return;
        }

        foreach (var seats in Crowds.Values)
        {
            seats.RemoveAll(seat => seat.Member == person);
        }

        Offers.Remove(person.Serial);
        Promises.Remove(person.Serial);
    }

    /// <summary>The seats held at a bank now, after members who left are let go.</summary>
    public static IReadOnlyList<BankCrowdSeat> SeatsAt(string bankKey)
    {
        var seats = SeatsOf(bankKey);
        seats.RemoveAll(IsGone);
        return seats;
    }

    public static void SetHawkerOffer(SosariaCharacter hawker, HawkerOffer offer) => Offers[hawker.Serial] = (hawker, offer);

    public static void ClearHawkerOffer(SosariaCharacter hawker) => Offers.Remove(hawker.Serial);

    public static bool TryGetHawkerOffer(Mobile hawker, out HawkerOffer offer)
    {
        offer = default;

        if (hawker == null || !Offers.TryGetValue(hawker.Serial, out var held))
        {
            return false;
        }

        offer = held.Offer;
        return true;
    }

    /// <summary>
    /// Everyone holding goods up within <paramref name="range"/> of a spot, with what they sell:
    /// a hawker in the crowd, a shopper that shouted WTS, a gatherer with its load. The offers
    /// are few, so this reads them and never the map.
    /// </summary>
    public static List<(SosariaCharacter Hawker, HawkerOffer Offer)> HawkersNear(Map map, Point3D at, int range)
    {
        var found = new List<(SosariaCharacter, HawkerOffer)>();

        foreach (var (hawker, offer) in Offers.Values)
        {
            if (hawker is { Deleted: false } && hawker.Map == map && NavMetric.Chebyshev(hawker.Location, at) <= range)
            {
                found.Add((hawker, offer));
            }
        }

        return found;
    }

    public static BankCrowdCandidate CandidateOf(SosariaCharacter person)
    {
        var profile = person.PersonProfile;
        var gold = (person.Backpack?.GetAmount(typeof(Gold)) ?? 0) + Banker.GetBalance(person);

        return new BankCrowdCandidate(
            person.MayUseThisBank() && !person.IsPk && !person.Criminal,
            profile.Has(PersonTrait.Social),
            profile.Has(PersonTrait.Loner),
            profile.Has(PersonTrait.Greedy),
            profile.Has(PersonTrait.Homebody),
            SelfCurse.CanTrain(person) && (SelfCurse.HasReagents(person) || ReagentsInBank(person)),
            person.Skills.Hiding.Base,
            Stealth.HidingRequirement,
            HawkerGoods.BestInPack(person) != null || BankGoods(person) != null,
            profile.Wealth == PersonWealth.Poor && gold < BankCrowdRules.BeggarGoldCeiling,
            profile.Tier == SkillTier.Novice
        );
    }

    /// <summary>The best goods for sale in the bank box, or null.</summary>
    public static Item BankGoods(SosariaCharacter person)
    {
        Item best = null;
        var bestValue = 0;

        foreach (var item in person?.BankBox?.Items ?? [])
        {
            if (!HawkerGoods.IsForSale(person, item))
            {
                continue;
            }

            var value = Appraisal.Value(item, Appraisal.MidRoll);

            if (value > bestValue)
            {
                bestValue = value;
                best = item;
            }
        }

        return best;
    }

    private static bool ReagentsInBank(SosariaCharacter person)
    {
        var bank = person.BankBox;

        if (bank == null)
        {
            return false;
        }

        for (var i = 0; i < SupplyCheck.ReagentTypes.Length; i++)
        {
            if (bank.GetAmount(SupplyCheck.ReagentTypes[i]) > 0)
            {
                return true;
            }
        }

        return false;
    }

    // The person's nearest bank within its leash; never the pirate town's bank, where the
    // standing crowd is murderers.
    private static bool BankOf(SosariaCharacter person, out string key, out Point3D bank)
    {
        key = null;
        bank = Point3D.Zero;

        if (!People.InWorld(person))
        {
            return false;
        }

        var dest = BankAt(person.HomeFacet, person.Location);

        if (dest == null || dest.Arrival == Point3D.Zero ||
            HomeLeash.BeyondLeash(dest.Arrival, person.Location, HomeLeash.ConfiguredRadius()) ||
            PkRules.InBuccaneersDen(dest.Arrival.X, dest.Arrival.Y))
        {
            return false;
        }

        key = KeyOf(person.HomeFacet, dest);
        bank = dest.ApproachPoint(NavWorld.GraphFor(person.HomeFacet));
        return true;
    }

    /// <summary>The one bank a crowd near <paramref name="at"/> gathers at, or null.</summary>
    private static Destination BankAt(string facet, Point3D at)
    {
        var catalog = NavWorld.DestinationsFor(facet);
        var nearest = catalog?.Nearest(at, DestinationKind.Bank);

        if (nearest == null)
        {
            return null;
        }

        if (!CanonicalBanks.TryGetValue(nearest, out var canonical))
        {
            canonical = BankCrowdRules.Canonical(catalog.NearestFirst(BankTeller.BankToken, nearest.Arrival, int.MaxValue), nearest);
            CanonicalBanks[nearest] = canonical;
        }

        return canonical;
    }

    private static string KeyOf(string facet, Destination bank) => $"{facet}:{bank.Name}";

    /// <summary>The crowd a bank holds for the live people who call it home, counted every few minutes.</summary>
    private static int TargetAt(string key)
    {
        if (Core.Now >= _residentsCountedAt)
        {
            CountResidents();
            _residentsCountedAt = Core.Now + BankCrowdRules.ResidentRefresh;
        }

        return BankCrowdRules.TargetFor(Residents.GetValueOrDefault(key));
    }

    private static void CountResidents()
    {
        Residents.Clear();

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is SosariaCharacter { Deleted: false } person && People.InWorld(person) &&
                BankAt(person.HomeFacet, person.HomeSpot) is { } home)
            {
                var key = KeyOf(person.HomeFacet, home);
                Residents[key] = Residents.GetValueOrDefault(key) + 1;
            }
        }
    }

    private static List<BankCrowdSeat> SeatsOf(string key)
    {
        if (!Crowds.TryGetValue(key, out var seats))
        {
            seats = [];
            Crowds[key] = seats;
        }

        return seats;
    }

    // The role the person takes at the bank, or null when the seats held and the seats
    // promised to others fill the crowd, or no open role fits.
    private static BankCrowdRole? RoleFor(SosariaCharacter person, string key)
    {
        var roles = RolesAt(key);
        var target = TargetAt(key);

        return BankCrowdRules.HasRoom(roles.Count, PromisedToOthers(key, person), target)
            ? BankCrowdRules.Choose(roles, CandidateOf(person), target)
            : null;
    }

    private static int PromisedToOthers(string key, SosariaCharacter person)
    {
        var now = Core.Now;
        var promised = 0;
        var lapsed = new List<Serial>();

        foreach (var (serial, promise) in Promises)
        {
            if (!BankCrowdRules.PromiseHolds(promise.Until, now))
            {
                lapsed.Add(serial);
            }
            else if (serial != person.Serial && promise.Bank == key)
            {
                promised++;
            }
        }

        for (var i = 0; i < lapsed.Count; i++)
        {
            Promises.Remove(lapsed[i]);
        }

        return promised;
    }

    private static List<BankCrowdRole> RolesAt(string key)
    {
        var seats = SeatsAt(key);
        var roles = new List<BankCrowdRole>(seats.Count);

        for (var i = 0; i < seats.Count; i++)
        {
            roles.Add(seats[i].Role);
        }

        return roles;
    }

    private static List<int> TakenSeats(string key)
    {
        var seats = SeatsAt(key);
        var taken = new List<int>(seats.Count);

        for (var i = 0; i < seats.Count; i++)
        {
            taken.Add(seats[i].Seat);
        }

        return taken;
    }

    // A member who died, left the world, walked off, or outstayed any hold has left the crowd.
    private static bool IsGone(BankCrowdSeat seat)
    {
        var member = seat.Member;
        var gone = member == null || member.Deleted || !member.Alive || !People.InWorld(member) ||
                   BankCrowdRules.HasLeft(seat.Arrived, BankCrowdRules.AtBank(member.Location, seat.Bank), Core.Now - seat.Since);

        if (gone && member != null)
        {
            Offers.Remove(member.Serial);
        }

        return gone;
    }
}
