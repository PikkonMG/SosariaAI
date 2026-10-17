using System;
using System.Runtime.CompilerServices;
using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Common;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;

namespace SosariaAI.Combat;

/// <summary>
/// A warrior's or a fencer's fight (<see cref="MeleeTacticsRules"/>): a step out of reach while
/// the bandage works, the draw of one foe off a group, a wall at its back against several foes,
/// and a return, healed, to the lone monster it ran from. A tank mage swings and casts the
/// first circle where it stands, and takes its weapon up again after the words.
/// </summary>
public static partial class CombatBrain
{
    private const string HealOutLine = "heal out";
    private const string WallLine = "wall";
    private const string ComeBackLine = "come back";
    private const string RunnerArrowLine = "runner arrow";

    // Weak keys: a deleted character's weapon note goes with it, as its fight memory does.
    private static readonly ConditionalWeakTable<SosariaCharacter, BaseWeapon> WeaponsBeforeCast = new();

    /// <summary>
    /// One melee think: out of reach while the bandage works (Jev's heal stance, or the rules
    /// when the fight turns), a step back while a foe is near and a stand past it
    /// (<see cref="MeleeTacticsRules.StepsBack"/>); else the draw, else a better stand on the
    /// scan pace, else into reach. A step-out that ended, not pinned, ends its break-away, so
    /// the next one starts fresh.
    /// </summary>
    private static void FightAsMelee(SosariaCharacter character, Memory memory, Mobile foe, CombatStance stance, bool scanned)
    {
        ExpireBreakAway(memory);
        RearmAfterCast(character);
        var reach = Math.Max(MeleeRange, character.Weapon?.MaxRange ?? MeleeRange);

        if (TankMageCasts(character, memory, foe, reach))
        {
            return;
        }
        var healingOut = stance == CombatStance.Heal && JevHolds(memory, CombatStance.Heal) ||
                         StepsOutToHeal(character, memory, foe);

        if (healingOut)
        {
            if (MeleeTacticsRules.StepsBack(memory.Threats.NearestDistance))
            {
                OpenGap(character, memory, foe, FootworkRules.SingleStep);
            }
            else
            {
                character.Motor.Stop();
            }

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

    /// <summary>
    /// A tank mage's words in a melee fight (<see cref="FightsAsTankMage"/>): a Magic Arrow
    /// where it stands that breaks a casting person's words (<see cref="TryBreakFoeCast"/>), a
    /// heal on a hurt friend (<see cref="TryHealerTactics"/>), or a Magic Arrow that catches a foe turned to run out of its weapon's reach
    /// (<see cref="MeleeTacticsRules.ArrowsRunner"/>). Before AOS a blow cannot break the first
    /// circle (Spell.Disturb), so it casts next to the foe. True while words take the think: they
    /// root the feet (Spell.BlocksMovement).
    /// </summary>
    private static bool TankMageCasts(SosariaCharacter character, Memory memory, Mobile foe, int reach)
    {
        if (!FightsAsTankMage(character))
        {
            return false;
        }

        if (character.Spell != null || TryBreakFoeCast(character, memory, foe) ||
            TryHealerTactics(character, memory, foe, SafeCircleNow(character, memory)))
        {
            return true;
        }

        if (!MeleeTacticsRules.ArrowsRunner(NavMetric.Chebyshev(character.Location, foe.Location), reach, FacesAway(foe, character)) ||
            !CastReady(character, memory) || !memory.Kit.Stock.Has(SpellKind.MagicArrow) ||
            !SpellBook.CanCast(SpellBook.MagicArrow, character.Skills.Magery.Value, character.Mana) ||
            !SpellBook.Fits(SpellBook.MagicArrow, SafeCircleNow(character, memory)) || !character.InLOS(foe) ||
            !CastAt(character, memory, foe, SpellKind.MagicArrow, committed: false))
        {
            return false;
        }

        if (LogsOnce(character, RunnerArrowLine, foe))
        {
            logger.Information("{Name} throws a Magic Arrow after {Foe} as it runs out of reach", character.Name, foe.Name);
        }

        return true;
    }

    /// <summary>
    /// Notes the weapon <paramref name="held"/> before a cast when the words put it in the pack
    /// (Mobile.ClearHands), so <see cref="RearmAfterCast"/> takes it up again.
    /// </summary>
    private static void NoteWeaponBeforeCast(SosariaCharacter character, BaseWeapon held)
    {
        if (held != null && held.Parent != character)
        {
            WeaponsBeforeCast.AddOrUpdate(character, held);
        }
    }

    /// <summary>
    /// Takes up again the weapon the last cast put in the pack, once the spell and its cursor
    /// are done: equipping during the words breaks them, even the first circle
    /// (Spell.OnCasterEquipping), and the engine clears the hands again when the cursor is
    /// answered (Spell.CheckSequence). Nothing when the hands hold a weapon already or the
    /// weapon left the pack.
    /// </summary>
    private static void RearmAfterCast(SosariaCharacter character)
    {
        if (character.Spell != null || character.Target != null || !WeaponsBeforeCast.TryGetValue(character, out var weapon))
        {
            return;
        }

        WeaponsBeforeCast.Remove(character);

        if (!weapon.Deleted && HeldWeapon(character) == null && weapon.IsChildOf(character.Backpack))
        {
            GearEquip.EquipTool(character, weapon);
        }
    }

    /// <summary>True when the rules send this melee fighter out of reach while its bandage works (<see cref="MeleeTacticsRules.StepsOutToHeal"/>).</summary>
    private static bool StepsOutToHeal(SosariaCharacter character, Memory memory, Mobile foe) =>
        MeleeTacticsRules.StepsOutToHeal(
            BandageOnSelf(character),
            everyBlowSlips: !Core.AOS,
            Vitals.HitsFraction(character),
            memory.Outlook,
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
