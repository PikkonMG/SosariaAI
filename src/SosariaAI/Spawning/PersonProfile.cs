using SosariaAI.Combat;

namespace SosariaAI.Spawning;

/// <summary>
/// Who a person is beyond its job: class, skill tier, temper, savings and leanings.
/// Rolled from the character id, so it is rebuilt the same on every boot and needs no save.
/// </summary>
public sealed class PersonProfile
{
    public const double NeutralPhaseLength = 1;

    public static PersonProfile Default { get; } = new(
        PersonClass.Warrior,
        SkillTier.Journeyman,
        PersonTrait.None,
        PersonWealth.Modest,
        ActivityTendencies.Even,
        NeutralPhaseLength,
        female: false
    );

    public PersonProfile(
        PersonClass personClass,
        SkillTier tier,
        PersonTrait traits,
        PersonWealth wealth,
        ActivityTendencies tendencies,
        double phaseLengthMultiplier,
        bool female
    )
    {
        Class = personClass;
        Tier = tier;
        Traits = traits;
        Wealth = wealth;
        Tendencies = tendencies ?? ActivityTendencies.Even;
        PhaseLengthMultiplier = phaseLengthMultiplier;
        Female = female;
    }

    public PersonClass Class { get; }

    public SkillTier Tier { get; }

    public PersonTrait Traits { get; }

    public PersonWealth Wealth { get; }

    public ActivityTendencies Tendencies { get; }

    /// <summary>Scales the length of a long activity: a restless person switches sooner, a homebody stays longer.</summary>
    public double PhaseLengthMultiplier { get; }

    public bool Female { get; }

    public bool IsVeteran => SkillTierRules.IsVeteran(Tier);

    public bool Has(PersonTrait trait) => (Traits & trait) == trait && trait != PersonTrait.None;

    public string Describe() => $"{Tier} {PersonClassRules.Title(Class)}";
}
