using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Mobiles;

namespace SosariaAI.Combat;

/// <summary>
/// The stance for this tick: the one Jev set while it stands, else the stance rules. Jev is
/// asked about a fight that matters when the words that describe it change, never more often
/// than its ask gap nor more than its asks per fight, and only when the decision route is a
/// System One provider.
/// </summary>
public static partial class CombatBrain
{
    private static CombatStance StanceFor(
        SosariaCharacter character,
        Memory memory,
        Mobile foe,
        CombatStyle style,
        bool caster,
        int distance
    )
    {
        var now = Core.TickCount;
        var situation = SituationOf(character, memory, foe, style, caster, distance, now);
        var stance = now - memory.StanceUntil < 0 && memory.JevStance is { } set && StanceRules.Feasible(set, situation)
            ? set
            : StanceRules.Pick(situation);

        TryAskStance(character, memory, foe, situation, now);
        return stance;
    }

    private static StanceSituation SituationOf(
        SosariaCharacter character,
        Memory memory,
        Mobile foe,
        CombatStyle style,
        bool caster,
        int distance,
        long now
    )
    {
        var magery = character.Skills.Magery.Value;
        var mana = character.Mana;
        var hits = Vitals.HitsFraction(character);
        var kit = memory.Kit;

        return new StanceSituation(
            style,
            caster,
            TankMage: caster && style == CombatStyle.Melee,
            hits,
            ManaFraction(character),
            character.Poisoned,
            NeedsCare: SelfCareRules.NeedsCare(character.Poisoned, hits, inFight: true),
            FoeIsPerson: People.IsLivingPlayer(foe),
            FoeStyle: StyleOfFoe(foe),
            FoeHitsFraction: Vitals.HitsFraction(foe),
            FoeDistance: distance,
            FoeCasting: IsCastingNow(foe),
            FoeHeld: foe.Paralyzed || foe.Frozen,
            AdjacentFoes: memory.Threats.Adjacent,
            RecentInterrupts: memory.Interrupts.Count(now),
            Warded: WardUp(character),
            CanWard: caster && kit.WardReagents &&
                     SpellBook.CanCast(SpellBook.EntryOf(SpellBook.WardOf(Core.UOR)), magery, mana),
            CanParalyze: caster && kit.ParalyzeReagents && SpellBook.CanCast(SpellBook.Paralyze, magery, mana),
            HasHealPotion: kit.HealPotion,
            HasBandage: kit.Bandage,
            CanHealSpell: caster && kit.HealReagents && SpellBook.CanCast(SpellBook.Heal, magery, mana),
            AlliesNear: memory.AllyTargets.Count,
            Sticky: memory.AdjacentSince != 0 && now - memory.AdjacentSince >= FootworkRules.KiteGraceMs,
            Outlook: memory.Outlook,
            LightDamage: FightTrendRules.LightDamage(memory.Trend, character.HitsMax),
            Cornered: memory.Pinned,
            BlueOnRed: !character.IsPk && People.IsLivingPlayer(foe) && PkRules.IsRed(foe.Kills)
        );
    }

    /// <summary>How a foe fights, as a player would read it at a glance: a bow, a mage's empty hands, or blades and claws.</summary>
    private static CombatStyle StyleOfFoe(Mobile foe)
    {
        if (foe.Weapon is BaseRanged)
        {
            return CombatStyle.Archer;
        }

        if (foe is BaseCreature creature)
        {
            return creature.AI == AIType.AI_Mage ? CombatStyle.Mage : CombatStyle.Melee;
        }

        return SpellBook.IsCaster(foe.Skills.Magery.Value) && HeldWeapon(foe) == null
            ? CombatStyle.Mage
            : CombatStyle.Melee;
    }

    /// <summary>
    /// Asks Jev for a stance when the fight is worth it and its words changed since the last
    /// answer, at most once per ask gap. The same words keep the last answer standing without a call.
    /// </summary>
    private static void TryAskStance(SosariaCharacter character, Memory memory, Mobile foe, StanceSituation situation, long now)
    {
        if (!CombatStanceJev.IsAvailable || memory.StanceAskInFlight || now - memory.NextStanceAskAt < 0 ||
            memory.StanceAsks >= CombatStanceJev.MaxAsksPerFight)
        {
            return;
        }

        memory.NextStanceAskAt = now + CombatStanceJev.AskGapMs;

        if (!WorthAsking(character, memory, foe, situation))
        {
            return;
        }

        var key = CombatStanceJev.Key(situation);

        if (key == memory.StanceAskKey)
        {
            if (memory.JevStance != null)
            {
                memory.StanceUntil = now + CombatStanceJev.StanceHoldMs;
            }

            return;
        }

        var fight = memory.FightId;

        if (!CombatStanceJev.TryAsk(character, foe, situation, verdict => ApplyVerdict(character, fight, foe, verdict)))
        {
            return;
        }

        memory.StanceAskKey = key;
        memory.StanceAskInFlight = true;
        memory.StanceAsks++;
    }

    /// <summary>A fight the rules already settle (a blue winning on a red presses) is not put to Jev.</summary>
    private static bool WorthAsking(SosariaCharacter character, Memory memory, Mobile foe, StanceSituation situation)
    {
        if (StanceRules.PressesRed(situation))
        {
            return false;
        }

        var index = IndexOf(memory, foe);
        var threat = index >= 0 ? memory.Foes[index].SoloThreat : 0;
        var dare = NerveRules.DarePower(CharacterPower.For(character), NerveOf(character, memory), AlliesOn(memory, foe));

        return StanceRules.WorthAsking(situation.FoeIsPerson, threat, dare, situation.HitsFraction);
    }

    /// <summary>Jev's answer, back on the world thread. An answer for a fight already over is dropped.</summary>
    private static void ApplyVerdict(SosariaCharacter character, int fight, Mobile foe, StanceVerdict verdict)
    {
        if (!Memories.TryGetValue(character, out var memory) || memory.FightId != fight)
        {
            return;
        }

        memory.StanceAskInFlight = false;
        memory.JevStance = verdict.Stance;
        memory.StanceSource = verdict.Source;
        memory.StanceUntil = verdict.Stance == null ? 0 : Core.TickCount + CombatStanceJev.StanceHoldMs;

        if (!SosariaSettings.LogActivity)
        {
            return;
        }

        if (verdict.Stance is { } stance)
        {
            logger.Information(
                "{Name} {Source} stance {Stance} for {Foe} ({Confidence:0.00})",
                character.Name,
                verdict.Source,
                stance,
                foe.Name,
                verdict.Confidence
            );
        }
        else
        {
            logger.Information(
                "{Name} {Source} stance unsure for {Foe}; the rules keep the fight",
                character.Name,
                verdict.Source,
                foe.Name
            );
        }
    }
}
