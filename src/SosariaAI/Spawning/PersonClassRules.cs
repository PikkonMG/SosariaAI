using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Skills;

namespace SosariaAI.Spawning;

/// <summary>
/// Picks a class that fits the job and the work the person's routines already do. A worker
/// with a craft career is the class of its trade; any other worker is the class of the work
/// its template does, so a miner's template makes miners, never tailors, and the class never
/// asks for a routine the person does not have. A class the running expansion lacks has no
/// weight: a Second Age shard never rolls a paladin.
/// </summary>
public static class PersonClassRules
{
    public const int MageShare = 75;
    public const int MageHealerShare = 12;
    public const int MageTreasureHunterShare = 13;
    public const int ArcherShare = 60;
    public const int RangerShare = 40;
    public const int WarriorShare = 50;
    public const int FencerShare = 30;
    public const int MeleeHealerShare = 10;
    public const int BardShare = 10;
    public const int LumberjackShare = 65;
    public const int CarpenterShare = 15;
    public const int TreasureHunterShare = 10;
    public const int MerchantShare = 10;
    public const int MageNecromancerShare = 20;
    public const int PaladinShare = 15;
    public const int SamuraiShare = 10;
    public const int NinjaShare = 8;

    /// <param name="career">The trade of the person's craft routine, or null (<see cref="CraftCareerRules.CareerOf"/>).</param>
    public static PersonClass Pick(
        string job,
        string preset,
        Func<string, bool> usesSkill,
        string career,
        string uniqueId,
        int salt,
        Expansion expansion
    )
    {
        var uses = usesSkill ?? (_ => false);

        return job switch
        {
            PersonJobs.Thief => PersonClass.Thief,
            PersonJobs.Tamer => PersonClass.Tamer,
            PersonJobs.Worker => CraftCareerRules.ClassOf(career) ?? PickWorker(uses, uniqueId, salt),
            _ => PickFighter(preset, uses, uniqueId, salt, expansion)
        };
    }

    /// <summary>The first expansion with the skill that makes the class; the Second Age classes need none.</summary>
    public static Expansion Floor(PersonClass personClass) =>
        personClass switch
        {
            PersonClass.Paladin => EraRules.SkillFloor(SkillName.Chivalry),
            PersonClass.Necromancer => EraRules.SkillFloor(SkillName.Necromancy),
            PersonClass.Samurai => EraRules.SkillFloor(SkillName.Bushido),
            PersonClass.Ninja => EraRules.SkillFloor(SkillName.Ninjitsu),
            _ => Expansion.None
        };

    public static bool Allowed(PersonClass personClass, Expansion expansion) => expansion >= Floor(personClass);

    public static bool IsCrafter(PersonClass personClass) =>
        personClass is PersonClass.Smith or PersonClass.Tailor or PersonClass.Carpenter or PersonClass.Miner
            or PersonClass.Lumberjack or PersonClass.Fisherman or PersonClass.Alchemist or PersonClass.Scribe
            or PersonClass.Bowyer or PersonClass.Tinker;

    /// <summary>A person whose trade makes goods at a station: the gatherers bring it stock.</summary>
    public static bool IsMaker(PersonClass personClass) =>
        personClass is PersonClass.Smith or PersonClass.Tailor or PersonClass.Carpenter or PersonClass.Alchemist
            or PersonClass.Scribe or PersonClass.Bowyer or PersonClass.Tinker;

    public static string Title(PersonClass personClass) =>
        personClass switch
        {
            PersonClass.TreasureHunter => "Treasure Hunter",
            PersonClass.Smith => "Blacksmith",
            _ => personClass.ToString()
        };

    // New classes weigh last, so a Second Age roll lands where it always did.
    private static PersonClass PickFighter(
        string preset,
        Func<string, bool> uses,
        string uniqueId,
        int salt,
        Expansion expansion
    )
    {
        if (string.Equals(preset, BuildPresets.Mage, StringComparison.OrdinalIgnoreCase))
        {
            return PersonDice.Weighted(
                    uniqueId,
                    salt,
                    MageShare,
                    MageHealerShare,
                    MageTreasureHunterShare,
                    Share(PersonClass.Necromancer, MageNecromancerShare, expansion)
                ) switch
                {
                    0 => PersonClass.Mage,
                    1 => PersonClass.Healer,
                    2 => PersonClass.TreasureHunter,
                    _ => PersonClass.Necromancer
                };
        }

        if (string.Equals(preset, BuildPresets.Archer, StringComparison.OrdinalIgnoreCase))
        {
            return PersonDice.Weighted(uniqueId, salt, ArcherShare, RangerShare) == 0
                ? PersonClass.Archer
                : PersonClass.Ranger;
        }

        var bard = uses(SkillKinds.Music) || uses(SkillKinds.Provoke) || uses(SkillKinds.Peace) ? BardShare : 0;

        return PersonDice.Weighted(
                uniqueId,
                salt,
                WarriorShare,
                FencerShare,
                MeleeHealerShare,
                bard,
                Share(PersonClass.Paladin, PaladinShare, expansion),
                Share(PersonClass.Samurai, SamuraiShare, expansion),
                Share(PersonClass.Ninja, NinjaShare, expansion)
            ) switch
            {
                0 => PersonClass.Warrior,
                1 => PersonClass.Fencer,
                2 => PersonClass.Healer,
                3 => PersonClass.Bard,
                4 => PersonClass.Paladin,
                5 => PersonClass.Samurai,
                _ => PersonClass.Ninja
            };
    }

    private static int Share(PersonClass personClass, int share, Expansion expansion) =>
        Allowed(personClass, expansion) ? share : 0;

    // A gatherer is the class of its harvest: a template that mines and smiths a dagger now and
    // then is a miner, who sells its ingots to the smiths. A live run turned 55 of every 100 such
    // copies into smiths and spawned no miner at all. Templates that name a trade and gather
    // nothing make the class of that trade.
    private static PersonClass PickWorker(Func<string, bool> uses, string uniqueId, int salt)
    {
        if (uses(SkillKinds.Mine))
        {
            return PersonClass.Miner;
        }

        if (uses(SkillKinds.Fish))
        {
            return PersonClass.Fisherman;
        }

        if (uses(SkillKinds.Tailor))
        {
            return PersonClass.Tailor;
        }

        if (uses(SkillKinds.Lumberjack))
        {
            var carpenter = uses(SkillKinds.Carpentry) ? CarpenterShare : 0;
            var hunter = uses(SkillKinds.Cartography) ? TreasureHunterShare : 0;
            var merchant = uses(SkillKinds.BankShop) || uses(SkillKinds.VendorSell) ? MerchantShare : 0;

            return PersonDice.Weighted(uniqueId, salt, LumberjackShare, carpenter, hunter, merchant) switch
            {
                0 => PersonClass.Lumberjack,
                1 => PersonClass.Carpenter,
                2 => PersonClass.TreasureHunter,
                _ => PersonClass.Merchant
            };
        }

        foreach (var career in CraftCareerRules.Careers)
        {
            if (uses(career.Kind) && CraftCareerRules.ClassOf(career.Kind) is { } crafter)
            {
                return crafter;
            }
        }

        return uses(SkillKinds.Patrol) ? PersonClass.Warrior : PersonClass.Merchant;
    }
}
