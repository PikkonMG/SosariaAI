namespace SosariaAI.Memory;

/// <summary>The kinds of adventure long-term memory keeps (the <c>adventures.kind</c> column).</summary>
public static class AdventureKinds
{
    public const string Dungeon = "dungeon";
    public const string Hunt = "hunt";
    public const string RedKill = "red-kill";
    public const string Death = "death";
    public const string Rescue = "rescue";
    public const string Duel = "duel";
    public const string House = "house";
    public const string FirstKill = "first-kill";

    /// <summary>A notable solo skill that ended, such as a mining or fishing trip.</summary>
    public const string Outing = "outing";
}
