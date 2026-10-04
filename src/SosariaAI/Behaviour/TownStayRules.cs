using SosariaAI.Combat;

namespace SosariaAI.Behaviour;

/// <summary>
/// A worker sells in the town it works from. It does not leave that town to shop.
/// </summary>
public static class TownStayRules
{
    public static bool MaySell(CharacterRole role, bool inTownRegion) =>
        role != CharacterRole.Worker || inTownRegion;
}
