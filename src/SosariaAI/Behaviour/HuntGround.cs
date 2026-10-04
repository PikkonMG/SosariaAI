using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;

namespace SosariaAI.Behaviour;

/// <summary>
/// A character's home, its leash, the hunt place and the dungeon hall picked for it, and the
/// power and tier they were picked for. A null place means the catalog has nothing of that
/// kind in reach. <paramref name="KnowsDoors"/> says the world has dungeon doors at all, so a
/// null dungeon is its verdict on reach. <paramref name="Dungeon"/> carries the power bar of
/// its hall as its difficulty (see <see cref="DungeonGround.WithPowerBar"/>).
/// <paramref name="Catalog"/> tells an authored hunt's own difficulty.
/// </summary>
public sealed record HuntHome(
    Point3D Home,
    int Leash,
    Destination Ground,
    Destination Dungeon = null,
    bool KnowsDoors = false,
    int Power = 0,
    SkillTier Tier = SkillTier.Novice,
    DestinationCatalog Catalog = null
);

/// <summary>What a look at a hunt ground finds before a fighter walks there.</summary>
public enum GroundState
{
    /// <summary>Under town guards, or found empty by this fighter lately: not a hunt.</summary>
    Closed,

    /// <summary>Open ground with no prey on it this moment. The spawner may fill it again.</summary>
    Empty,

    /// <summary>Open ground with live prey on it.</summary>
    Live
}

/// <summary>
/// Where a fighter hunts. The world catalog lists every place a creature spawns, with the
/// creature as the role and a difficulty on the power scale. Spawners of vendors, animals and
/// reagents are in the same list, so the caller says which roles are enemies. Town life
/// (rats, cats, birds) is never a hunt. A fighter picks a place inside its home leash that
/// its power can handle, with live prey on it when one has any, and looks for its ambition
/// prey first. Power fits both ways: a novice keeps to easy ground near its town, and a
/// veteran leaves ground far below it (the Britain graveyard) to the novices unless its
/// ambition names that prey or nothing else is in reach. An authored hunt names one fixed
/// place, which only the characters who live near it, and whose power it fits, keep.
/// </summary>
public static class HuntGround
{
    public const int NearChoices = 5;

    /// <summary>
    /// A shared spawner lets its creatures walk this far from the spawner, so the ground
    /// covers the walk. A smaller square missed most of the spawn it was named for.
    /// </summary>
    public const int AreaRadius = 30;

    public const int PowerBand = 10;

    /// <summary>
    /// A pick looks at this many spawner spots, nearest first, for the prey and again for
    /// any foe, before it settles. Every person picks once at boot, so the look stays short.
    /// </summary>
    public const int MaxGroundLooks = 12;

    /// <summary>
    /// A spot's difficulty is the threat of the hard foe its ground holds again and again, and
    /// its boss (<see cref="GroundRating"/>), on the scale a fighter opens a fight on: one foe
    /// up to 1.15 times its power (<see cref="ThreatRating.DefaultThreatMultiple"/>). A spot is
    /// in reach up to that multiple of the power, so a fighter there opens on the usual hard foe
    /// of the ground by its own rule. The old reach of 1.8 scored the two weakest foes near a
    /// spot, and a power-78 fighter was sent where eleven liches stood.
    /// </summary>
    public const double PowerReach = ThreatRating.DefaultThreatMultiple;

    /// <summary>A novice calls ground below this share of its reach far below it; each tier up raises the bar.</summary>
    public const double FarBelowBase = 0.1;

    public const double FarBelowPerTier = 0.07;

    /// <summary>
    /// A novice likes ground at this share of its reach best, where the hard foe is half what it
    /// dares: an orc or lizardman camp, the graveyard; each tier up likes it harder, and a
    /// grandmaster likes ground near the top of its reach.
    /// </summary>
    public const double IdealBase = 0.5;

    public const double IdealPerTier = 0.06;

    /// <summary>How fast the liking falls off away from the ideal share.</summary>
    public const double FitSlope = 3.0;

    /// <summary>What ground far below a fighter still weighs: only when nothing else is left.</summary>
    public const double FarBelowFit = 0.02;

    /// <summary>The ambition's prey weighs as much as the best ground.</summary>
    public const double PreyFit = 1.0;

    private const double NoFit = 0;

