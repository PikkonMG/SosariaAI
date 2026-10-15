using System;
using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Common;
using SosariaAI.Mobiles;

namespace SosariaAI.Combat;

/// <summary>
/// A warrior's or a fencer's fight (<see cref="MeleeTacticsRules"/>): a step out of reach while
/// the bandage works, the draw of one foe off a group, a wall at its back against several foes,
/// and a return, healed, to the lone monster it ran from.
/// </summary>
public static partial class CombatBrain
{
    private const string HealOutLine = "heal out";
    private const string WallLine = "wall";
    private const string ComeBackLine = "come back";

    /// <summary>
    /// One melee think: out of reach while the bandage works (Jev's heal stance, or the rules
    /// when the fight turns), else the draw, else a better stand on the scan pace, else into
    /// reach. A step-out that ended, not pinned, ends its break-away, so the next one starts fresh.
    /// </summary>
    private static void FightAsMelee(SosariaCharacter character, Memory memory, Mobile foe, CombatStance stance, bool scanned)
    {
        ExpireBreakAway(memory);
        var reach = Math.Max(MeleeRange, character.Weapon?.MaxRange ?? MeleeRange);
        var healingOut = stance == CombatStance.Heal && JevHolds(memory, CombatStance.Heal) && memory.Threats.Adjacent > 0 ||
                         StepsOutToHeal(character, memory, foe);

        if (healingOut)
        {
            OpenGap(character, memory, foe, FootworkRules.SingleStep);

            if (LogsOnce(character, HealOutLine, foe))
            {
                logger.Information("{Name} steps out of {Foe}'s reach while it heals", character.Name, foe.Name);
            }

            return;
        }

        if (memory.KitingSince != 0 && !memory.Pinned)
        {
            EndKite(memory);
        }

        if (Draws(character, memory, foe) || scanned && TakesStand(character, memory, foe, reach, stance))
        {
            return;
        }

        Approach(character, memory, foe, reach);
    }

    /// <summary>True when the rules send this melee fighter out of reach while its bandage works (<see cref="MeleeTacticsRules.StepsOutToHeal"/>).</summary>
    private static bool StepsOutToHeal(SosariaCharacter character, Memory memory, Mobile foe) =>
        MeleeTacticsRules.StepsOutToHeal(
            BandageOnSelf(character),
            everyBlowSlips: !Core.AOS,
            Vitals.HitsFraction(character),
            memory.Outlook,
            memory.Threats.Adjacent,
            StyleOfFoe(foe) != CombatStyle.Melee,
            memory.Pinned
        );

    /// <summary>The engine is putting a bandage on this character from its own hands (BandageContext).</summary>
    private static bool BandageOnSelf(Mobile character) =>
        BandageContext.GetContext(character) is { } context && context.Patient == character;

    /// <summary>
    /// A melee fighter stepping out to heal keeps its foe: the target switch waits, and the
    /// fighter goes back in on the same foe once the bandage is done.
    /// </summary>
    private static bool HoldsTargetToHeal(SosariaCharacter character, Memory memory, Mobile foe) =>
        StyleOf(character, IsSpellFighter(character)) == CombatStyle.Melee && StepsOutToHeal(character, memory, foe);

    /// <summary>
    /// With several foes close, steps to the neighbouring tile with a wall or a rock at its
    /// back (<see cref="EscapeRules.ChooseStand"/>), the foe still in reach. True when it stepped.
    /// </summary>
    private static bool TakesStand(SosariaCharacter character, Memory memory, Mobile foe, int reach, CombatStance stance)
    {
        if (!MeleeTacticsRules.WeighsWall(memory.Threats.Close, memory.PullFrom != null, stance) ||
            !CanWalk(character) || !character.Motor.CanMoveNow)
        {
            return false;
        }

        var direction = EscapeRules.ChooseStand(
            character.Motor.EscapeStep,
            character.Location,
            foe.Location,
            reach,
            memory.IdleFoes
        );

        if (direction == EscapeRules.NoChoice || !character.Motor.DoMove((Direction)direction))
        {
            return false;
        }

        if (LogsOnce(character, WallLine, foe))
        {
            logger.Information(
                "{Name} puts its back to the wall at {Location} against {Count} foes",
                character.Name,
                character.Location,
                memory.Threats.Close
            );
        }

        return true;
    }

    /// <summary>
    /// The index in the last scan of the monster this melee fighter ran from, when it goes back
    /// for it healed (<see cref="MeleeTacticsRules.ComesBackFor"/>) and has it in sight; else -1.
    /// The avoided ground does not hide it: the monster stands alone now.
    /// </summary>
    private static int ComeBackIndex(SosariaCharacter character, Memory memory)
    {
        if (memory.LastFleeFrom is not { } left || !IsFighter(character) ||
            StyleOf(character, IsSpellFighter(character)) != CombatStyle.Melee)
        {
            return -1;
        }

        var index = IndexOf(memory, left);

        if (index < 0)
        {
            return -1;
        }

        var sighting = memory.Foes[index];
        var comesBack = MeleeTacticsRules.ComesBackFor(
            sighting.IsPerson,
            sighting.Harmless,
            left is BaseCreature { BardPacified: true },
            sighting.Packmates,
            Vitals.HitsFraction(character),
            sighting.HitsFraction,
            Core.TickCount - memory.LastFleeAt
        );

        return comesBack && !sighting.OutOfSight && character.InLOS(left) ? index : -1;
    }

    /// <summary>Logs a return to the monster run from, once.</summary>
    private static void NoteComeBack(SosariaCharacter character, Mobile foe)
    {
        if (LogsOnce(character, ComeBackLine, foe))
        {
            logger.Information("{Name} healed up and goes back for {Foe}", character.Name, foe.Name);
        }
    }
}
