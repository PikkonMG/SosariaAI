using System;
using System.Collections.Generic;
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
/// Guild chat for characters. They speak into it through the engine's own guild message, so
/// a player of the guild reads it in the guild channel. They hear a player's guild line through
/// <see cref="GuildChatHook"/>: "what is everyone doing" gets real answers, a group call gets
/// guildmates travelling over (by recall when they have a rune there), and a hello gets a hello.
/// While a player of the guild is online the characters chat, welcome the player back, pass on
/// news, and ask for groups themselves.
/// </summary>
public static class GuildChat
{
    private static readonly ILogger logger = SosariaLog.For(typeof(GuildChat));
    private static readonly Dictionary<Guild, DateTime> NextChatter = new();
    private static readonly Dictionary<Guild, HashSet<Serial>> SeenOnline = new();
    private static readonly Dictionary<Guild, (Serial Asker, DateTime At)> OpenCalls = new();

    public static void Initialize()
    {
        EventSink.Speech += DeliverOldSystemLine;
        GuildChatHook.Install();
        Timer.StartTimer(GuildChatRules.Tick, GuildChatRules.Tick, Tick);
    }

    /// <summary>A character's line in its guild's channel, through the engine's guild message.</summary>
    public static void Say(SosariaCharacter character, string text)
    {
        if (character is not { Deleted: false } || character.Guild is not Guild guild || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        guild.GuildChat(character, text);

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} guild chat: {Line}", character.Name, text);
        }
    }

    /// <summary>A player's line in guild chat. Guildmate characters answer from what they really do.</summary>
    public static void HeardFromPlayer(Mobile player, string text)
    {
        if (!People.IsHuman(player) || player.Guild is not Guild guild || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var members = OnlineCharacters(guild);

        if (members.Count == 0)
        {
            return;
        }

        Shuffle(members);

        if (SpeechIntent.AsksWhatDoing(text))
        {
            AnswerDoing(members);
            return;
        }

        if (SpeechIntent.IsPartyCall(text))
        {
            Rally(guild, members, player);
            return;
        }

        // A player's "me" answers a character's open call; any other line is left alone.
        if (!(SpeechIntent.IsJoin(text) && AnswerOpenCall(guild, player)) &&
            SpeechIntent.Classify(text, null) == SpeechIntentKind.Greeting)
        {
            AnswerGreeting(members, player);
        }
    }

    /// <summary>Guildmates of this guild in the world and alive.</summary>
    public static List<SosariaCharacter> OnlineCharacters(Guild guild)
    {
        var found = new List<SosariaCharacter>();

        for (var i = 0; i < guild.Members.Count; i++)
        {
            if (guild.Members[i] is SosariaCharacter { Deleted: false, Alive: true } character &&
                character.Map != null && character.Map != Map.Internal)
            {
                found.Add(character);
            }
        }

        return found;
    }

    private static void AnswerDoing(List<SosariaCharacter> members)
    {
        for (var i = 0; i < members.Count && i < GuildChatRules.DoingAnswers; i++)
        {
            var member = members[i];
            var line = SpeechLines.DoingAnswer(
                member.Routine?.CurrentSkill?.Name,
                GossipLines.PlaceWord(PlaceNames.Of(member)),
                Utility.Random(int.MaxValue)
            );
            SayLater(member, line, GuildChatRules.DelayFor(i));
        }
    }

    /// <summary>A player asked for a group: guildmates come to the player and accept the player's invite.</summary>
    private static void Rally(Guild guild, List<SosariaCharacter> members, Mobile player)
    {
        var coming = SendHelp(members, player);

        for (var i = 0; i < coming.Count; i++)
        {
            LfgBoard.Volunteer(coming[i], player);
        }

        if (coming.Count == 0 && Utility.Random(100) < GuildChatRules.CantComePercent)
        {
            SayLater(members[0], Talk.Line(TalkCategory.GuildCantCome), GuildChatRules.AnswerDelay);
        }

        if (coming.Count > 0 && SosariaSettings.LogActivity)
        {
            logger.Information("{Count} of {Guild} set out to group with {Asker}", coming.Count, guild.Abbreviation, player.Name);
        }
    }

    /// <summary>
    /// Fitting guildmates on the asker's facet say they are coming and travel to the asker: by
    /// recall when they have a rune marked there, else on foot or through gates.
    /// </summary>
    private static List<SosariaCharacter> SendHelp(List<SosariaCharacter> members, Mobile asker)
    {
        var coming = HelpersFor(members, asker);
        SetOff(coming, asker);
        return coming;
    }

    /// <summary>The fitting guildmates on the asker's facet who would come, before any of them is told.</summary>
    private static List<SosariaCharacter> HelpersFor(List<SosariaCharacter> members, Mobile asker)
    {
        var coming = new List<SosariaCharacter>();

        for (var i = 0; i < members.Count && coming.Count < GuildChatRules.RallyAnswers; i++)
        {
            var member = members[i];

            if (member != asker && member.Map == asker.Map && LfgBoard.Fits(member))
            {
                coming.Add(member);
            }
        }

        return coming;
    }

    /// <summary>Each helper says it is coming and travels to the asker.</summary>
    private static void SetOff(List<SosariaCharacter> coming, Mobile asker)
    {
        for (var i = 0; i < coming.Count; i++)
        {
            SayLater(coming[i], Talk.Line(TalkCategory.GuildOnMyWay), GuildChatRules.DelayFor(i));
            WorldPlay.StartWork(coming[i], new TravelSkill(asker.Location, GuildChatRules.ArriveRange));
        }
    }

    /// <summary>
    /// A player's "me" in guild chat: a guildmate's open group call first, else a road group's
    /// call that went out in this guild's chat (<see cref="PartyRoads.JoinPlayer"/>).
    /// </summary>
    private static bool AnswerOpenCall(Guild guild, Mobile player)
    {
        if (OpenCalls.TryGetValue(guild, out var call) && GuildChatRules.CallStillOpen(call.At, Core.Now) &&
            World.FindMobile(call.Asker) is SosariaCharacter asker && LfgBoard.InvitePlayerToCall(asker, player))
        {
            SayInvite(asker, player);
            return true;
        }

        if (PartyRoads.JoinPlayer(player, guild, out var invited) is not { } leader)
        {
            return false;
        }

        if (invited)
        {
            SayInvite(leader, player);
        }

        return true;
    }

    private static void SayInvite(SosariaCharacter leader, Mobile player) =>
        SayLater(leader, Talk.Line(TalkCategory.GuildInvitePlayer, new TalkSlots { Name = player.Name }), GuildChatRules.AnswerDelay);

    private static void AnswerGreeting(List<SosariaCharacter> members, Mobile player)
    {
        var order = 0;

        for (var i = 0; i < members.Count && order < GuildChatRules.GreetAnswers; i++)
        {
            if (Utility.Random(100) >= GuildChatRules.GreetAnswerPercent)
            {
                continue;
            }

            SayLater(members[i], Talk.Line(TalkCategory.GuildWelcome, new TalkSlots { Name = player.Name }), GuildChatRules.DelayFor(order));
            order++;
        }
    }

    private static void Tick()
    {
        var now = Core.Now;

        for (var i = 0; i < GuildCatalog.All.Length; i++)
        {
            if (EngineGuilds.For(i) is not { Disbanded: false } guild)
            {
                continue;
            }

            var players = OnlinePlayers(guild);

            if (players.Count == 0)
            {
                SeenOnline.Remove(guild);
                continue;
            }

            var members = OnlineCharacters(guild);

            if (members.Count == 0)
            {
                continue;
            }

            Shuffle(members);
            Welcome(guild, members, players);

            if (!GuildChatRules.ChatterDue(NextChatter.GetValueOrDefault(guild), now))
            {
                continue;
            }

            NextChatter[guild] = GuildChatRules.NextChatter(now, Utility.Random(101));
            Chatter(guild, members, now);
        }
    }

    private static void Welcome(Guild guild, List<SosariaCharacter> members, List<Mobile> players)
    {
        if (!SeenOnline.TryGetValue(guild, out var seen))
        {
            SeenOnline[guild] = seen = [];
        }

        foreach (var player in players)
        {
            if (seen.Add(player.Serial) && Utility.Random(100) < GuildChatRules.WelcomePercent)
            {
                SayLater(members[0], Talk.Line(TalkCategory.GuildWelcome, new TalkSlots { Name = player.Name }), GuildChatRules.AnswerDelay);
            }
        }

        seen.RemoveWhere(serial => World.FindMobile(serial) is not { NetState: not null });
    }

    /// <summary>One chatter slot: a group call, a piece of true news, or small talk.</summary>
    private static void Chatter(Guild guild, List<SosariaCharacter> members, DateTime now)
    {
        var speaker = members[0];
        var roll = Utility.Random(100);
        var target = LfgBoard.TargetFor(speaker);

        if (roll < GuildChatRules.AskGroupPercent && target != null && LfgBoard.Fits(speaker) &&
            speaker.LastScore?.Goal.Kind is GoalKind.Hunt or GoalKind.Dungeon &&
            AskForGroup(guild, members, speaker, target, now))
        {
            return;
        }

        if (roll < GuildChatRules.AskGroupPercent + GuildChatRules.GossipPercent && speaker.Combatant == null)
        {
            var news = SosariaSettings.Journal?.PickGossip(speaker.Name, speaker.Location, now, speaker.HomeFacet, Utility.Random(100));
            var line = Meeting.GossipLine(news, speaker.Name, speaker.Location, now, Utility.Random(int.MaxValue));

            if (!string.IsNullOrEmpty(line))
            {
                Say(speaker, line);
                return;
            }
        }

        // Small talk keeps no trip or group: a random guildmate said "heading to town" whatever it did.
        var chatter = Talk.Line(TalkCategory.GuildChatter);

        if (!PromiseLines.IsPromise(chatter))
        {
            Say(speaker, chatter);
        }
    }

    /// <summary>
    /// A character asks its guild for a group; those coming join its call when they arrive. The
    /// call is asked only once the group stands: "going to shame, join me" went out when no
    /// guildmate fit, and a player's "me" then found no group. False when no group opened.
    /// </summary>
    private static bool AskForGroup(Guild guild, List<SosariaCharacter> members, SosariaCharacter asker, LfgTarget target, DateTime now)
    {
        var coming = HelpersFor(members, asker);

        if (!LfgBoard.OpenGuildCall(asker, target, coming))
        {
            return false;
        }

        OpenCalls[guild] = (asker.Serial, now);
        Say(asker, Talk.Line(TalkCategory.GuildAskGroup, new TalkSlots { Place = target.Place }));
        SetOff(coming, asker);
        return true;
    }

    private static List<Mobile> OnlinePlayers(Guild guild)
    {
        var found = new List<Mobile>();

        for (var i = 0; i < guild.Members.Count; i++)
        {
            if (People.IsHuman(guild.Members[i]) && guild.Members[i].NetState != null)
            {
                found.Add(guild.Members[i]);
            }
        }

        return found;
    }

    private static void SayLater(SosariaCharacter character, string line, TimeSpan delay)
    {
        var serial = character.Serial;

        Timer.StartTimer(delay, () =>
        {
            if (World.FindMobile(serial) is SosariaCharacter { Deleted: false, Alive: true } living)
            {
                Say(living, line);
            }
        });
    }

    private static void Shuffle(List<SosariaCharacter> members)
    {
        for (var i = members.Count - 1; i > 0; i--)
        {
            var j = Utility.Random(i + 1);
            (members[i], members[j]) = (members[j], members[i]);
        }
    }

    /// <summary>
    /// Before the new guild system (Samurai Empire), the engine treats a guild-typed line as a
    /// local shout nobody in the guild hears. Deliver it to the guild and keep the echo off the street.
    /// </summary>
    private static void DeliverOldSystemLine(SpeechEventArgs e)
    {
        if (Guild.NewGuildSystem || e.Type != MessageType.Guild || !People.IsHuman(e.Mobile) ||
            e.Mobile.Guild is not Guild guild)
        {
            return;
        }

        guild.GuildChat(e.Mobile, e.Hue, e.Speech);
        e.Blocked = true;
    }
}
