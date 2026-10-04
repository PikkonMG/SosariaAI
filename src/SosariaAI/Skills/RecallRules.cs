using System;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// Fourth-circle Recall as a human mage uses it: a runebook entry or a rune marked at the
/// place, reagents, a scroll or a book charge, the words of power, then the landing. A fizzle is cast again a few times; a trip
/// that will not take is walked. A rune does not have to sit on the goal: a player recalls
/// to the nearest town it has a rune for and walks the rest.
/// </summary>
public static class RecallRules
{
    public const string Kind = SkillKinds.Recall;

    /// <summary>
    /// The lowest Magery that can cast Recall from a book at all. Before Mondain's Legacy the
    /// engine checks a fourth-circle book cast from 30 to 70 Magery (MagerySpell.GetCastSkills);
    /// below 30 no cast takes.
    /// </summary>
    public const double MinMagery = 30;

    /// <summary>
    /// The lowest Magery that recalls from a scroll or a runebook charge with a fair chance.
    /// The engine casts a scroll at the spell's own circle, two below a book cast
    /// (MagerySpell.GetCastSkills), and the runebook passes itself as the scroll for a charge.
    /// Before Mondain's Legacy the fourth circle from a scroll is checked from 10 to 50
    /// Magery: at 20 one cast in four takes, so <see cref="MaxCastTries"/> casts land three
    /// trips in four. Below it the person walks.
    /// </summary>
    public const double ScrollMinMagery = 20;

    /// <summary>
    /// Casts in a row before a fizzling trip is walked instead. A shaky caster kept at the
    /// macro: three fizzles in a row at one chance in two or worse were routine.
    /// </summary>
    public const int MaxCastTries = 5;

    /// <summary>A trip shorter than this is a walk: the words of power cost more than the road.</summary>
    public const int MinTripTiles = 160;

    /// <summary>The longest walk from a landing to the goal that still makes a recall worth it.</summary>
    public const int MaxLandingWalkTiles = 120;

    /// <summary>The walk left after the landing is at most this share of the trip: a third.</summary>
    public const int TripShareOfLandingWalk = 3;

    /// <summary>
    /// With no road to the goal, water or a gateless island in the way, a trip of any length
    /// is recalled over, as players crossed water; the landing walk still has to be short.
    /// </summary>
    public const int NoRoadMinTripTiles = 0;

    /// <summary>
    /// Out of a hundred, the long trips a person with the means makes by magic. Nobody walked
    /// half the continent, but plenty walked to the next town, and a world where every long
    /// trip is a recall has empty roads.
    /// </summary>
    public const int LongTripMagicPercent = 65;

    /// <summary>Out of a hundred, the far trips made by magic.</summary>
    public const int FarTripMagicPercent = 85;

    /// <summary>A trip this long or longer is a far one.</summary>
    public const int FarTripTiles = 300;

    /// <summary>
    /// With no road, a recall refused only for a moment (see <see cref="TravelSpells.Passes"/>)
    /// is waited for this long: the heat of battle cools in thirty seconds.
    /// </summary>
    public static readonly TimeSpan NoRoadRecallWait = TimeSpan.FromSeconds(40);

    /// <summary>
    /// A red's recall home to the Den after a death waits this long for a refusal that passes:
    /// the heat of battle and the cast recovery wear off in seconds, a criminal flag in two
    /// minutes. Past it the red walks home.
    /// </summary>
    public static readonly TimeSpan HomeRecallWait = TimeSpan.FromMinutes(3);

    /// <summary>
    /// True when a recall home refused for <paramref name="why"/> is waited for rather than
    /// walked: a refusal that passes (<see cref="TravelSpells.Passes"/>), or the criminal flag,
    /// which lapses on its own. A red flagged for a fight at its body could not recall, nor bank.
    /// </summary>
    public static bool HomeRecallWaits(string why) => TravelSpells.Passes(why) || why == TravelSpells.CriminalWhy;

    /// <summary>
    /// How long a trip with no road waits for its recall over the gap, refused for
    /// <paramref name="why"/>: a refusal that passes in seconds (<see cref="TravelSpells.Passes"/>)
    /// <see cref="NoRoadRecallWait"/>, the criminal flag, which lapses on its own,
    /// <see cref="HomeRecallWait"/>. Null for a refusal no wait ends. Pure.
    /// </summary>
    public static TimeSpan? NoRoadWait(string why) =>
        TravelSpells.Passes(why) ? NoRoadRecallWait
        : why == TravelSpells.CriminalWhy ? HomeRecallWait
        : null;

    public const string NoRuneWhy = "no rune lands near the goal";

    /// <summary>A party member stays with its party on foot, so it recalls over no road alone.</summary>
    public const string InPartyWhy = "walks with its party";

    /// <summary>The trip recalled over no road once already: a landing with no road on is not recalled from again.</summary>
    public const string RecalledOnceWhy = "recalled once this trip";

    /// <summary>Every check passed and the engine spell still would not begin.</summary>
    public const string WordsFailedWhy = "the words of power would not begin";

    /// <summary>The route's failure, and with no road, after it, why no recall carried the walker over it. Pure.</summary>
    public static string NoRoadLine(string routeWhy, string noRecallWhy) =>
        string.IsNullOrEmpty(noRecallWhy) ? routeWhy : $"{routeWhy}; no recall: {noRecallWhy}";

