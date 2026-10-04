using SosariaAI.Spawning;

namespace SosariaAI.Combat;

/// <summary>
/// A shard is mostly middling players with a few grandmasters. Tiers fall on a bell
/// curve, seeded by the person's id; tamers lean high (<see cref="TamerWeights"/>). A tier sets the class skills and how much of the
/// era stat cap the person has trained.
/// </summary>
public static class SkillTierRules
{
    public const int NoviceWeight = 12;
    public const int ApprenticeWeight = 18;
    public const int JourneymanWeight = 22;
    public const int ExpertWeight = 20;
    public const int AdeptWeight = 14;
    public const int MasterWeight = 10;
    public const int GrandmasterWeight = 4;

    /// <summary>
    /// Tamers of the period were mostly high: taming was a long grind, and a tamer that stayed
    /// in the game got there. The tamer weights sum to 100 and put most tamers at Expert or
    /// better, which keeps a low-tier tamer rare.
    /// </summary>
    public const int TamerNoviceWeight = 3;
    public const int TamerApprenticeWeight = 4;
    public const int TamerJourneymanWeight = 8;
    public const int TamerExpertWeight = 25;
    public const int TamerAdeptWeight = 22;
    public const int TamerMasterWeight = 20;
    public const int TamerGrandmasterWeight = 18;

    public const double NoviceSkill = 45;
    public const double ApprenticeSkill = 55;
    public const double JourneymanSkill = 65;
    public const double ExpertSkill = 75;
    public const double AdeptSkill = 85;
    public const double MasterSkill = 95;
    public const double GrandmasterSkill = EraBuildCaps.SkillCap;

    public const int NoviceStatPercent = 70;
    public const int ApprenticeStatPercent = 76;
    public const int JourneymanStatPercent = 82;
    public const int ExpertStatPercent = 88;
    public const int AdeptStatPercent = 94;
    public const int FullStatPercent = 100;

    /// <summary>An authored veteran is a master or better; an authored novice is a journeyman or an expert.</summary>
    private const int FixtureTierChoices = 2;

    public static readonly int[] Weights =
    [
        NoviceWeight, ApprenticeWeight, JourneymanWeight, ExpertWeight, AdeptWeight, MasterWeight, GrandmasterWeight
    ];

    public static readonly int[] TamerWeights =
    [
        TamerNoviceWeight, TamerApprenticeWeight, TamerJourneymanWeight, TamerExpertWeight, TamerAdeptWeight,
        TamerMasterWeight, TamerGrandmasterWeight
    ];

    /// <summary>The tier of a person of <paramref name="personClass"/>: tamers on their own curve.</summary>
    public static SkillTier Roll(string uniqueId, int salt, PersonClass personClass) =>
        (SkillTier)PersonDice.Weighted(uniqueId, salt, WeightsFor(personClass));

    public static int[] WeightsFor(PersonClass personClass) => personClass == PersonClass.Tamer ? TamerWeights : Weights;

    /// <summary>A fixture keeps its authored veteran flag; the dice only pick within it.</summary>
    public static SkillTier ForFixture(bool veteran, string uniqueId, int salt)
    {
        var step = PersonDice.Roll(uniqueId, salt, FixtureTierChoices);
        return veteran
            ? SkillTier.Master + step
            : SkillTier.Journeyman + step;
    }

    public static bool IsVeteran(SkillTier tier) => tier >= SkillTier.Adept;

    /// <summary>The class's lead skill at this tier.</summary>
    public static double PrimarySkill(SkillTier tier) =>
        tier switch
        {
            SkillTier.Novice => NoviceSkill,
            SkillTier.Apprentice => ApprenticeSkill,
            SkillTier.Journeyman => JourneymanSkill,
            SkillTier.Expert => ExpertSkill,
            SkillTier.Adept => AdeptSkill,
            SkillTier.Master => MasterSkill,
            _ => GrandmasterSkill
        };

    /// <summary>The share of the class's full stat line this tier has trained.</summary>
    public static int StatPercent(SkillTier tier) =>
        tier switch
        {
            SkillTier.Novice => NoviceStatPercent,
            SkillTier.Apprentice => ApprenticeStatPercent,
            SkillTier.Journeyman => JourneymanStatPercent,
            SkillTier.Expert => ExpertStatPercent,
            SkillTier.Adept => AdeptStatPercent,
            _ => FullStatPercent
        };
}
