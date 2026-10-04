using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using SosariaAI.Combat;

namespace SosariaAI.Navigation.Generation;

/// <summary>
/// A creature type's stats, read once from fresh instances of the engine's own class: its hits,
/// strength and average blow, and its arts (<see cref="HostileArts"/>): the Magery of a caster,
/// a breath, the poison on its blows, and a bow, read as the fight reads a live foe
/// (<see cref="HostileRead"/>). The engine rolls each instance's hits, strength
/// and Magery in a range, so the stats are the mean of <see cref="ProbeInstances"/> instances:
/// one roll moved a floor's rating by a tenth from one boot to the next.
/// </summary>
public static class CreatureStatsLookup
{
    /// <summary>How many instances of a type are made, read and deleted to rate it.</summary>
    public const int ProbeInstances = 8;

    private readonly record struct Probe(HostileStats? Stats, bool IsLandEnemy);

    private static readonly Dictionary<string, Probe> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static HostileStats? TryLive(string typeName) => ProbeOf(typeName).Stats;

    /// <summary>
    /// True when the type is a creature a lawful fighter may kill and can walk to. A
    /// spawner also holds vendors, guildmasters, animals, reagents and sea creatures.
    /// </summary>
    public static bool IsLandEnemy(string typeName) => ProbeOf(typeName).IsLandEnemy;

    private static Probe ProbeOf(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return default;
        }

        if (Cache.TryGetValue(typeName, out var cached))
        {
            return cached;
        }

        var probe = default(Probe);

        try
        {
            var type = AssemblyHandler.FindTypeByName(typeName);

            if (type != null && typeof(BaseCreature).IsAssignableFrom(type))
            {
                probe = MeanOf(type);
            }
        }
        catch
        {
            probe = default;
        }

        Cache[typeName] = probe;
        return probe;
    }

    /// <summary>
    /// The mean stats of <see cref="ProbeInstances"/> fresh instances of a creature type, each
    /// deleted once read; whether it is an enemy on land reads the same on every instance.
    /// </summary>
    private static Probe MeanOf(Type type)
    {
        long hits = 0, strength = 0, damage = 0, spellSkill = 0;
        HostileArts arts = default;
        var landEnemy = false;
        var made = 0;

        for (var i = 0; i < ProbeInstances; i++)
        {
            if (Activator.CreateInstance(type) is not BaseCreature creature)
            {
                break;
            }

            try
            {
                arts = HostileRead.ArtsOf(creature);
                hits += creature.HitsMax;
                strength += creature.RawStr;
                damage += HostileRead.AverageBlow(creature);
                spellSkill += arts.SpellSkill;
                landEnemy = IsLandEnemy(creature);
                made++;
            }
            finally
            {
                creature.Delete();
            }
        }

        return made == 0
            ? default
            : new Probe(
                new HostileStats(
                    (int)(hits / made),
                    (int)(strength / made),
                    (int)(damage / made),
                    arts with { SpellSkill = (int)(spellSkill / made) }
                ),
                landEnemy
            );
    }

    private static bool IsLandEnemy(BaseCreature creature) =>
        !creature.CantWalk &&
        EnemyRules.IsEnemy(
            creature.Karma,
            creature.AlwaysMurderer,
            isPlayer: false,
            isSosaria: false,
            isControlled: false,
            isSummoned: false,
            creature is BaseVendor,
            creature.Blessed || creature.IsInvulnerable
        );

}
