namespace SosariaAI.Skills;

/// <summary>A map-drawing session at the mapmaker, the trade in <see cref="CartographyRules"/>.</summary>
public sealed class CartographyCraftSkill() : CraftStationSkill(CartographyRules.Trade);
