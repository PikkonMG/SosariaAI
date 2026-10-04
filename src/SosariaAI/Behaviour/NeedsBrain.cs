using System;
using System.Collections.Generic;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

public sealed class NeedsSnapshot
{
    public double HitsFraction { get; init; } = 1;

    public int GoldBanked { get; init; }

    public int GoldCarried { get; init; }

    public double PackFillFraction { get; init; }

    public int Power { get; init; }

    /// <summary>The power once the wounds are mended (<see cref="SosariaAI.Combat.CharacterPower.Healthy"/>); zero when unread.</summary>
    public int HealthyPower { get; init; }

    /// <summary>
    /// The power a hunt ground or a dungeon hall is held to: the healthy one, as the ground and
    /// the hall were picked by it (<see cref="DungeonGround.FightingPower"/>). Held to the hits
    /// of the moment, a fighter a little hurt lost the hall it had just picked: 45 of 905 fighters
    /// above ground in one count stood "below required power".
    /// </summary>
    public int OutingPower => Math.Max(Power, HealthyPower);

    public TimeSpan TimeSinceHunt { get; init; } = TimeSpan.MaxValue;

    public TimeSpan TimeSinceRest { get; init; } = TimeSpan.MaxValue;

    public TimeSpan TimeSinceTown { get; init; } = TimeSpan.MaxValue;

    public bool PartyForming { get; init; }

    public PersonaDrives Drives { get; init; } = PersonaDrives.Neutral;

    public string DayPart { get; init; }

    public bool AmbitionWantsWork { get; init; }

    public bool AmbitionWantsHunt { get; init; }

    public bool AmbitionWantsTravel { get; init; }

    public int AlliesPower { get; init; }

    public string BlockedRoutine { get; init; }

    public int BlockedCount { get; init; }

    public bool CanShop { get; init; } = true;

    public int GoldTotal => GoldBanked + GoldCarried;

    public bool HasSignals =>
        HitsFraction < NeedsBrain.HealthyHitsFraction ||
        GoldTotal > 0 ||
        PackFillFraction >= NeedsBrain.PackedFraction ||
        TimeSinceHunt != TimeSpan.MaxValue ||
        TimeSinceRest != TimeSpan.MaxValue ||
        TimeSinceTown != TimeSpan.MaxValue ||
        PartyForming ||
        !string.IsNullOrWhiteSpace(DayPart) ||
        AmbitionWantsWork ||
        AmbitionWantsHunt ||
        AmbitionWantsTravel;
}

/// <summary>
/// Free decision maker. No model. Scores named routines from cheap loop-side signals.
/// </summary>
public static class NeedsBrain
{
    public const double HurtHitsFraction = 0.45;
    public const double HealthyHitsFraction = 0.85;
    public const int PoorGoldThreshold = 200;
    public const double PackedFraction = 0.75;
    public const double RestWhenHurtWeight = 3.0;
    public const double HuntWhenBravePoorWeight = 2.5;
    public const double DungeonWhenBraveWeight = 2.0;
    public const double PartyFormingWeight = 4.0;
    public const double HuntPenaltyWhenHurt = 2.5;
    public const double CautionRestWeight = 1.2;
    public const double GreedWorkWeight = 1.0;
    public const double RecentActivityHours = 1.0;
    public const double JitterScale = 0.1;
    public const int JitterModulus = 1000;
    public const int JitterSeedPrime = 397;
    public const double UnmetPowerScore = 0.05;
    public const double TrainingPull = 1.5;
    public const double TrainingBand = 0.80;
    public const double AmbitionWorkBoost = 1.2;
    public const double AmbitionHuntBoost = 1.2;
    public const double AmbitionTravelBoost = 2.5;
    public const double NoShopTownCut = 0.4;

    public static string Pick(
        IReadOnlyList<ChoiceDefinition> choices,
        NeedsSnapshot needs,
        int seed,
        Func<int, int> fallbackNext = null,
        DestinationCatalog catalog = null
    )
    {
        if (choices == null || choices.Count == 0)
        {
            return DecideChoice.DefaultRoutineId;
        }

        needs ??= new NeedsSnapshot();

        if (!needs.Drives.IsCustom && !needs.HasSignals)
        {
            return DecideChoice.Fallback(choices, fallbackNext ?? (_ => 0));
        }

        var maxUnmetRequiredPower = MaxUnmetRequiredPower(choices, needs, catalog);
        var trainHunt = InTrainingBand(needs.Power, maxUnmetRequiredPower);
        var bestId = choices[0].Routine;
        var bestScore = double.MinValue;

        for (var i = 0; i < choices.Count; i++)
        {
            var choice = choices[i];
            var id = choice.Routine;
            var score = Score(choice, needs, catalog) + Jitter(seed, id);

            if (trainHunt && PowerMet(choice, needs, catalog) && FamilyOf(id) == RoutineFamily.Hunt)
            {
                score += TrainingPull;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestId = id;
            }
        }

        return bestId;
    }

