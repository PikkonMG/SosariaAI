using System;
using Server;
using Server.Guilds;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Skills;

namespace SosariaAI.Social;

/// <summary>
/// A player recruits a character at the guildstone. The engine then lists it as accepted
/// and waits for it to use the stone, a step no character took on its own. A sweep finds
/// accepted characters and sends them to the stone. A member of a player's guild stays on
/// the shard: it keeps living its life, but the session clock never logs it out.
/// </summary>
public static class GuildRecruits
{
    public static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(15);

    private static readonly ILogger logger = SosariaLog.For(typeof(GuildRecruits));

    public static void Install() => Timer.StartTimer(SweepInterval, SweepInterval, Sweep);

    /// <summary>True for a guild a player founded: none of the plugin's own guild tags.</summary>
    public static bool IsPlayerGuild(BaseGuild guild) =>
        guild is Guild found && GuildRecruitRules.IsPlayerGuildTag(found.Abbreviation, GuildCatalog.All);

    public static bool InPlayerGuild(Mobile mobile) => IsPlayerGuild(mobile?.Guild);

    /// <summary>True while a player's guild has accepted this person and it has not yet used the stone.</summary>
    public static bool Recruited(Mobile mobile)
    {
        if (mobile == null)
        {
            return false;
        }

        foreach (var baseGuild in World.Guilds.Values)
        {
            if (baseGuild is Guild { Disbanded: false } guild && guild.Accepted.Contains(mobile))
            {
                return true;
            }
        }

        return false;
    }

    public static void Joined(SosariaCharacter character, Guild guild)
    {
        character.GuildIndex = GuildCatalog.None;
        character.SpeakScripted(GuildRecruitRules.JoinLine(guild.Abbreviation));

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} joined the player guild {Guild}", character.Name, guild.Name);
        }
    }

    private static void Sweep()
    {
        foreach (var baseGuild in World.Guilds.Values)
        {
            if (baseGuild is not Guild { Disbanded: false } guild || guild.Accepted.Count == 0)
            {
                continue;
            }

            for (var i = 0; i < guild.Accepted.Count; i++)
            {
                if (guild.Accepted[i] is SosariaCharacter { Deleted: false, Alive: true, Guild: null } recruit &&
                    People.InWorld(recruit) &&
                    recruit.Routine?.CurrentSkill is not GuildstoneJoinSkill)
                {
                    recruit.AttachRoutine(new Routine([new GuildstoneJoinSkill(guild)]));
                }
            }
        }
    }
}
