using System.Collections.Generic;
using Server;

namespace SosariaAI.Combat;

public sealed class ResolvedBuild
{
    public CombatStyle Style { get; init; }

    public CharacterRole Role { get; init; }

    public bool Veteran { get; init; }

    public IReadOnlyDictionary<string, double> Skills { get; init; }

    public int Strength { get; init; }

    public int Dexterity { get; init; }

    public int Intelligence { get; init; }

    public IReadOnlyList<string> Kit { get; init; }

    public bool CanHeal { get; init; }

    public double HealInterval { get; init; }

    public bool IsFighter => Role == CharacterRole.Fighter;

    /// <summary>True when <paramref name="skill"/> is one of the build's skills.</summary>
    public bool Trains(SkillName skill) => Skills?.ContainsKey(skill.ToString()) == true;
}
