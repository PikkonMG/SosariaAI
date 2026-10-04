using Server;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Sixth-circle Mark on a blank rune, as a player marks its bank and its favourite hunt.
/// Novice magery 40 cannot do this. Veteran magery can, and a scroll brings it lower. A
/// mage that walked a long way marks where it arrived, so the next trip there is a recall.
/// The marked rune then goes into the mage's runebook.
/// </summary>
public static class MarkRules
{
    public const double MinMagery = 52;

    /// <summary>
    /// A scroll lowers the circle by two: before Mondain's Legacy the engine checks a sixth-circle
    /// scroll from 30 to 70 Magery (MagerySpell.GetCastSkills); below 30 no cast takes.
    /// </summary>
    public const double ScrollMinMagery = 30;

    /// <summary>
    /// A place wants a rune when the person carries a blank one and none of its marked runes
    /// already lands near here.
    /// </summary>
    public static bool WantsRuneHere(bool hasBlank, bool markedNearHere) => hasBlank && !markedNearHere;

    /// <summary>A place is worth a rune when getting there was a trip long enough to recall.</summary>
    public static bool TripWorthARune(int tripTiles) => tripTiles >= RecallRules.MinTripTiles;

    /// <summary>Begins a real Mark on a blank rune where the person stands, when no rune serves here yet.</summary>
    public static bool TryMarkHere(SosariaCharacter character)
    {
        if (!People.InWorld(character))
        {
            return false;
        }

        var blank = RuneShelf.Blank(character);
        var markedNear = RuneShelf.MarkedNear(character, character.Location, character.Map) != null;

        return WantsRuneHere(blank != null, markedNear) &&
               TravelSpells.Begin(character, TravelSpellKind.Mark, TravelMark.Of(blank));
    }
}
