using System;
using System.Collections.Generic;
using Server;
using Server.Guilds;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Spawning;

namespace SosariaAI.Social;

/// <summary>
/// The plugin's guilds as real engine guilds, so a guild war is a real war: enemies
/// show orange, a blow is not a crime, and the town guards stay out of it. Without
/// this, warring members fought in town as criminals and the guards cut them down.
/// The engine keeps two war books: before Samurai Empire a war is the guild's enemy list,
/// after it a war declaration. A declaration alone meant nothing in the Second Age, and a
/// war blow read as a crime. The plugin's <see cref="GuildWarRegistry"/> is the truth;
/// once a minute the engine book is brought in line with it.
/// </summary>
public static class EngineGuilds
{
    /// <summary>A war ends on time, not on a kill count.</summary>
    public const int WarKillsCap = 1000;

    private static bool _seeded;

    public static void Initialize() => ActivityPulse.MinutePassed += () => SyncWars(SosariaSettings.GuildWars, Core.Now);

    public static Guild For(int guildIndex)
    {
        if (guildIndex < 0 || guildIndex >= GuildCatalog.All.Length)
        {
            return null;
        }

        return BaseGuild.FindByAbbrev(GuildCatalog.All[guildIndex].Tag) as Guild;
    }

    /// <summary>
    /// Sets the plugin guild a person wears on this bind (see <see cref="GuildCatalog.Settle"/>):
    /// Order and Chaos only for a fighter, and never Order for a murderer. A member of a
    /// player's guild, or someone a player recruited, is never moved.
    /// </summary>
    public static void Settle(SosariaCharacter character, bool thief, bool murderer)
    {
        if (character == null || GuildRecruits.InPlayerGuild(character) || GuildRecruits.Recruited(character))
        {
            return;
        }

        character.GuildIndex = GuildCatalog.Settle(
            character.CharacterId,
            thief,
            character.Build?.IsFighter == true,
            character.GuildIndex,
            murderer
        );
    }

    /// <summary>
    /// Settles and wears the guild again when the red name comes or goes: a new red leaves
    /// Order at once, and a red whose murders decayed wears the guild its next bind would give it.
    /// </summary>
    public static void Resettle(SosariaCharacter character, bool murderer)
    {
        Settle(character, character.PersonProfile?.Class == PersonClass.Thief, murderer);
        Join(character);
    }

    public static void Join(SosariaCharacter character)
    {
        // A member of a player's guild stays in it, and a player's recruit waits for the stone;
        // the plugin's guild dice never move either.
        if (character == null || character.Deleted || GuildRecruits.InPlayerGuild(character) ||
            GuildRecruits.Recruited(character))
        {
            return;
        }

        var record = character.GuildIndex >= 0 && character.GuildIndex < GuildCatalog.All.Length
            ? GuildCatalog.All[character.GuildIndex]
            : null;

        if (record == null)
        {
            (character.Guild as Guild)?.RemoveMember(character);
            return;
        }

        var guild = For(character.GuildIndex);

        if (guild == null)
        {
            // The first member founds it. The engine makes the founder the leader.
            guild = new Guild(character, record.Name, record.Tag);
        }
        else if (character.Guild != guild)
        {
            guild.AddMember(character);
        }

        // The engine shows Order and Chaos only in the eras that had them. The side is
        // stored either way; the plugin's own fight rules read the catalog.
        guild.Type = record.Alignment;
    }

    /// <summary>
    /// The Order or Chaos side of anyone in a guild: the catalog's side for a plugin guild,
    /// found by its tag, and the engine's type for a guild a player founded.
    /// </summary>
    public static GuildType AlignmentOf(Mobile mobile) => SideOf(mobile?.Guild);

    /// <summary>The Order or Chaos side of a guild, read the way <see cref="AlignmentOf"/> reads a member's.</summary>
    public static GuildType SideOf(BaseGuild baseGuild)
    {
        if (baseGuild is not Guild guild)
        {
            return GuildType.Regular;
        }

        var index = GuildCatalog.IndexOfTag(guild.Abbreviation);
        return index != GuildCatalog.None ? GuildCatalog.AlignmentOf(index) : guild.Type;
    }

