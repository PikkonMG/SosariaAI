namespace SosariaAI.Memory;

/// <summary>
/// What a member did in an adventure. Roles describe the member, not the reader: one row is
/// shared by everyone in it. Every role but <see cref="Against"/> stands on the same side.
/// </summary>
public static class AdventureRoles
{
    /// <summary>On the side.</summary>
    public const string With = "with";

    /// <summary>An opponent.</summary>
    public const string Against = "against";

    /// <summary>Healed or resurrected someone.</summary>
    public const string Healer = "healer";

    /// <summary>Landed the kill.</summary>
    public const string Killer = "killer";

    /// <summary>Died.</summary>
    public const string Fallen = "fallen";

    /// <summary>True when two roles stand on the same side of the adventure.</summary>
    public static bool SameSide(string first, string second) => IsOpponent(first) == IsOpponent(second);

    public static bool IsOpponent(string role) => role == Against;
}
