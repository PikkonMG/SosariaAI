using System;
using System.Collections.Generic;
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
    /// character can land, how many foes stand in reach, where the close ones gather, and where
    /// the hostiles not on it yet stand (<see cref="ReadIdleFoes"/>). A harmless creature counts
    /// once it is hitting this character: its blows break a spell like any other, and left out
    /// they broke casts the read said were safe.
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

            if (sighting.Harmless && sighting.Mobile.Combatant != character)
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
        ReadIdleFoes(character, memory, foe);
    }

    /// <summary>
    /// Where the hostiles of the last scan stand that rate as a threat and are not on this
    /// character yet, <paramref name="target"/> aside: the back-off steps and the run legs keep
    /// off them (<see cref="EscapeRules.IdleFoeTiles"/>). A step scored on the ground alone
    /// backed a fighter into the next monster and pulled it into the fight.
    /// </summary>
    private static void ReadIdleFoes(SosariaCharacter character, Memory memory, Mobile target)
    {
        var idle = memory.IdleFoes;
        idle.Clear();

        for (var i = 0; i < memory.Foes.Count; i++)
        {
            var sighting = memory.Foes[i];
            var mobile = sighting.Mobile;

            if (sighting.Harmless || mobile == target || mobile?.Deleted != false || !mobile.Alive ||
                mobile.Map != character.Map || mobile.Combatant == character)
            {
                continue;
            }

            idle.Add(mobile.Location);
        }
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
        CastTiming.SafeCircle(memory.Threats.SoonestBlowMs, Core.AOS, SlowedByProtection(character));

    /// <summary>Protection is up; whether it slows the words is the era's call (<see cref="CastTiming.CastDelayMs"/>).</summary>
    private static bool SlowedByProtection(Mobile mobile) => ProtectionSpell.Registry.ContainsKey(mobile);

    /// <summary>The ward of the era is up: Protection from UOR on, Reactive Armor before.</summary>
    private static bool WardUp(Mobile mobile) =>
        Core.UOR ? ProtectionSpell.Registry.ContainsKey(mobile) : ReactiveArmorSpell.HasEffect(mobile);

    /// <summary>
    /// Steps clear: away from the middle of a crowd, or from the target alone, two steps when
    /// something is right there, and not toward a hostile that is not on it yet. The steps go
    /// on the move clock, so this is the run pace of a player, not a burst. Marks the
    /// break-away start and whether the body was pinned.
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

        memory.Pinned = !character.Motor.BackAway(from, steps, memory.IdleFoes);
    }

    /// <summary>Steps clear of the blows: two steps when a foe is right there, else one (<see cref="FootworkRules.RetreatSteps"/>).</summary>
    private static void StepOut(SosariaCharacter character, Memory memory, Mobile foe) =>
        OpenGap(character, memory, foe, FootworkRules.RetreatSteps(memory.Threats.NearestDistance));

    /// <summary>The footwork call for this tick (<see cref="FootworkRules.Next"/>), once a spent break-away is dropped.</summary>
    private static Footwork FootworkNow(Memory memory, bool holds, CombatStance stance)
    {
        ExpireBreakAway(memory);
        return FootworkRules.Next(holds, KitingMs(memory), memory.Pinned, stance);
    }

    /// <summary>
    /// Reads the bow's swing clock: an arrow that left since the last read ends the break-away
    /// that bought it (<see cref="FootworkRules.Fired"/>). A shot where the archer stands keeps
    /// the clock, so the stand ends and the steps resume, as a cast where it stands does. Ended
    /// on every step that gained a tile instead, the grace restarted before the bow had stood
    /// still long enough, and the archer never committed and never shot.
    /// </summary>
    private static void NoteShot(SosariaCharacter character, Memory memory, Mobile foe)
    {
        var clock = character.NextCombatTime;
        var fired = FootworkRules.Fired(memory.SwingClock, clock, Core.TickCount);
        memory.SwingClock = clock;

        if (!fired || memory.Committed)
        {
            return;
        }

        if (memory.KitingSince != 0 && LogsOnce(character, StepClearLine, foe))
        {
            logger.Information("{Name} stepped clear of {Foe} to shoot", character.Name, foe.Name);
        }

        EndKite(memory);
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

    /// <summary>
    /// Logs once a stand why the fighter <paramref name="act"/> where it stands: it holds its
    /// ground by choice (<see cref="CombatStance.Hold"/>), or it cannot shake the foe, pinned or
    /// outpaced. A hold logged as "cannot shake (holding)" read as a fighter caught.
    /// </summary>
    private static void NoteStand(SosariaCharacter character, Memory memory, Mobile foe, CombatStance stance, string act)
    {
        if (stance == CombatStance.Hold)
        {
            if (LogsOnce(character, HoldLine, foe))
            {
                logger.Information("{Name} holds its ground and {Act} at {Foe}", character.Name, act, foe.Name);
            }

            return;
        }

        if (LogsOnce(character, ShakeLine, foe))
        {
            logger.Information(
                "{Name} cannot shake {Foe} ({Why}) and {Act} where it stands",
                character.Name,
                foe.Name,
                memory.Pinned ? PinnedWhy : OutpacedWhy,
                act
            );
        }
    }

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

    /// <summary>Every spell a fighter may cast, for its stock.</summary>
    private static readonly SpellKind[] AllSpells = Enum.GetValues<SpellKind>();

    private static readonly SpellInfo[] SpellInfos = new SpellInfo[AllSpells.Length];

    /// <summary>The pack's count of each reagent type, read by <see cref="StockOf"/>. World thread only.</summary>
    private static readonly int[] ReagentCounts = new int[SupplyCheck.ReagentTypes.Length];

    /// <summary>The pack's remedies and the spells it holds the reagents for. Read on the scan pace.</summary>
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
            Stock = StockOf(pack, AllSpells, character)
        };
    }

    /// <summary>
    /// The spells of <paramref name="kinds"/> whose reagents <paramref name="pack"/> holds
    /// (<see cref="SpellStock"/>). The pack is read once, one count per reagent type
    /// (<see cref="SupplyCheck.ReagentTypes"/>); a pack search per reagent of every spell was
    /// fifty searches a scan for every fighter. World thread only.
    /// </summary>
    private static SpellStock StockOf(Container pack, IReadOnlyList<SpellKind> kinds, Mobile caster)
    {
        var stock = SpellStock.None;

        if (pack == null)
        {
            return stock;
        }

        for (var i = 0; i < SupplyCheck.ReagentTypes.Length; i++)
        {
            ReagentCounts[i] = pack.GetAmount(SupplyCheck.ReagentTypes[i]);
        }

        for (var i = 0; i < kinds.Count; i++)
        {
            if (SpellCasting.HasReagents(InfoOf(kinds[i], caster), CountedReagent))
            {
                stock = stock.With(kinds[i]);
            }
        }

        return stock;
    }

    /// <summary>The count <see cref="StockOf"/> read for a reagent type; zero for a type it does not count.</summary>
    private static int CountedReagent(Type reagent)
    {
        var index = Array.IndexOf(SupplyCheck.ReagentTypes, reagent);
        return index < 0 ? 0 : ReagentCounts[index];
    }

    private static bool HasReagents(Container pack, SpellKind kind, Mobile caster) =>
        SpellCasting.HasReagents(pack, InfoOf(kind, caster));

    /// <summary>
    /// A spell's reagent list (SpellInfo), the same for every caster: read once per kind, so the
    /// stock read on the scan pace makes no spell each time.
    /// </summary>
    private static SpellInfo InfoOf(SpellKind kind, Mobile caster) =>
        SpellInfos[(int)kind] ??= NewSpell(kind, caster).Info;
}