    public static double Score(ChoiceDefinition choice, NeedsSnapshot needs, DestinationCatalog catalog = null)
    {
        if (choice == null || needs == null || string.IsNullOrWhiteSpace(choice.Routine))
        {
            return 0;
        }

        if (RepeatFailure.IsBlocked(needs.BlockedCount) &&
            !string.IsNullOrWhiteSpace(needs.BlockedRoutine) &&
            needs.BlockedRoutine.Equals(choice.Routine, StringComparison.OrdinalIgnoreCase))
        {
            return UnmetPowerScore;
        }

        if (!PowerMet(choice, needs, catalog))
        {
            return UnmetPowerScore;
        }

        var score = Score(FamilyOf(choice.Routine), needs);

        if (!needs.CanShop && FamilyOf(choice.Routine) == RoutineFamily.Rest)
        {
            score *= NoShopTownCut;
        }

        return score;
    }

    /// <summary>
    /// The pull of one family of activity: drives, hits, gold and pack, then the hour of the
    /// person's day (<see cref="DayShapeRules"/>) and its ambition.
    /// </summary>
    public static double Score(RoutineFamily family, NeedsSnapshot needs)
    {
        if (needs == null)
        {
            return 0;
        }

        var drives = needs.Drives ?? PersonaDrives.Neutral;
        var score = 1.0;
        var hurt = needs.HitsFraction < HurtHitsFraction;
        var healthy = needs.HitsFraction >= HealthyHitsFraction;
        var poor = needs.GoldTotal < PoorGoldThreshold;
        var packed = needs.PackFillFraction >= PackedFraction;
        var recentHunt = IsRecent(needs.TimeSinceHunt);
        var recentRest = IsRecent(needs.TimeSinceRest);

        if (family == RoutineFamily.Rest)
        {
            if (hurt)
            {
                score += RestWhenHurtWeight * Math.Max(drives.Caution, 0.5);
            }

            score += drives.Caution * CautionRestWeight;

            if (packed)
            {
                score += drives.Greed * GreedWorkWeight;
            }

            if (recentRest)
            {
                score -= 1;
            }
        }

        if (family == RoutineFamily.Hunt)
        {
            if (hurt)
            {
                score -= HuntPenaltyWhenHurt * drives.Caution;
            }
            else if (healthy && poor)
            {
                score += HuntWhenBravePoorWeight * drives.Valor * Math.Max(drives.Greed, 0.4);
            }

            score += drives.Valor * 1.0;

            if (recentHunt)
            {
                score -= 1;
            }
        }

        if (family == RoutineFamily.Dungeon)
        {
            if (hurt)
            {
                score -= HuntPenaltyWhenHurt * drives.Caution;
            }
            else if (healthy)
            {
                score += DungeonWhenBraveWeight * drives.Valor;
            }

            if (recentHunt)
            {
                score -= 0.5;
            }
        }

        if (family == RoutineFamily.Work)
        {
            score += drives.Greed * GreedWorkWeight;

            if (packed)
            {
                score -= 1;
            }
        }

        if (family == RoutineFamily.Trade)
        {
            score += drives.Greed * GreedWorkWeight;

            if (packed)
            {
                score += 1;
            }
        }

        if (family == RoutineFamily.Pk)
        {
            score += drives.Valor * DungeonWhenBraveWeight;

            if (hurt)
            {
                score -= HuntPenaltyWhenHurt;
            }
        }

        if (needs.PartyForming && family is RoutineFamily.Hunt or RoutineFamily.Dungeon or RoutineFamily.Party)
        {
            score += PartyFormingWeight;
        }

        score *= DayMultiplier(family, needs.DayPart);

        if (needs.AmbitionWantsWork && family is RoutineFamily.Work or RoutineFamily.Trade)
        {
            score += AmbitionWorkBoost;
        }

        if (needs.AmbitionWantsHunt && family is RoutineFamily.Hunt or RoutineFamily.Dungeon)
        {
            score += AmbitionHuntBoost;
        }

        if (needs.AmbitionWantsTravel && family == RoutineFamily.Leisure)
        {
            score += AmbitionTravelBoost;
        }

        return score;
    }

