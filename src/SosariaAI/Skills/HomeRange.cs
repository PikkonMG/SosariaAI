using Server;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// A skill that pins the character to a place sets Home and RangeHome. Between skills
/// LoiterInHome pulls the character back there, so the old values are put back when
/// the skill ends.
/// </summary>
public readonly record struct HomeRange(Point3D Home, int RangeHome)
{
    public static HomeRange Capture(SosariaCharacter character) => new(character.Home, character.RangeHome);

    public void Restore(SosariaCharacter character)
    {
        if (character == null)
        {
            return;
        }

        character.Home = Home;
        character.RangeHome = RangeHome;
    }
}
