using System;
using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;

namespace SosariaAI.Combat;

/// <summary>A bard's songs in a fight. They play the way the provoke and peace jobs play (<see cref="InstrumentPlay"/>).</summary>
public static partial class CombatBrain
{
    /// <summary>
    /// On the song rest, a bard sets the monster on it against another monster, one of the same
    /// fight first (<see cref="PartnerOf"/>), or, when the fight goes badly, calms it and slips
    /// away while it stands calm (<see cref="BardSongRules"/>). True when the calm ended this
    /// fight in a run.
    /// </summary>
    private static bool TrySing(SosariaCharacter character, Memory memory, Mobile foe)
    {
        var now = Core.TickCount;
        var provocation = character.Skills.Provocation.Value;
        var peacemaking = character.Skills.Peacemaking.Value;

        if (now - memory.NextSongAt < 0 || character.Spell != null || !BardSongRules.CanSing(provocation, peacemaking))
        {
            return false;
        }

        var instrument = MusicRules.FindInstrument(character);
        var creatureOnSelf = foe is BaseCreature && foe.Combatant == character;
        var provocable = creatureOnSelf && ProvokeSkill.IsTarget(character, foe);
        var calmable = creatureOnSelf && PeaceSkill.IsCalmable(character, foe, MusicRules.BardRange(peacemaking));
        var partner = provocable && provocation >= BardSongRules.MinSongSkill ? PartnerOf(character, memory, foe, provocation) : null;
        var attackers = memory.Picture.Attackers;
        var goingBadly = BardSongRules.GoingBadly(
            memory.Outlook,
            Vitals.HitsFraction(character),
            RetreatRules.StartLine(BaseLineOf(character), NerveOf(character, memory), Math.Max(RetreatRules.SingleAttacker, attackers)),
            attackers
        );
        var song = BardSongRules.Pick(
            provocation,
            peacemaking,
            instrument != null,
            creatureOnSelf,
            provocable,
            partner != null,
            calmable,
            goingBadly
        );

        if (song == CombatSong.None)
        {
            memory.NextSongAt = now + BardSongRules.NoSongRetryMs;
            return false;
        }

        RestSong(memory, now);

        if (song == CombatSong.Provoke)
        {
            if (InstrumentPlay.Try(character, instrument, SkillName.Provocation, ProvokeRules.PracticeMin, ProvokeRules.PracticeMax))
            {
                ((BaseCreature)foe).Provoke(character, partner, true);

                if (SosariaSettings.LogActivity)
                {
                    logger.Information("{Name} provokes {Foe} onto {Partner}", character.Name, foe.Name, partner.Name);
                }
            }

            return false;
        }

        if (!Calm(character, instrument, foe, peacemaking))
        {
            return false;
        }

        BreakOff(character, memory, foe, ordered: true, FleeSpan());

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} calms {Foe} and slips away", character.Name, foe.Name);
        }

        return true;
    }

    /// <summary>
    /// The creature to set <paramref name="foe"/> on: the strongest other attacker on the bard
    /// that the engine lets it provoke (<see cref="BardSongRules.FightPartner"/>), so both leave
    /// it; else any creature near the foe the engine lets it provoke (<see cref="ProvokeSkill.PartnerFor"/>).
    /// </summary>
    private static Mobile PartnerOf(SosariaCharacter character, Memory memory, Mobile foe, double provocation)
    {
        var songReach = MusicRules.BardRange(provocation);
        Mobile best = null;
        var bestThreat = int.MinValue;

        for (var i = 0; i < memory.Foes.Count; i++)
        {
            var sighting = memory.Foes[i];
            var mobile = sighting.Mobile;

            if (mobile == foe || mobile?.Deleted != false || !mobile.Alive || mobile.Map != character.Map ||
                sighting.SoloThreat <= bestThreat ||
                !BardSongRules.FightPartner(
                    mobile.Combatant == character,
                    sighting.Harmless,
                    NavMetric.Chebyshev(foe.Location, mobile.Location),
                    NavMetric.Chebyshev(character.Location, mobile.Location),
                    songReach) ||
                !ProvokeSkill.IsTarget(character, mobile))
            {
                continue;
            }

            best = mobile;
            bestThreat = sighting.SoloThreat;
        }

        return best ?? ProvokeSkill.PartnerFor(character, foe);
    }

    /// <summary>
    /// A bard on the run calms the creature nearest on it when the song rest allows
    /// (<see cref="BardSongRules.CalmsChaser"/>), and runs on while it stands calm.
    /// </summary>
    private static void CalmChaser(SosariaCharacter character, Memory memory)
    {
        var now = Core.TickCount;
        var peacemaking = character.Skills.Peacemaking.Value;

        if (now - memory.NextSongAt < 0 || character.Spell != null || !BardSongRules.CanCalm(peacemaking) ||
            NearestAttacker(character, memory) is not BaseCreature chaser)
        {
            return;
        }

        var instrument = MusicRules.FindInstrument(character);

        if (!BardSongRules.CalmsChaser(instrument != null, PeaceSkill.IsCalmable(character, chaser, MusicRules.BardRange(peacemaking))))
        {
            memory.NextSongAt = now + BardSongRules.NoSongRetryMs;
            return;
        }

        RestSong(memory, now);

        if (Calm(character, instrument, chaser, peacemaking) && SosariaSettings.LogActivity)
        {
            logger.Information("{Name} calms {Foe} on the run", character.Name, chaser.Name);
        }
    }

    /// <summary>One Peacemaking song on <paramref name="creature"/>; true when it played well and the creature stands calm.</summary>
    private static bool Calm(SosariaCharacter character, BaseInstrument instrument, Mobile creature, double peacemaking)
    {
        if (!InstrumentPlay.Try(character, instrument, SkillName.Peacemaking, PeaceRules.PracticeMin, PeaceRules.PracticeMax))
        {
            return false;
        }

        PeaceSkill.Calm(character, creature, TimeSpan.FromSeconds(BardSongRules.CalmSeconds(peacemaking)));
        return true;
    }

    /// <summary>A song played: the next waits out the rest, the way a player waits on the skill timer.</summary>
    private static void RestSong(Memory memory, long now) =>
        memory.NextSongAt = now + Utility.RandomMinMax(BardSongRules.MinRestMs, BardSongRules.MaxRestMs);
}
