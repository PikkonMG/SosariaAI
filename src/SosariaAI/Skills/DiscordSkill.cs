using System;
using Server;
using Server.Mobiles;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Play in place. CheckSkill(Discordance); on success lower combat stats of a nearby mobile.
/// </summary>
public sealed class DiscordSkill : Skill
{
    private SosariaCharacter _bard;

    public override string Name => SkillKinds.Discord;

    public override bool Begin(SosariaCharacter character)
    {
        _bard = character;
        return People.InWorld(character);
    }

    public override SkillStatus Tick()
    {
        if (!People.InWorld(_bard) || _bard.Deleted)
        {
            return SkillStatus.Failed;
        }

        var instrument = MusicRules.FindInstrument(_bard);

        if (instrument == null)
        {
            return SkillStatus.Failed;
        }

        var target = FindTarget(_bard);

        // The engine plays discord at a target only; with none there is no song to play.
        if (target == null)
        {
            return SkillStatus.Failed;
        }

        var ok = _bard.CheckSkill(
            SkillName.Discordance,
            DiscordRules.PracticeMin,
            DiscordRules.PracticeMax
        );

        if (!ok)
        {
            instrument.PlayInstrumentBadly(_bard);
            instrument.ConsumeUse(_bard);
            return SkillStatus.Failed;
        }

        instrument.PlayInstrumentWell(_bard);
        instrument.ConsumeUse(_bard);
        LowerCombatStats(target);
        return SkillStatus.Done;
    }

    public override void Abort() => _bard = null;

    /// <summary>The first mobile in the bard's range that a song of discord would touch, or null.</summary>
    public static Mobile FindTarget(Mobile bard)
    {
        foreach (var mobile in bard.GetMobilesInRange(MusicRules.BardRange(bard.Skills.Discordance.Value)))
        {
            if (mobile is not { Deleted: false } ||
                !DiscordRules.IsDiscordTarget(
                    mobile == bard,
                    mobile.Player,
                    mobile is BaseCreature { BardImmune: true },
                    bard.CanBeHarmful(mobile, false),
                    mobile.GetStatMod(DiscordRules.EffectName) != null
                ))
            {
                continue;
            }

            return mobile;
        }

        return null;
    }

    private void LowerCombatStats(Mobile target)
    {
        var discordance = _bard.Skills.Discordance.Value;
        var duration = DiscordRules.EffectDuration();

        target.AddStatMod(
            new StatMod(
                StatType.Str,
                DiscordRules.EffectName,
                DiscordRules.CombatStatOffset(target.RawStr, discordance),
                duration));
        target.AddStatMod(
            new StatMod(
                StatType.Int,
                DiscordRules.EffectName,
                DiscordRules.CombatStatOffset(target.RawInt, discordance),
                duration));
        target.AddStatMod(
            new StatMod(
                StatType.Dex,
                DiscordRules.EffectName,
                DiscordRules.CombatStatOffset(target.RawDex, discordance),
                duration));
    }
}
