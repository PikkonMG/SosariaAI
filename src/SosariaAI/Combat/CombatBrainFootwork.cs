using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
using Server.Spells.First;
using Server.Spells.Second;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;

namespace SosariaAI.Combat;

/// <summary>
/// Footwork: reads when the next blow can land, steps clear of it at the lawful run pace, and
/// keeps the count of casts that blows broke. The engine's own clocks decide it: a foe's step
/// pace, its weapon reach, and its swing clock.
/// </summary>
public static partial class CombatBrain
{
    /// <summary>
    /// Reads, from the last scan's foes plus the target, when the soonest blow on this
    /// character can land, how many foes stand in reach, and where the close ones gather.
    /// </summary>
    private static void ReadThreats(SosariaCharacter character, Memory memory, Mobile foe)
    {
        var read = new ThreatRead { SoonestBlowMs = CastTiming.NoBlowMs, NearestDistance = int.MaxValue };
        var sumX = 0;
        var sumY = 0;
        var now = Core.TickCount;
        var sawFoe = false;

        for (var i = 0; i < memory.Foes.Count; i++)
        {
            var sighting = memory.Foes[i];

            if (sighting.Harmless)
            {
                continue;
            }

            sawFoe |= sighting.Mobile == foe;
            ReadOne(character, sighting.Mobile, foe, now, ref read, ref sumX, ref sumY);
        }

        if (!sawFoe)
        {
            ReadOne(character, foe, foe, now, ref read, ref sumX, ref sumY);
        }

        if (read.Close > 0)
        {
            read.CenterX = sumX / read.Close;
            read.CenterY = sumY / read.Close;
        }

        memory.AdjacentSince = read.Adjacent == 0 ? 0 : memory.AdjacentSince == 0 ? now : memory.AdjacentSince;
        memory.Threats = read;
    }

    private static void ReadOne(
        SosariaCharacter character,
        Mobile mobile,
        Mobile foe,
        long now,
        ref ThreatRead read,
        ref int sumX,
        ref int sumY
    )
    {
        // Only the target and what is fighting this character throw blows at it.
        if (mobile?.Deleted != false || !mobile.Alive || mobile.Map != character.Map ||
            mobile != foe && mobile.Combatant != character)
        {
            return;
        }

        var distance = NavMetric.Chebyshev(character.Location, mobile.Location);
        var reach = mobile.Weapon?.MaxRange ?? MeleeRange;
        var held = mobile.Paralyzed || mobile.Frozen;
        var blow = CastTiming.BlowInMs(distance, reach, StepMsOf(mobile), mobile.NextCombatTime - now, held);

        read.SoonestBlowMs = Math.Min(read.SoonestBlowMs, blow);
        read.NearestDistance = Math.Min(read.NearestDistance, distance);

        if (!held && distance <= MeleeRange)
        {
            read.Adjacent++;
        }

        if (distance <= FootworkRules.CrowdTiles)
        {
            read.Close++;
            sumX += mobile.X;
            sumY += mobile.Y;
        }
    }

    /// <summary>A creature walks at its engine pace; a person runs at the client's run pace.</summary>
    private static int StepMsOf(Mobile mobile)
    {
        if (mobile is BaseCreature creature)
        {
            var seconds = creature.ActiveMoveSpeed > 0 ? creature.ActiveMoveSpeed : creature.ActiveSpeed;
            return (int)(seconds * MillisecondsPerSecond);
        }

        return RunRules.StepDelay(mobile.Mounted, running: true);
    }

    /// <summary>The highest circle this caster can finish before the soonest blow, by this era's rules.</summary>
    private static int SafeCircleNow(SosariaCharacter character, Memory memory) =>
        CastTiming.SafeCircle(
            memory.Threats.SoonestBlowMs,
            SlowedByProtection(character),
            firstCircleShrugsHits: !Core.AOS
        );

    private static bool SlowedByProtection(Mobile mobile) =>
        Core.UOR && ProtectionSpell.Registry.ContainsKey(mobile);

    /// <summary>The ward of the era is up: Protection from UOR on, Reactive Armor before.</summary>
    private static bool WardUp(Mobile mobile) =>
        Core.UOR ? ProtectionSpell.Registry.ContainsKey(mobile) : ReactiveArmorSpell.HasEffect(mobile);

    /// <summary>
    /// Steps clear: away from the middle of a crowd, or from the target alone, two steps when
    /// something is right there. The steps go on the move clock, so this is the run pace of a
    /// player, not a burst. Marks the break-away start and whether the body was pinned.
    /// </summary>
    private static void OpenGap(SosariaCharacter character, Memory memory, Mobile foe, int steps)
    {
        var threats = memory.Threats;
        var from = FootworkRules.IsCrowd(threats.Close)
            ? new Point3D(threats.CenterX, threats.CenterY, character.Z)
            : foe.Location;

        if (from.X == character.X && from.Y == character.Y)
        {
            from = foe.Location;
        }

        if (memory.KitingSince == 0)
        {
            memory.KitingSince = Core.TickCount;
        }

        memory.Pinned = !character.Motor.BackAway(from, steps);
    }

