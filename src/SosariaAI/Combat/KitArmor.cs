namespace SosariaAI.Combat;

/// <summary>How much armor a class wears. The skill tier picks the set inside the weight.</summary>
public enum KitArmor
{
    /// <summary>Robes and cloth only: casters, bards, merchants.</summary>
    None,

    /// <summary>Gloves and a few leather pieces over work clothes.</summary>
    Work,

    /// <summary>A partial leather suit: thieves and tamers who move light.</summary>
    Light,

    /// <summary>Leather, then studded at higher tiers, bone for a few veterans: archers, rangers, tank mages.</summary>
    Medium,

    /// <summary>Studded, ring, chain, then plate: the dexxer's climb.</summary>
    Heavy
}
