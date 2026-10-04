using System;

namespace SosariaAI.Skills;

/// <summary>
/// T2A discordance: CheckSkill 0-120, then lower combat stats of a mobile in bard range
/// (<see cref="MusicRules.BardRange"/>).
/// </summary>
public static class DiscordRules
{
    public const double PracticeMin = 0;
    public const double PracticeMax = 120;
    public const double EffectSkillDivisor = 5;
    public const double EffectScalar = 0.01;
    public const double EffectSeconds = 15;
    public const string EffectName = "Discordance";

    /// <summary>
    /// The engine's discord target: not the bard, not a player, not bard-immune, a mobile
    /// the bard may harm, and not already in discord.
    /// </summary>
    public static bool IsDiscordTarget(bool isSelf, bool isPlayer, bool bardImmune, bool canHarm, bool inDiscord) =>
        !isSelf && !isPlayer && !bardImmune && canHarm && !inDiscord;

    public static int CombatStatOffset(int rawStat, double discordance)
    {
        var effect = (int)(discordance / -EffectSkillDivisor);
        var scalar = effect * EffectScalar;
        return (int)(rawStat * scalar);
    }

    public static TimeSpan EffectDuration() => TimeSpan.FromSeconds(EffectSeconds);
}