    public static Destination Pick(
        DestinationCatalog catalog,
        Point3D home,
        int leash,
        int power,
        SkillTier tier,
        string prey,
        int seed,
        Func<string, bool> isEnemy,
        Func<Destination, GroundState> look = null
    )
    {
        if (catalog == null || isEnemy == null)
        {
            return null;
        }

        var preyInReach = new List<Destination>();
        var fitting = new List<Destination>();
        var farBelow = new List<Destination>();
        var moongates = GatesOutOf(home);

        for (var i = 0; i < catalog.All.Count; i++)
        {
            var place = catalog.All[i];

            if (place.ParsedKind != DestinationKind.Hunt ||
                place.Difficulty is not { } difficulty ||
                !InReach(difficulty, power) ||
                NavMetric.ByMoongate(home, place.Arrival, moongates) > leash ||
                HarmlessCreatures.LooksNamed(place.Role) ||
                !isEnemy(place.Role))
            {
                continue;
            }

            if (string.Equals(place.Role, prey, StringComparison.OrdinalIgnoreCase))
            {
                preyInReach.Add(place);
            }
            else if (FarBelow(difficulty, power, tier))
            {
                farBelow.Add(place);
            }
            else
            {
                fitting.Add(place);
            }
        }

        var looked = new Dictionary<Point3D, GroundState>();
        var preyGrounds = Sorted(preyInReach, home, moongates, look, looked);
        var grounds = Sorted(fitting, home, moongates, look, looked);
        var pick = Choose(preyGrounds.Live, seed) ??
                   Choose(grounds.Live, seed) ??
                   Choose(preyGrounds.Empty, seed) ??
                   Choose(grounds.Empty, seed);

        if (pick != null)
        {
            return pick;
        }

        // Only ground far below the fighter is left: better that than no hunt at all.
        var low = Sorted(farBelow, home, moongates, look, looked);
        return Choose(low.Live, seed) ?? Choose(low.Empty, seed);
    }

    /// <summary>
    /// The public moongates a home is left by, or null for a home walked out of. Buccaneer's Den
    /// is an island with no hunt ground on it: its reds step through the Den's gate, so a
    /// ground's walk from there counts the gates, as a dungeon door's does. The ground within
    /// the leash of the Den itself held only its town's rats and birds.
    /// </summary>
    public static IReadOnlyList<Point3D> GatesOutOf(Point3D home) =>
        PkRules.InBuccaneersDen(home.X, home.Y) ? MoongateSeeds.LocationsFor(FacetNames.Felucca) : null;

    /// <summary>The hardest spot difficulty a fighter of <paramref name="power"/> takes on.</summary>
    public static int Reach(int power) => (int)(Math.Max(0, power) * PowerReach);

    /// <summary>True when a spot of known difficulty lies within the fighter's reach.</summary>
    public static bool InReach(int difficulty, int power) => difficulty > 0 && power > 0 && difficulty <= Reach(power);

    /// <summary>
    /// The power a spot asks of a fighter on its own: its difficulty over
    /// <see cref="PowerReach"/>, rounded up, so that <see cref="InReach"/> holds from there on.
    /// </summary>
    public static int PowerBar(int difficulty) =>
        difficulty <= 0 ? 0 : (int)Math.Ceiling(difficulty / PowerReach);

    /// <summary>A spot's difficulty as a share of the fighter's reach; zero without a power.</summary>
    public static double ShareOfReach(int difficulty, int power) =>
        power <= 0 ? NoFit : (double)Math.Max(0, difficulty) / (power * PowerReach);

    public static double FarBelowShare(SkillTier tier) => FarBelowBase + (int)tier * FarBelowPerTier;

    public static double IdealShare(SkillTier tier) => IdealBase + (int)tier * IdealPerTier;

    /// <summary>A spot in reach, but so far below the fighter's tier and power that it leaves it to others.</summary>
    public static bool FarBelow(int difficulty, int power, SkillTier tier) =>
        InReach(difficulty, power) && ShareOfReach(difficulty, power) < FarBelowShare(tier);

    /// <summary>
    /// How much a fighter likes a spot, from zero to one: nothing out of reach, a sliver far
    /// below it, and most at the share its tier likes, falling off either side.
    /// </summary>
    public static double Fit(int difficulty, int power, SkillTier tier)
    {
        if (!InReach(difficulty, power))
        {
            return NoFit;
        }

        return FarBelow(difficulty, power, tier)
            ? FarBelowFit
            : 1 / (1 + Math.Abs(ShareOfReach(difficulty, power) - IdealShare(tier)) * FitSlope);
    }

