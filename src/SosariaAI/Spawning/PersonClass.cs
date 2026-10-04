namespace SosariaAI.Spawning;

/// <summary>
/// The template a person plays, as players named them in 1998 and 1999. A class sits
/// inside the person's job (worker, fighter, thief, tamer) and its routine work; it
/// picks the skill template, the outfit and the pack, not the routines. Paladin through
/// Ninja came with later expansions and exist only in an era that has their skill (see
/// <see cref="PersonClassRules.Allowed"/>). The four crafters after them live by a trade
/// a worker copy takes up (<see cref="SosariaAI.Skills.CraftCareerRules"/>).
/// </summary>
public enum PersonClass
{
    Warrior,
    Fencer,
    Mage,
    Archer,
    Ranger,
    Healer,
    Bard,
    Tamer,
    Thief,
    TreasureHunter,
    Merchant,
    Smith,
    Tailor,
    Carpenter,
    Miner,
    Lumberjack,
    Fisherman,

    /// <summary>Age of Shadows: a sword dexxer with chivalry.</summary>
    Paladin,

    /// <summary>Age of Shadows: the necro mage, necromancy over a magery core.</summary>
    Necromancer,

    /// <summary>Samurai Empire: a sword dexxer with bushido.</summary>
    Samurai,

    /// <summary>Samurai Empire: a fencer with ninjitsu, hiding and stealth.</summary>
    Ninja,

    /// <summary>A potion brewer at the alchemist's.</summary>
    Alchemist,

    /// <summary>A scroll writer at the mage shop.</summary>
    Scribe,

    /// <summary>A fletcher at the bowyer's.</summary>
    Bowyer,

    /// <summary>A maker of tools and small wares at the tinker's.</summary>
    Tinker
}