    private static long KitingMs(Memory memory) =>
        memory.KitingSince == 0 ? 0 : Core.TickCount - memory.KitingSince;

    private static void EndKite(Memory memory)
    {
        memory.KitingSince = 0;
        memory.Pinned = false;
        memory.Committed = false;
    }

    /// <summary>A break-away that ran its grace and its stand is dropped, so the next one starts fresh.</summary>
    private static void ExpireBreakAway(Memory memory)
    {
        if (FootworkRules.BreakAwayExpired(KitingMs(memory)))
        {
            EndKite(memory);
        }
    }

    /// <summary>Why a fighter acts where it stands, for the log.</summary>
    private static string CommitReason(Memory memory, CombatStance stance) =>
        stance == CombatStance.Hold ? "holding" : memory.Pinned ? "pinned" : "outpaced";

    /// <summary>A person casting within reach is worth a swing more than anything else: it breaks the words.</summary>
    private static Mobile TurnOnCaster(SosariaCharacter character, Memory memory, Mobile foe)
    {
        var reach = character.Weapon?.MaxRange ?? MeleeRange;

        if (IsBreakable(foe))
        {
            return foe;
        }

        for (var i = 0; i < memory.Foes.Count; i++)
        {
            var mobile = memory.Foes[i].Mobile;

            if (mobile == foe || !memory.Foes[i].Acquirable || mobile?.Deleted != false || !mobile.Alive ||
                !FootworkRules.ShouldTurnOnCaster(
                    mobile.Player,
                    IsCastingNow(mobile),
                    NavMetric.Chebyshev(character.Location, mobile.Location),
                    reach
                ) || !character.InLOS(mobile))
            {
                continue;
            }

            character.Combatant = mobile;
            character.FocusMob = mobile;
            memory.NextSwitchAt = Core.TickCount + FocusRules.SwitchCooldownMs;
            memory.FoeLostSince = 0;
            memory.Unreachable = false;

            if (SosariaSettings.LogActivity)
            {
                logger.Information("{Name} turns on {Caster} to break the spell", character.Name, mobile.Name);
            }

            return mobile;
        }

        return foe;
    }

    private static bool IsBreakable(Mobile mobile) => mobile.Player && IsCastingNow(mobile);

    private static bool IsCastingNow(Mobile mobile) => mobile.Spell is Spell { IsCasting: true };

    /// <summary>How long the words the foe is saying still take, or zero when it says none.</summary>
    private static long FoeCastLeftMs(Mobile foe) =>
        foe.Spell is Spell { IsCasting: true } spell
            ? spell.StartCastTime + (long)spell.GetCastDelay().TotalMilliseconds - Core.TickCount
            : 0;

    /// <summary>
    /// A cast ended with no target cursor: a blow broke it. Counted for the stance rules, for
    /// this fight's rate, and for the shard's interrupt rate per cast (<see cref="CastTally"/>).
    /// </summary>
    private static void NoteInterrupt(SosariaCharacter character, Memory memory)
    {
        memory.Interrupts.Note(Core.TickCount);
        memory.BrokenThisFight++;
        CastTally.NoteBroken(SpellBook.EntryOf(memory.CastKind).Circle);

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} was interrupted casting {Spell} by {Foe} ({Broken} of {Casts} casts this fight)",
                character.Name,
                memory.CastKind,
                character.FindMostRecentDamager(false)?.Name ?? character.Combatant?.Name ?? "a blow",
                memory.BrokenThisFight,
                memory.CastsThisFight
            );
        }
    }

    /// <summary>The pack's remedies and the reagents the stance rules count on. Read on the scan pace.</summary>
    private static void ReadKit(SosariaCharacter character, Memory memory)
    {
        var pack = character.Backpack;

        if (pack == null)
        {
            memory.Kit = default;
            return;
        }

        memory.Kit = new Supplies
        {
            HealPotion = pack.FindItemByType<BaseHealPotion>() != null,
            Bandage = character.Skills.Healing.Value > SelfCareRules.NoSkill && pack.FindItemByType<Bandage>() != null,
            HealReagents = HasReagents(pack, SpellKind.Heal, character),
            WardReagents = HasReagents(pack, SpellBook.WardOf(Core.UOR), character),
            ParalyzeReagents = HasReagents(pack, SpellKind.Paralyze, character)
        };
    }

    private static bool HasReagents(Container pack, SpellKind kind, Mobile caster) =>
        SpellCasting.HasReagents(pack, NewSpell(kind, caster).Info);
}
