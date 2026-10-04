using Server;
using Server.Multis;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// An owner at its house, the way a player kept one standing: it uses the house sign, and the
/// engine's own sign decides by its era rules whether the visit refreshes the house (a friend
/// before Mondain's Legacy, the owner after). No house is made ageless for a character: one
/// whose owner stops coming decays and falls as on any shard, and the owner forgets it and
/// may buy again (see <see cref="HouseRules.VisitDue"/>).
/// </summary>
public static class HouseVisit
{
    /// <summary>The standing house the character owns: one the engine lists, else the one it recorded; null when none.</summary>
    public static BaseHouse OwnedHouse(SosariaCharacter character)
    {
        foreach (var house in BaseHouse.GetHouses(character))
        {
            if (house is { Deleted: false })
            {
                return house;
            }
        }

        return HouseRules.BySerial(character.HouseSerial) is { Deleted: false } live ? live : null;
    }

    /// <summary>The house has worn far enough that its owner should come and look in on it.</summary>
    public static bool Due(BaseHouse house) =>
        house is { Deleted: false } && HouseRules.VisitDue(house.DecayType, house.DecayLevel);

    /// <summary>Where an owner walks to reach its house: the spot it placed that house from, else the house itself.</summary>
    public static Point3D Doorstep(SosariaCharacter character, BaseHouse house) =>
        character.HouseSerial == (int)house.Serial.Value && character.HouseLocation != Point3D.Zero
            ? character.HouseLocation
            : house.Location;

    /// <summary>World thread. Uses the sign as a player's double-click does, range and sight checks included.</summary>
    public static void UseSign(SosariaCharacter visitor, BaseHouse house)
    {
        if (house is { Deleted: false, Sign: { Deleted: false } sign })
        {
            visitor.Use(sign);
        }
    }

    /// <summary>
    /// A recorded house that no longer stands fell to decay or was taken down: the owner lets it
    /// go, so the next house can be bought.
    /// </summary>
    public static void ForgetFallen(SosariaCharacter character)
    {
        if (character.HouseSerial <= HouseRules.NoHouseSerial ||
            HouseRules.BySerial(character.HouseSerial) is { Deleted: false })
        {
            return;
        }

        character.HouseSerial = HouseRules.NoHouseSerial;
        character.HouseLocation = Point3D.Zero;
        character.Remember(HouseRules.FallenLine);
        WorldPlay.Log($"{character.Name} found its house gone");
    }
}
