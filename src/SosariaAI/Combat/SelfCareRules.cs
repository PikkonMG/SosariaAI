namespace SosariaAI.Combat;

public enum CareChoice
{
    None,
    CureSpell,
    CurePotion,
    Bandage,
    HealPotion,
    GreaterHealSpell,
    HealSpell
}

/// <summary>
/// What the self-care call reads, as plain values. Each Ready flag is one cooldown.
/// <see cref="SafeCircle"/> is the highest circle whose words finish before the next blow.
/// </summary>
public readonly record struct CareFacts(
    bool Poisoned,
    double HitsFraction,
    bool InFight,
    int SafeCircle,
    bool SpellReady,
    double Magery,
    int Mana,
    bool PotionReady,
    bool HasCurePotion,
    bool HasHealPotion,
    bool BandageReady,
    bool HasBandage,
    double Healing,
    double Anatomy
);

/// <summary>
/// A player looks after itself in and out of a fight: cure first when poisoned, then a
/// potion when the fight is going badly, a bandage for anyone with Healing, and a heal spell
/// for anyone with Magery. A heal spell is cast only when its words finish before the next
/// blow. Heal potions and heal spells do nothing for the poisoned (engine rule).
/// </summary>
public static class SelfCareRules
{
    /// <summary>In a fight a player heals below this; at rest it tops up at the town heal line.</summary>
    public const double FightHealFraction = 0.70;
    public const double RestHealFraction = SosariaCombat.HealTriggerHitsFraction;

    /// <summary>A heal potion in a fight is for real trouble, not a scratch.</summary>
    public const double PotionHitsFraction = 0.45;

    /// <summary>Greater Heal is for a deep wound; a light one takes the cheap Heal.</summary>
    public const double GreaterHealHitsFraction = 0.60;

    /// <summary>Pre-AOS a bandage cures poison only with this Healing and Anatomy.</summary>
    public const double BandageCureSkill = 60;
    public const double NoSkill = 0;

    /// <summary>A caster sits down to meditate below this share of its mana.</summary>
    public const double MeditateBelowManaFraction = 0.5;

    /// <summary>A caster that sat down to meditate gets up at this share of its mana.</summary>
    public const double RestedManaFraction = 0.9;

    public static double HealLine(bool inFight) => inFight ? FightHealFraction : RestHealFraction;

    /// <summary>
    /// A caster low on mana meditates after the fight until it is rested. The two lines keep it
    /// from sitting down and standing up over one point of mana.
    /// </summary>
    public static bool ShouldMeditate(bool meditating, bool caster, double manaFraction) =>
        caster && manaFraction < (meditating ? RestedManaFraction : MeditateBelowManaFraction);

    /// <summary>True when something at hand would treat this wound now: the heal line and cooldowns apply.</summary>
    public static bool HasRemedy(CareFacts facts) => Choose(facts) != CareChoice.None;

    /// <summary>Cheap gate before any backpack search: nothing to do when clean and above the line.</summary>
    public static bool NeedsCare(bool poisoned, double hitsFraction, bool inFight) =>
        poisoned || hitsFraction < HealLine(inFight);

    public static CareChoice Choose(CareFacts facts) =>
        facts.Poisoned ? ChooseCure(facts) : ChooseHeal(facts);

    /// <summary>The spell a choice casts, or null for a potion, a bandage or nothing.</summary>
    public static SpellKind? SpellOf(CareChoice choice) =>
        choice switch
        {
            CareChoice.CureSpell => SpellKind.Cure,
            CareChoice.GreaterHealSpell => SpellKind.GreaterHeal,
            CareChoice.HealSpell => SpellKind.Heal,
            _ => null
        };

    /// <summary>
    /// True when, given open ground, the care would be a spell whose words do not finish
    /// before the next blow: the caster should step clear first.
    /// </summary>
    public static bool WantsRoom(CareFacts facts) =>
        SpellOf(Choose(facts with { SafeCircle = CastTiming.TopCircle })) is { } spell &&
        !SpellBook.Fits(SpellBook.EntryOf(spell), facts.SafeCircle);

    private static CareChoice ChooseCure(CareFacts facts)
    {
        if (MaySpell(facts, SpellBook.Cure))
        {
            return CareChoice.CureSpell;
        }

        if (facts.PotionReady && facts.HasCurePotion)
        {
            return CareChoice.CurePotion;
        }

        if (MayBandage(facts) && facts.Healing >= BandageCureSkill && facts.Anatomy >= BandageCureSkill)
        {
            return CareChoice.Bandage;
        }

        return CareChoice.None;
    }

    private static CareChoice ChooseHeal(CareFacts facts)
    {
        if (facts.HitsFraction >= HealLine(facts.InFight))
        {
            return CareChoice.None;
        }

        var potionNow = facts.InFight
            ? facts.HitsFraction < PotionHitsFraction
            : facts.HitsFraction < RecoveryRules.RecoverBelowHitsFraction;

        if (potionNow && facts.PotionReady && facts.HasHealPotion)
        {
            return CareChoice.HealPotion;
        }

        if (MayBandage(facts))
        {
            return CareChoice.Bandage;
        }

        if (facts.HitsFraction < GreaterHealHitsFraction && MaySpell(facts, SpellBook.GreaterHeal))
        {
            return CareChoice.GreaterHealSpell;
        }

        return MaySpell(facts, SpellBook.Heal) ? CareChoice.HealSpell : CareChoice.None;
    }

    private static bool MaySpell(CareFacts facts, SpellEntry entry) =>
        facts.SpellReady &&
        SpellBook.CanCast(entry, facts.Magery, facts.Mana) &&
        SpellBook.Fits(entry, facts.SafeCircle);

    private static bool MayBandage(CareFacts facts) =>
        facts.BandageReady && facts.HasBandage && facts.Healing > NoSkill;
}
