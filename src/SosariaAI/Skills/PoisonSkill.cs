using Server;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Coats a bladed or piercing weapon, or food, with a poison potion from the pack, with the
/// engine's check in the potion's own window (<see cref="PoisonRules"/>). The potion goes at
/// the try, passed or not. With no potion or nothing to coat there is no work
/// (<see cref="PoisonRules.HasWork"/>).
/// </summary>
public sealed class PoisonSkill : Skill
{
    public const string NoPotionWhy = "no poison potion in the pack";
    public const string NothingToCoatWhy = "no bladed weapon or food to poison";
    public const string CheckFailedWhy = "the poison did not take";

    private SosariaCharacter _character;

    public override string Name => PoisonRules.Kind;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        return WhyNot(character) is not { } why || CannotStart(why);
    }

    public override SkillStatus Tick()
    {
        if (_character == null || _character.Deleted)
        {
            return Fail(NotInWorldReason);
        }

        if (WhyNot(_character) is { } why)
        {
            return Fail(why);
        }

        var potion = PoisonRules.FindPotion(_character);
        var target = PoisonRules.FindCoatable(_character);
        var poison = potion.Poison;
        var passed = _character.CheckSkill(SkillName.Poisoning, potion.MinPoisoningSkill, potion.MaxPoisoningSkill);
        PoisonRules.ConsumePotion(_character, potion);

        return passed && PoisonRules.Apply(target, poison, _character) ? SkillStatus.Done : Fail(CheckFailedWhy);
    }

    public override void Abort() => _character = null;

    private static string WhyNot(SosariaCharacter character) =>
        !People.InWorld(character) ? NotInWorldReason
        : PoisonRules.FindPotion(character) == null ? NoPotionWhy
        : PoisonRules.FindCoatable(character) == null ? NothingToCoatWhy
        : null;
}
