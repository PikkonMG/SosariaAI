using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Skills;

namespace SosariaAI.Spawning;

/// <summary>
/// Rolls a person's profile from its id and its composed definition. Pure: no world
/// objects, so the spawner and <see cref="PersonMaker"/> roll the same person.
/// </summary>
public static class PersonProfileRules
{
    public const int FemalePercent = 40;
    public const int TraitPercent = 25;

    public const int WealthTierStep = 14;
    public const int WealthDiceSides = 46;
    public const int GreedyWealthBonus = 10;
    public const int GenerousWealthPenalty = 5;
    public const int MerchantWealthBonus = 15;
    public const int ModestWealthScore = 30;
    public const int ComfortableWealthScore = 55;
    public const int RichWealthScore = 80;

    public const double MinPhaseLength = 0.8;
    public const double PhaseLengthStep = 0.05;
    public const int PhaseLengthSteps = 9;
    public const double RestlessPhaseFactor = 0.5;
    public const double HomebodyPhaseFactor = 1.75;

    public const double TraitLean = 0.2;
    public const double SmallTraitLean = 0.15;
    public const double TendencyJitter = 0.1;
    private const int TendencyJitterSteps = 20;

    public static readonly (double Banking, double Adventuring, double Travel, double Crafting, double Idling)
        MerchantLeanings = (Banking: 0.8, Adventuring: 0.15, Travel: 0.5, Crafting: 0.4, Idling: 0.4);

    public static readonly (double Banking, double Adventuring, double Travel, double Crafting, double Idling)
        ThiefLeanings = (Banking: 0.7, Adventuring: 0.3, Travel: 0.4, Crafting: 0.1, Idling: 0.5);

    public static readonly (double Banking, double Adventuring, double Travel, double Crafting, double Idling)
        CrafterLeanings = (Banking: 0.45, Adventuring: 0.2, Travel: 0.3, Crafting: 0.8, Idling: 0.35);

    public static readonly (double Banking, double Adventuring, double Travel, double Crafting, double Idling)
        AdventurerLeanings = (Banking: 0.4, Adventuring: 0.7, Travel: 0.5, Crafting: 0.15, Idling: 0.3);

    public const double NerveValorWeight = 0.6;
    public const double NerveCalmWeight = 0.2;
    public const double NerveTierWeight = 0.2;

    private const int ClassSalt = 401;
    private const int TierSalt = 409;
    private const int GenderSalt = 419;
    private const int WealthSalt = 421;
    private const int PhaseSalt = 431;
    private const int BraveSalt = 433;
    private const int RestlessSalt = 439;
    private const int SocialSalt = 443;
    private const int GreedySalt = 449;
    private const int BankingSalt = 457;
    private const int AdventureSalt = 461;
    private const int TravelSalt = 463;
    private const int CraftSalt = 467;
    private const int IdleSalt = 479;

    public static PersonProfile Roll(string uniqueId, CharacterDefinition definition) =>
        Roll(uniqueId, definition, EraRules.Current());

    /// <summary>The profile of a person on a shard running <paramref name="expansion"/>: only its classes roll.</summary>
    public static PersonProfile Roll(string uniqueId, CharacterDefinition definition, Expansion expansion)
    {
        var build = definition?.Build;
        var personClass = PersonClassRules.Pick(
            PersonJobs.Of(build),
            build?.Preset,
            kind => definition?.UsesSkill(kind) == true,
            CraftCareerRules.CareerOf(definition),
            uniqueId,
            ClassSalt,
            expansion
        );
        var tier = WorkSites.IsCopy(uniqueId)
            ? SkillTierRules.Roll(uniqueId, TierSalt, personClass)
            : SkillTierRules.ForFixture(build?.Veteran == true, uniqueId, TierSalt);
        var traits = RollTraits(uniqueId);

        return new PersonProfile(
            personClass,
            tier,
            traits,
            RollWealth(uniqueId, tier, personClass, traits),
            RollTendencies(uniqueId, personClass, traits),
            RollPhaseLength(uniqueId, traits),
            PersonDice.Chance(uniqueId, GenderSalt, FemalePercent)
        );
    }

    /// <summary>
    /// How readily a person stands and fights, from 0 to 1: mostly its valor, a little
    /// its calm, and a little the confidence of its skill tier.
    /// </summary>
    public static double Nerve(PersonaDrives drives, SkillTier tier)
    {
        var resolved = drives ?? PersonaDrives.Neutral;
        var tierShare = (double)tier / (double)SkillTier.Grandmaster;
        var nerve = resolved.Valor * NerveValorWeight +
                    (PersonaDrives.MaxValue - resolved.Caution) * NerveCalmWeight +
                    tierShare * NerveTierWeight;
        return Math.Clamp(nerve, PersonaDrives.MinValue, PersonaDrives.MaxValue);
    }