    public static TimeSpan ElapsedSince(DateTime last, DateTime now)
    {
        if (last == default)
        {
            return TimeSpan.MaxValue;
        }

        var elapsed = now - last;
        return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
    }

    public static RoutineFamily FamilyOf(string routineId)
    {
        if (string.IsNullOrWhiteSpace(routineId))
        {
            return RoutineFamily.Other;
        }

        if (Contains(routineId, "rest") || Contains(routineId, "town") || Contains(routineId, "idle"))
        {
            return RoutineFamily.Rest;
        }

        if (Contains(routineId, "follow") || Contains(routineId, "guild"))
        {
            return RoutineFamily.Party;
        }

        if (Contains(routineId, "pk") || Contains(routineId, "red"))
        {
            return RoutineFamily.Pk;
        }

        if (Contains(routineId, "trade") || Contains(routineId, "shop") || Contains(routineId, "wts") ||
            Contains(routineId, "wtb") || Contains(routineId, "sell"))
        {
            return RoutineFamily.Trade;
        }

        if (Contains(routineId, "despise") || Contains(routineId, "dungeon"))
        {
            return RoutineFamily.Dungeon;
        }

        if (Contains(routineId, "hunt") || Contains(routineId, "graveyard"))
        {
            return RoutineFamily.Hunt;
        }

        if (Contains(routineId, "tavern") ||
            Contains(routineId, "visit") ||
            Contains(routineId, "sightsee") ||
            Contains(routineId, "loiter") ||
            Contains(routineId, "look") ||
            Contains(routineId, "pub"))
        {
            return RoutineFamily.Leisure;
        }

        return RoutineFamily.Work;
    }

    private static double DayMultiplier(RoutineFamily family, string dayPart)
    {
        if (string.IsNullOrWhiteSpace(dayPart) ||
            !Enum.TryParse(dayPart, ignoreCase: true, out DayPart part))
        {
            return DayShapeRules.NeutralWeight;
        }

        return DayShapeRules.WeightMultiplier(part, family);
    }

    private static bool PowerMet(ChoiceDefinition choice, NeedsSnapshot needs, DestinationCatalog catalog)
    {
        var required = DecideChoice.RequiredPowerOf(choice, catalog);
        var family = FamilyOf(choice.Routine);
        var power = DecideChoice.EffectivePower(
            needs.Power,
            needs.AlliesPower,
            family is RoutineFamily.Hunt or RoutineFamily.Dungeon or RoutineFamily.Party
        );
        return required <= 0 || power >= required;
    }

    private static int MaxUnmetRequiredPower(
        IReadOnlyList<ChoiceDefinition> choices,
        NeedsSnapshot needs,
        DestinationCatalog catalog
    )
    {
        var max = 0;

        for (var i = 0; i < choices.Count; i++)
        {
            var required = DecideChoice.RequiredPowerOf(choices[i], catalog);
            var family = FamilyOf(choices[i].Routine);
            var power = DecideChoice.EffectivePower(
                needs.Power,
                needs.AlliesPower,
                family is RoutineFamily.Hunt or RoutineFamily.Dungeon or RoutineFamily.Party
            );
            if (required > max && power < required)
            {
                max = required;
            }
        }

        return max;
    }

    private static bool InTrainingBand(int power, int maxUnmetRequiredPower) =>
        maxUnmetRequiredPower > 0 &&
        power >= maxUnmetRequiredPower * TrainingBand &&
        power < maxUnmetRequiredPower;

    private static bool IsRecent(TimeSpan elapsed) =>
        elapsed != TimeSpan.MaxValue && elapsed < TimeSpan.FromHours(RecentActivityHours);

    private static bool Contains(string routineId, string token) =>
        routineId.Contains(token, StringComparison.OrdinalIgnoreCase);

    /// <summary>A small fixed offset from the seed and the id, so equal scores do not always tie the same way.</summary>
    public static double Jitter(int seed, string id)
    {
        unchecked
        {
            var hash = seed * JitterSeedPrime ^ id.GetHashCode(StringComparison.OrdinalIgnoreCase);
            var unit = (hash & int.MaxValue) % JitterModulus;
            return unit / (double)JitterModulus * JitterScale;
        }
    }
}

public enum RoutineFamily
{
    Rest,
    Hunt,
    Dungeon,
    Work,
    Party,
    Trade,
    Pk,
    Leisure,
    Other
}
