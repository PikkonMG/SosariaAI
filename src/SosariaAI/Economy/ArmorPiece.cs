namespace SosariaAI.Economy;

/// <summary>
/// The piece of a suit an armor claim names. A chest takes more metal and more coin than a
/// gorget. A full set is every piece at once. A weapon, a shield, or armor with no piece
/// named is <see cref="Whole"/>.
/// </summary>
public enum ArmorPiece
{
    Whole,
    Chest,
    Legs,
    Arms,
    Helm,
    Gorget,
    Gloves,
    FullSet
}