    /// <summary>The share, out of a hundred, of trips this long that go by magic.</summary>
    public static int MagicPercent(int tripTiles) =>
        tripTiles >= FarTripTiles ? FarTripMagicPercent : LongTripMagicPercent;

    /// <summary>True when this trip goes by magic rather than on foot, for a roll out of <see cref="PercentRoll.Scale"/>.</summary>
    public static bool TakesMagic(int tripTiles, int roll) => PercentRoll.Under(roll, MagicPercent(tripTiles));

    /// <summary>
    /// Recall pays on a trip of at least <paramref name="minTripTiles"/> when a rune lands
    /// close to the goal: a short walk left, and a small share of the whole trip.
    /// </summary>
    public static bool PaysForTrip(int tripTiles, int landingToGoalTiles, int minTripTiles = MinTripTiles) =>
        tripTiles >= minTripTiles &&
        landingToGoalTiles <= Math.Min(MaxLandingWalkTiles, tripTiles / TripShareOfLandingWalk);

    public static bool ShouldCastAgain(int castsSoFar) => castsSoFar < MaxCastTries;

    /// <summary>
    /// After a cast that did not take, the next is tried for this long: the cast recovery
    /// wears off and a person steps off the landing. Casting again at once hit the recovery,
    /// so one fizzle ended the run: "fizzled recall" and a failed outing eleven seconds later.
    /// </summary>
    public static readonly TimeSpan RecastWait = TimeSpan.FromSeconds(8);

    /// <summary>Casts that did not take and the tries are spent.</summary>
    public const string CastsSpentWhy = "the spell would not take";

    /// <summary>A cast did not take, and the next would not begin before the wait ran out.</summary>
    public const string RecastRefusedWhy = "the spell would not begin again";

    /// <summary>True while a cast that did not take may still be tried again. Pure.</summary>
    public static bool MayRecast(int castsSoFar, DateTime waitEnds, DateTime now) =>
        ShouldCastAgain(castsSoFar) && now < waitEnds;

    /// <summary>
    /// The marked place, book entry or loose rune, a trip to <paramref name="goal"/> would
    /// recall or gate to, or null when none pays. A goal in a dungeon is reached through its
    /// door, so the mark lands there. <paramref name="minTripTiles"/> is the shortest trip
    /// worth the words: a dungeon run pays sooner than a town errand.
    /// </summary>
    public static TravelMark RuneToward(SosariaCharacter character, Point3D goal, int minTripTiles = MinTripTiles)
    {
        if (!People.InWorld(character) || goal == Point3D.Zero || !RuneShelf.CarriesMarked(character))
        {
            return null;
        }

        var landing = RuneShelf.LandingFor(character.Map, goal);
        var mark = RuneShelf.MarkedNear(character, landing, character.Map, MaxLandingWalkTiles);

        return mark != null &&
               PaysForTrip(
                   NavMetric.Chebyshev(character.Location, landing),
                   NavMetric.Chebyshev(mark.Target, landing),
                   minTripTiles
               )
            ? mark
            : null;
    }

    /// <summary>True when the person could recall toward <paramref name="goal"/> now: a mark that pays and the means.</summary>
    public static bool CanRecallToward(SosariaCharacter character, Point3D goal, int minTripTiles = MinTripTiles) =>
        WhyNoRecall(character, goal, minTripTiles) == null;

    /// <summary>
    /// True when a recall toward <paramref name="goal"/> can be cast now, or as soon as the
    /// person on its landing steps off (<see cref="TravelSpells.LandingTakenWhy"/>): a gang
    /// mate who just landed on the camp rune does not take the camp out of reach.
    /// </summary>
    public static bool CanRecallSoon(SosariaCharacter character, Point3D goal, int minTripTiles = MinTripTiles) =>
        WhyNoRecall(character, goal, minTripTiles) is null or TravelSpells.LandingTakenWhy;

    /// <summary>
    /// Why the person could not recall toward <paramref name="goal"/> now, or null when it
    /// could: no rune lands near enough (a red's runes under the guards never count), or the
    /// cast would not begin (see <see cref="TravelSpells.WhyNotToward"/>).
    /// </summary>
    public static string WhyNoRecall(SosariaCharacter character, Point3D goal, int minTripTiles = MinTripTiles) =>
        RuneToward(character, goal, minTripTiles) is { } mark
            ? TravelSpells.WhyNotToward(character, TravelSpellKind.Recall, mark)
            : NoRuneWhy;

    /// <summary>Begins a real Recall toward <paramref name="goal"/>. True when the words of power began.</summary>
    public static bool TryRecallToward(SosariaCharacter character, Point3D goal, int minTripTiles = MinTripTiles) =>
        RuneToward(character, goal, minTripTiles) is { } mark &&
        TravelSpells.Begin(character, TravelSpellKind.Recall, mark);

    /// <summary>
    /// Begins a real Recall to the rune marked nearest home. True when the words of power
    /// began; the landing follows when the cursor is answered.
    /// </summary>
    public static bool TryRecallHome(SosariaCharacter character) =>
        character != null && TryRecallToward(character, character.HomeSpot);
}