    /// <summary>
    /// The nearest open grounds, split by what a look finds. One spawner spot lists each of
    /// its creatures as a place, so a spot is looked at once.
    /// </summary>
    private static (List<Destination> Live, List<Destination> Empty) Sorted(
        List<Destination> places,
        Point3D home,
        IReadOnlyList<Point3D> moongates,
        Func<Destination, GroundState> look,
        Dictionary<Point3D, GroundState> looked
    )
    {
        var live = new List<Destination>();
        var empty = new List<Destination>();
        var looks = 0;

        places.Sort((left, right) =>
            NavMetric.ByMoongate(home, left.Arrival, moongates).CompareTo(NavMetric.ByMoongate(home, right.Arrival, moongates)));

        for (var i = 0; i < places.Count && live.Count < NearChoices; i++)
        {
            var place = places[i];

            if (!looked.TryGetValue(place.Arrival, out var state))
            {
                if (looks >= MaxGroundLooks)
                {
                    break;
                }

                looks++;
                state = look?.Invoke(place) ?? GroundState.Live;
                looked[place.Arrival] = state;
            }

            if (state == GroundState.Live)
            {
                live.Add(place);
            }
            else if (state == GroundState.Empty && empty.Count < NearChoices)
            {
                empty.Add(place);
            }
        }

        return (live, empty);
    }

    /// <summary>What a look finds: guards or a recent dry run close a ground, prey makes it live.</summary>
    public static GroundState StateOf(bool guarded, bool dry, int preyCount) =>
        guarded || dry ? GroundState.Closed
        : preyCount > 0 ? GroundState.Live
        : GroundState.Empty;

    private static Destination Choose(List<Destination> choices, int seed) =>
        choices.Count == 0 ? null : choices[(seed & int.MaxValue) % choices.Count];

    /// <summary>
    /// An authored hunt stays when the character lives within the leash of it and the ground
    /// is not far below it. A character that lives further away, or a veteran whose authored
    /// ground is novice ground, hunts at its own ground. A crew's hunt stays: the party says so.
    /// </summary>
    public static SkillStepDefinition ForHome(SkillStepDefinition step, HuntHome hunt)
    {
        if (hunt?.Ground == null ||
            step == null ||
            !SkillKinds.Hunt.Equals(step.Skill, StringComparison.OrdinalIgnoreCase) ||
            step.Target == Point3D.Zero)
        {
            return step;
        }

        var beyondLeash = NavMetric.Chebyshev(hunt.Home, step.Target) > hunt.Leash;
        var belowItsTier = string.IsNullOrWhiteSpace(step.Party) &&
                           FarBelow(DifficultyAt(hunt.Catalog, step.Target), hunt.Power, hunt.Tier);

        return beyondLeash || belowItsTier ? AtPlace(step, hunt.Ground, keepsCrew: false) : step;
    }

    /// <summary>
    /// How a log line shows a fighter's fit to a place: " (fits power 120 vs ground 95)", or
    /// " (power 80 below ground 130)" when the place is past its reach; empty for a place of
    /// unknown difficulty.
    /// </summary>
    public static string FitNote(int power, int difficulty, string placeWord) =>
        difficulty <= 0 ? string.Empty
        : difficulty <= Reach(power) ? $" (fits power {power} vs {placeWord} {difficulty})"
        : $" (power {power} below {placeWord} {difficulty})";

    /// <summary>The word a log line names a hunt ground by.</summary>
    public const string GroundWord = "ground";

    /// <summary>The difficulty of the spawn spot nearest a place, within a ground's walk of it; zero when none.</summary>
    public static int DifficultyAt(DestinationCatalog catalog, Point3D at)
    {
        var spot = catalog?.Nearest(at, DestinationKind.Hunt);

        return spot?.Difficulty is { } difficulty && NavMetric.Chebyshev(spot.Arrival, at) <= AreaRadius
            ? difficulty
            : 0;
    }

    /// <summary>
    /// The same step at a catalog place, in the square round it, with its range, time and
    /// hits floor. A moved hunt goes alone: its crew belongs to the authored place. A moved
    /// delve keeps its crew (<paramref name="keepsCrew"/>): the graph walks it through the
    /// door, and the authored dungeon name, which names another hall, is dropped.
    /// </summary>
    public static SkillStepDefinition AtPlace(SkillStepDefinition step, Destination place, bool keepsCrew)
    {
        if (step == null || place == null)
        {
            return step;
        }

        var at = place.Arrival;

        return new SkillStepDefinition
        {
            Skill = step.Skill,
            Target = at,
            Area = AreaAround(at),
            Range = step.Range,
            Minutes = step.Minutes,
            StopBelowHitsFraction = step.StopBelowHitsFraction,
            Party = keepsCrew ? step.Party : null
        };
    }

    /// <summary>The square a fighter hunts in, centred on the catalog spot.</summary>
    public static AreaDefinition AreaAround(Point3D at) =>
        new()
        {
            X = at.X - AreaRadius,
            Y = at.Y - AreaRadius,
            Width = AreaRadius * 2 + 1,
            Height = AreaRadius * 2 + 1
        };
}
