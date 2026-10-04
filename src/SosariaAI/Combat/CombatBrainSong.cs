using System;
using Server;
using Server.Mobiles;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Skills;

namespace SosariaAI.Combat;

/// <summary>A bard's songs in a fight. They play the way the provoke and peace jobs play (<see cref="InstrumentPlay"/>).</summary>
public static partial class CombatBrain
{
    /// <summary>
    /// On the song rest, a bard sets the monster on it against another near it, or, when the
    /// fight goes badly, calms it for a few seconds and lets it be (<see cref="BardSongRules"/>).
    /// True when the calm dropped this fight.
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
        var partner = provocable && provocation >= BardSongRules.MinSongSkill ? ProvokeSkill.PartnerFor(character, foe) : null;
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

        memory.NextSongAt = now + Utility.RandomMinMax(BardSongRules.MinRestMs, BardSongRules.MaxRestMs);

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

        if (!InstrumentPlay.Try(character, instrument, SkillName.Peacemaking, PeaceRules.PracticeMin, PeaceRules.PracticeMax))
        {
            return false;
        }

        PeaceSkill.Calm(character, foe, TimeSpan.FromSeconds(BardSongRules.CalmSeconds(peacemaking)));
        character.Combatant = null;
        character.FocusMob = null;

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} calms {Foe} and lets it be", character.Name, foe.Name);
        }

        return true;
    }
}
