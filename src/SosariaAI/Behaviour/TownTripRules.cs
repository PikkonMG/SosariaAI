using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>
/// A travel job goes to another town. Its goal is a bank there: every town has one, and
/// the bank is where a visitor finds people. Pure. No world objects.
/// </summary>
public static class TownTripRules
{
    public const string RoutineId = "travel";
    public const string BankKind = "Bank";
    public const int PickSalt = 0x7A11;

    private const char IdSeparator = ':';

    /// <summary>
    /// How long a bank rests after its trip was given up for danger on the road: as long as
    /// the walker remembers the place it ran from (<see cref="SpotMemory.Memory"/>).
    /// </summary>
    public static readonly TimeSpan GivenUpRest = SpotMemory.Memory;

    public static string IdPrefix { get; } = $"{RoutineId}{IdSeparator}{SkillKinds.Travel}{IdSeparator}";

    /// <summary>
    /// The bank this phase's trip goes to, or null when there is none. The pick depends on
    /// the seed and the banks only, not on where the person stands, so it holds for the
    /// whole phase: at the far bank the trip is done, and a person who strolled off walks
    /// back to it. At home, a pick in the home town (within <paramref name="minTiles"/>)
    /// means no trip this phase. Banks near a place the person ran from are left out, and
    /// so are banks the router just found no way to: a walker bound for an island with no
    /// moongate goes to another town instead of standing about at home.
    /// </summary>
    public static Destination Pick(
        IReadOnlyList<Destination> places,
        Point3D from,
        int minTiles,
        IReadOnlyList<Point3D> dangerSpots,
        IReadOnlyList<Point3D> unreachable,
        int seed,
        bool awayFromHome
    )
    {
        if (places == null || places.Count == 0)
        {
            return null;
        }

        var banks = new List<Destination>();

        for (var i = 0; i < places.Count; i++)
        {
            var place = places[i];

            if (place == null ||
                string.IsNullOrWhiteSpace(place.Name) ||
                !string.Equals(place.Kind, BankKind, StringComparison.OrdinalIgnoreCase) ||
                place.Arrival == Point3D.Zero ||
                NavSearch.IsNearAny(place.Arrival, dangerSpots) ||
                NavSearch.IsNearAny(place.Arrival, unreachable))
            {
                continue;
            }

            banks.Add(place);
        }

        if (banks.Count == 0)
        {
            return null;
        }

        var index = Math.Min((int)(ChoiceSeed.Unit(seed, PickSalt) * banks.Count), banks.Count - 1);
        var bank = banks[index];
        var inHomeTown = !awayFromHome && NavMetric.Chebyshev(from, bank.Arrival) <= minTiles;
        return inHomeTown ? null : bank;
    }

    /// <summary>True while a bank rests after its trip was given up at <paramref name="givenUpAt"/>; default is never.</summary>
    public static bool RestsAfterGivingUp(DateTime givenUpAt, DateTime now) =>
        !TimeRules.Rested(givenUpAt, now, GivenUpRest);

    public static ActionCandidate Candidate(Destination bank)
    {
        if (bank == null || string.IsNullOrWhiteSpace(bank.Name))
        {
            return null;
        }

        var step = new SkillStepDefinition
        {
            Skill = SkillKinds.Travel,
            Destination = bank.Name,
            Target = bank.Arrival
        };

        return new ActionCandidate
        {
            Id = ActionId.From(RoutineId, step, 0),
            SkillKind = SkillKinds.Travel,
            RoutineId = RoutineId,
            Step = step
        };
    }

    /// <summary>The bank a trip id names, or null when the id is not a trip.</summary>
    public static string BankNameOf(string actionId) =>
        !string.IsNullOrWhiteSpace(actionId) && actionId.StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase)
            ? actionId[IdPrefix.Length..]
            : null;
}