    public static PersonTrait RollTraits(string uniqueId) =>
        Pair(uniqueId, BraveSalt, PersonTrait.Brave, PersonTrait.Cautious) |
        Pair(uniqueId, RestlessSalt, PersonTrait.Restless, PersonTrait.Homebody) |
        Pair(uniqueId, SocialSalt, PersonTrait.Social, PersonTrait.Loner) |
        Pair(uniqueId, GreedySalt, PersonTrait.Greedy, PersonTrait.Generous);

    public static PersonWealth RollWealth(string uniqueId, SkillTier tier, PersonClass personClass, PersonTrait traits)
    {
        var score = (int)tier * WealthTierStep + PersonDice.Roll(uniqueId, WealthSalt, WealthDiceSides);

        if ((traits & PersonTrait.Greedy) != 0)
        {
            score += GreedyWealthBonus;
        }

        if ((traits & PersonTrait.Generous) != 0)
        {
            score -= GenerousWealthPenalty;
        }

        if (personClass == PersonClass.Merchant)
        {
            score += MerchantWealthBonus;
        }

        return score switch
        {
            >= RichWealthScore => PersonWealth.Rich,
            >= ComfortableWealthScore => PersonWealth.Comfortable,
            >= ModestWealthScore => PersonWealth.Modest,
            _ => PersonWealth.Poor
        };
    }

    public static double RollPhaseLength(string uniqueId, PersonTrait traits)
    {
        var length = MinPhaseLength + PersonDice.Roll(uniqueId, PhaseSalt, PhaseLengthSteps) * PhaseLengthStep;

        if ((traits & PersonTrait.Restless) != 0)
        {
            length *= RestlessPhaseFactor;
        }

        if ((traits & PersonTrait.Homebody) != 0)
        {
            length *= HomebodyPhaseFactor;
        }

        return length;
    }

    public static ActivityTendencies RollTendencies(string uniqueId, PersonClass personClass, PersonTrait traits)
    {
        var (banking, adventuring, travel, crafting, idling) = ClassLeanings(personClass);

        if ((traits & PersonTrait.Brave) != 0)
        {
            adventuring += TraitLean;
        }

        if ((traits & PersonTrait.Cautious) != 0)
        {
            banking += SmallTraitLean;
            adventuring -= SmallTraitLean;
        }

        if ((traits & PersonTrait.Restless) != 0)
        {
            travel += TraitLean;
            idling -= SmallTraitLean;
        }

        if ((traits & PersonTrait.Homebody) != 0)
        {
            travel -= TraitLean;
            idling += TraitLean;
        }

        if ((traits & PersonTrait.Social) != 0)
        {
            banking += SmallTraitLean;
            idling += SmallTraitLean;
        }

        if ((traits & PersonTrait.Loner) != 0)
        {
            idling -= SmallTraitLean;
            crafting += SmallTraitLean;
        }

        if ((traits & PersonTrait.Greedy) != 0)
        {
            banking += SmallTraitLean;
        }

        return new ActivityTendencies(
            Lean(banking, uniqueId, BankingSalt),
            Lean(adventuring, uniqueId, AdventureSalt),
            Lean(travel, uniqueId, TravelSalt),
            Lean(crafting, uniqueId, CraftSalt),
            Lean(idling, uniqueId, IdleSalt)
        );
    }

    private static (double Banking, double Adventuring, double Travel, double Crafting, double Idling) ClassLeanings(
        PersonClass personClass
    ) =>
        personClass switch
        {
            PersonClass.Merchant => MerchantLeanings,
            PersonClass.Thief => ThiefLeanings,
            _ when PersonClassRules.IsCrafter(personClass) => CrafterLeanings,
            _ => AdventurerLeanings
        };

    private static double Lean(double value, string uniqueId, int salt)
    {
        var jitter = (PersonDice.Roll(uniqueId, salt, TendencyJitterSteps + 1) - TendencyJitterSteps / 2) *
                     (TendencyJitter * 2 / TendencyJitterSteps);
        return Math.Clamp(value + jitter, PersonaDrives.MinValue, PersonaDrives.MaxValue);
    }

    private static PersonTrait Pair(string uniqueId, int salt, PersonTrait first, PersonTrait second)
    {
        var roll = PersonDice.Percent(uniqueId, salt);

        if (roll < TraitPercent)
        {
            return first;
        }

        return roll < TraitPercent * 2 ? second : PersonTrait.None;
    }
}