    /// <summary>Adds the online members of every guild of <paramref name="side"/> to <paramref name="into"/>.</summary>
    public static void AddSideMembers(GuildType side, List<SosariaCharacter> into)
    {
        foreach (var guild in World.Guilds.Values)
        {
            if (guild is Guild { Disbanded: false } found && SideOf(found) == side)
            {
                into.AddRange(GuildChat.OnlineCharacters(found));
            }
        }
    }

    /// <summary>An Order member and a Chaos member: a fight between them is lawful anywhere.</summary>
    public static bool Opposed(Mobile first, Mobile second) =>
        first != null && second != null && first != second &&
        GuildCatalog.Opposed(AlignmentOf(first), AlignmentOf(second));

    /// <summary>Two people in guilds at war, by the engine's book or by the plugin's.</summary>
    public static bool AtWar(Mobile first, Mobile second)
    {
        if (first?.Guild is not Guild firstGuild || second?.Guild is not Guild secondGuild || firstGuild == secondGuild)
        {
            return false;
        }

        return firstGuild.IsWar(secondGuild) ||
               SosariaSettings.GuildWars?.AtWar(
                   GuildCatalog.IndexOfTag(firstGuild.Abbreviation),
                   GuildCatalog.IndexOfTag(secondGuild.Abbreviation),
                   Core.Now
               ) == true;
    }

    public static void DeclareWar(int firstIndex, int secondIndex, TimeSpan length)
    {
        var first = For(firstIndex);
        var second = For(secondIndex);

        if (first == null || second == null || first == second || first.IsWar(second))
        {
            return;
        }

        if (Guild.NewGuildSystem)
        {
            first.AcceptedWars.Add(Declaration(first, second, length, requester: true));
            second.AcceptedWars.Add(Declaration(second, first, length, requester: false));
        }
        else
        {
            first.AddEnemy(second);
        }

        first.InvalidateMemberProperties();
        second.InvalidateMemberProperties();
    }

    /// <summary>Takes a war out of the engine's book, whichever book the era keeps.</summary>
    public static void EndWar(Guild first, Guild second)
    {
        if (first == null || second == null)
        {
            return;
        }

        first.RemoveEnemy(second);
        first.AcceptedWars.Remove(first.FindActiveWar(second));
        second.AcceptedWars.Remove(second.FindActiveWar(first));
        first.InvalidateMemberProperties();
        second.InvalidateMemberProperties();
    }

    /// <summary>
    /// Brings the engine's wars in line with the registry. On the first pass after a boot the
    /// registry is empty, so it first takes the wars the save kept; after that a war the feud
    /// let lapse ends in the engine too, and a war the registry holds is declared.
    /// </summary>
    public static void SyncWars(GuildWarRegistry registry, DateTime now)
    {
        if (registry == null)
        {
            return;
        }

        var seeding = !_seeded;
        _seeded = true;

        for (var i = 0; i < GuildCatalog.All.Length; i++)
        {
            for (var j = i + 1; j < GuildCatalog.All.Length; j++)
            {
                var first = For(i);
                var second = For(j);

                if (first == null || second == null)
                {
                    continue;
                }

                var engineWar = first.IsWar(second);

                switch (GuildWarRegistry.SyncStep(engineWar, registry.AtWar(i, j, now), seeding))
                {
                    case WarSync.Keep:
                        {
                            registry.RecordAggression(i, j, now);
                            break;
                        }
                    case WarSync.Declare:
                        {
                            DeclareWar(i, j, GuildWarRegistry.WarDuration);
                            break;
                        }
                    case WarSync.End:
                        {
                            EndWar(first, second);
                            break;
                        }
                }
            }
        }
    }

    private static WarDeclaration Declaration(Guild guild, Guild opponent, TimeSpan length, bool requester) =>
        new(guild, opponent, WarKillsCap, length, requester) { WarBeginning = Core.Now };
}
