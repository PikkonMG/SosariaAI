using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>Marks a blank rune where the person stands with the real spell.</summary>
public sealed class MarkSkill : TravelCastSkill
{
    public override string Name => SkillKinds.Mark;

    protected override bool BeginCast(SosariaCharacter character) => MarkRules.TryMarkHere(character);
}
