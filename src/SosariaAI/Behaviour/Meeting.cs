using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Spawning;
using SosariaAI.Social;
using SosariaAI.Logging;

namespace SosariaAI.Behaviour;

/// <summary>
/// When two characters pass within greeting range during Wander, one of them greets
/// the other by name. Tone follows opinion. The greet does not hold either walker
/// and does not spend a model call.
/// </summary>
public static class Meeting
{
    private static readonly ILogger logger = SosariaLog.For(typeof(Meeting));
    private static readonly Point3D[] TownPads = TownHomes();

    private static Point3D[] TownHomes()
    {
        var sites = WorkSites.TownSites;
        var pads = new Point3D[sites.Length];

        for (var i = 0; i < sites.Length; i++)
        {
            pads[i] = sites[i].Home;
        }

        return pads;
    }

    public static void Consider(SosariaCharacter character)
    {
        if (character?.Map == null || character.Map == Map.Internal || !character.Alive || character.Hidden)
        {
            return;
        }

        if (character.Motor.Action != CharacterAction.Wander)
        {
            return;
        }

        SpeechResponder.GreetArrivals(character);
        Scenes.ConsiderMeeting(character);

        if (SceneRunner.IsBusy(character))
        {
            return;
        }

        var nearby = new List<SosariaCharacter>();

        foreach (var mobile in character.Map.GetMobilesInRange(character.Location, MeetingRules.GreetingRange))
        {
            if (mobile is SosariaCharacter { Hidden: false } other && other != character && other.Alive)
            {
                nearby.Add(other);
            }
        }

        var now = Core.Now;
        var atQuiet = AtMeetingPoint(character.Location);

        if (!Brain.Conversations.SpeakerMayGreet(character.Serial, now, MeetingRules.SpeakerQuiet))
        {
            return;
        }

        if (atQuiet && !Brain.Conversations.AreaMayGreet(MeetingRules.BankArea, now, MeetingRules.BankGap))
        {
            return;
        }

        var roll = MeetingRules.ChanceRoll(unchecked((int)character.Serial.Value) ^ now.Second);

        if (!MeetingRules.MayGreetCrowd(nearby.Count, atQuiet, roll))
        {
            return;
        }

        for (var i = 0; i < nearby.Count; i++)
        {
            var other = nearby[i];

            if (other.Motor.Action != CharacterAction.Wander)
            {
                continue;
            }

            if (TryGreet(character, other))
            {
                if (atQuiet)
                {
                    Brain.Conversations.RecordAreaGreet(MeetingRules.BankArea, now);
                }

                return;
            }
        }
    }

    /// <summary>At the bank or a town's home pad, where people wait for each other.</summary>
    public static bool AtMeetingPoint(Point3D at) =>
        MeetingRules.InQuietPlace(at, CharactersFile.DefaultBankSpot, TownPads);

    public static bool ShouldSpeak(string line, IReadOnlyList<string> recentSpeech) =>
        !string.IsNullOrWhiteSpace(line) && !SpokenRepeat.IsNearRepeat(line, recentSpeech);

    public static bool TryGreet(
        SosariaCharacter speaker,
        SosariaCharacter other,
        DateTime now,
        BotConversationTracker tracker,
        IReadOnlyList<string> recentSpeech,
        ShardEvent news
    )
    {
        if (speaker == null || other == null || tracker == null)
        {
            return false;
        }

        if (MeetingRules.SameWornName(speaker.Name, other.Name))
        {
            return false;
        }

        if (!tracker.CanGreet(
                speaker.Serial,
                other.Serial,
                now,
                Brain.BotToBotPairRest,
                Brain.BotToBotPairRest,
                1))
        {
            return false;
        }

        var seed = unchecked((int)speaker.Serial.Value);
        var line = TellTo(other, news, speaker, now, seed);
        Recollection? recalled = null;

        if (string.IsNullOrEmpty(line))
        {
            news = null;
            recalled = RecallShared(speaker, other, TalkCategory.GreetOldFriend, MeetingRules.OldFriendRecallPercent, unchecked(seed + now.Second));
            line = recalled?.Line;
        }

        if (string.IsNullOrEmpty(line))
        {
            var warmLine = OwnLine(() => speaker.Persona?.PickGreeting(other.Name));
            var score = Recall.ScoreOf(MemoryStore.Shared, speaker, other);

            // A guildmate is greeted like a friend.
            if (SpeechResponder.SameGuild(speaker, other))
            {
                score = Math.Max(score, BondRules.WarmThreshold);
            }

            line = GreetingLines.Pick(score, other.Name, warmLine, seed);
        }

        if (string.IsNullOrEmpty(line))
        {
            return false;
        }

        tracker.RecordGreeting(speaker.Serial, other.Serial, now);

        if (!ShouldSpeak(line, recentSpeech))
        {
            return false;
        }

        speaker.Direction = speaker.GetDirectionTo(other);
        Meet(speaker, other, BondRules.GreetBonus, BondRules.GreetedReason);
        speaker.SpeakAloud(line);
        NoteTold(recalled);

        var replySeed =unchecked((int)speaker.Serial.Value * 31 + (int)other.Serial.Value + now.Second);
        var reply = news != null
            ? GossipLines.Reply(news, replySeed)
            : GreetReply(speaker, other, replySeed);

        // SayTo paces the turn: a same-second answer to a greeting reads as one script.
        if (ShouldSpeak(reply, other.RecentSpeech()))
        {
            SpeechResponder.SayTo(other, speaker, reply);
        }

        if (news != null)
        {
            BystanderReply(speaker, other, news, replySeed);
        }

        speaker.ConsumeNearbyNotice(other.Name);
        other.ConsumeNearbyNotice(speaker.Name);

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} greeted {Other}", speaker.Name, other.Name);
        }

        return true;
    }

    /// <summary>
    /// The teller's line for a real journal event: the event kind picks the lines, distance and
    /// age pick the detail, a killer with a string of murders gets named, and the teller speaks
    /// in the first person about its own story. Null when this teller has nothing to say about it.
    /// </summary>
    public static string GossipLine(ShardEvent news, string tellerName, Point3D tellerAt, DateTime now, int seed)
    {
        if (news == null)
        {
            return null;
        }

        var distance = Math.Max(Math.Abs(tellerAt.X - news.X), Math.Abs(tellerAt.Y - news.Y));
        var detail = GossipRules.DetailLevel(distance, now - news.At, news.Involves(tellerName));
        var kills = news.Type == ShardEventType.Pk ? SosariaSettings.Journal?.KillsBy(news.Other, now) ?? 0 : 0;
        return GossipLines.Tell(news, tellerName, detail, kills, now, seed);
    }

    /// <summary>
    /// News for this teller now, or null: none while it is in a fight, and far news fades. With
    /// a player in earshot the model tells it in the teller's own words and the written line is
    /// skipped; the flag says so.
    /// </summary>
    private static ShardEvent PickNews(SosariaCharacter teller, DateTime now, out bool toldByModel)
    {
        toldByModel = false;

        if (teller.Combatant != null || teller.Warmode)
        {
            return null;
        }

        var news = SosariaSettings.Journal?.PickGossip(
            teller.Name,
            teller.Location,
            now,
            teller.HomeFacet,
            Utility.Random(100)
        );

        if (news != null)
        {
            toldByModel = Brain.RequestMusing(teller, NewsForModel(news, teller.Name, now));
        }

        return news;
    }

    /// <summary>Nobody tells a person their own story; that listener gets small talk instead.</summary>
    private static string TellTo(SosariaCharacter listener, ShardEvent news, SosariaCharacter teller, DateTime now, int seed) =>
        news == null || news.Involves(listener.Name) ? null : GossipLine(news, teller.Name, teller.Location, now, seed);

    public static string NewsForModel(ShardEvent news, string tellerName, DateTime now) =>
        $"News to pass on to the people near you: {GossipLines.Fact(news, tellerName, now)} Tell only these facts.";

    /// <summary>Someone else standing by chimes in on the story now and then.</summary>
    private static void BystanderReply(SosariaCharacter teller, SosariaCharacter listener, ShardEvent news, int seed)
    {
        if (teller.Map == null || teller.Map == Map.Internal ||
            MeetingRules.ChanceRoll(seed * 7 + 3) >= MeetingRules.BystanderReplyPercent)
        {
            return;
        }

        foreach (var mobile in teller.Map.GetMobilesInRange(teller.Location, MeetingRules.ChatRange))
        {
            if (mobile is not SosariaCharacter bystander || bystander == teller || bystander == listener ||
                !bystander.Alive || bystander.Hidden || bystander.Motor.Action != CharacterAction.Wander)
            {
                continue;
            }

            var reply = GossipLines.Reply(news, seed + (bystander.Name?.Length ?? 0));

            if (ShouldSpeak(reply, bystander.RecentSpeech()))
            {
                SpeechResponder.SayTo(bystander, teller, reply);
            }

            return;
        }
    }

    /// <summary>
    /// A small talk opener in the speaker's own voice when the persona has idle
    /// lines, else the shared pool. Keeps two standing characters from sounding
    /// like one script.
    /// </summary>
    public static string ChatLine(SosariaCharacter speaker, SosariaCharacter other, int seed)
    {
        var idle = OwnLine(() => speaker?.Persona?.PickIdleLine(speaker.Routine?.CurrentSkill?.Name));

        if (!string.IsNullOrEmpty(idle))
        {
            return idle;
        }

        return Talk.Line(TalkCategory.SmallTalk, seed, new TalkSlots { Name = other?.Name });
    }

    /// <summary>
    /// A short scripted answer to a named greeting, or null when the greeted
    /// character stays silent. Hostile characters do not answer. Costs no model
    /// call.
    /// </summary>
    public static string GreetReply(SosariaCharacter speaker, SosariaCharacter other, int seed)
    {
        var score = Recall.ScoreOf(MemoryStore.Shared, other, speaker);

        if (BondRules.IsCold(score) ||
            MeetingRules.ChanceRoll(seed) >= MeetingRules.GreetReplyChancePercent)
        {
            return null;
        }

        // In the greeted one's own written voice now and then, not always the shared pool.
        if (MeetingRules.ChanceRoll(seed * 7 + 1) < MeetingRules.PersonaReplyPercent &&
            OwnLine(() => other.Persona?.PickGreeting(speaker.Name)) is { } own)
        {
            return own;
        }

        return Talk.Line(TalkCategory.GreetReply, seed, new TalkSlots { Name = speaker.Name });
    }

    /// <summary>
    /// A small talk answer in the replier's own voice when its persona has written lines,
    /// else the shared pool — the same pattern <see cref="ChatLine"/> uses for openers.
    /// </summary>
    public static string ChatReply(SosariaCharacter replier, int seed)
    {
        if (MeetingRules.ChanceRoll(seed * 13 + 5) < MeetingRules.PersonaReplyPercent &&
            OwnLine(() => replier?.Persona?.PickIdleLine(replier.Routine?.CurrentSkill?.Name)) is { } own)
        {
            return own;
        }

        return Talk.Line(TalkCategory.SmallTalkReply, seed + (replier?.Name?.Length ?? 0), default);
    }

    /// <summary>
    /// A few picks for a written line that claims nothing the speaker cannot back: generated
    /// personas carry situational lines (a rez plea, an afk) a reply does not have behind it.
    /// </summary>
    private static string OwnLine(Func<string> pick)
    {
        for (var tries = 0; tries < PersonaTries; tries++)
        {
            var line = pick();

            if (SpeechFacts.ClaimsFree(line))
            {
                return line;
            }
        }

        return null;
    }

    private const int PersonaTries = 3;

    private const int RecallRollFactor = 11;
    private const int RecallRollOffset = 7;

    /// <summary>Now and then, a line about an adventure the two really shared (<see cref="GreetingLines.Recollect"/>).</summary>
    private static Recollection? RecallShared(SosariaCharacter speaker, SosariaCharacter other, string category, int percent, int seed) =>
        MeetingRules.ChanceRoll(unchecked(seed * RecallRollFactor + RecallRollOffset)) < percent
            ? GreetingLines.Recollect(MemoryStore.Shared, category, Recall.IdOf(speaker), Recall.IdOf(other), other.Name, seed)
            : null;

    /// <summary>A told adventure counts one more telling and lives longer.</summary>
    private static void NoteTold(Recollection? recalled)
    {
        if (recalled is { } told)
        {
            MemoryStore.Shared.NoteTold(told.AdventureId);
        }
    }

    private static bool TryGreet(SosariaCharacter speaker, SosariaCharacter other)
    {
        var now = Core.Now;
        var news = PickNews(speaker, now, out var toldByModel);

        // The model tells the story near a player; the greeting then stays a plain hello.
        return TryGreet(speaker, other, now, Brain.Conversations, speaker.RecentSpeech(), toldByModel ? null : news);
    }

    /// <summary>
    /// Small talk for two characters already standing together: a visitor and the
    /// friend, or people sharing a tavern floor. Scripted lines only, so a chat
    /// never spends a model call.
    /// </summary>
    public static bool TryChatNearby(SosariaCharacter speaker, SosariaCharacter preferred, int turn)
    {
        var other = ChatPartner(speaker, preferred, turn);
        return other != null && TryChat(
            speaker,
            other,
            Core.Now,
            Brain.Conversations,
            speaker.RecentSpeech(),
            unchecked((int)speaker.Serial.Value) ^ (turn * 7919) ^ Core.Now.Second
        );
    }

    /// <summary>The preferred partner when it is still close, else one nearby face picked by seed.</summary>
    public static SosariaCharacter ChatPartner(SosariaCharacter speaker, SosariaCharacter preferred, int turn)
    {
        if (speaker?.Map == null || speaker.Map == Map.Internal)
        {
            return null;
        }

        List<SosariaCharacter> nearby = null;

        foreach (var mobile in speaker.Map.GetMobilesInRange(speaker.Location, MeetingRules.ChatRange))
        {
            if (mobile is SosariaCharacter candidate)
            {
                (nearby ??= []).Add(candidate);
            }
        }

        return PickChatPartner(speaker, preferred, nearby, turn);
    }

    public static SosariaCharacter PickChatPartner(
        SosariaCharacter speaker,
        SosariaCharacter preferred,
        IReadOnlyList<SosariaCharacter> nearby,
        int turn
    )
    {
        if (ChatEligible(speaker, preferred))
        {
            return preferred;
        }

        List<SosariaCharacter> eligible = null;

        if (nearby != null)
        {
            for (var i = 0; i < nearby.Count; i++)
            {
                if (ChatEligible(speaker, nearby[i]))
                {
                    (eligible ??= []).Add(nearby[i]);
                }
            }
        }

        if (eligible == null)
        {
            return null;
        }

        var index = Math.Abs(unchecked((int)speaker.Serial.Value) + turn) % eligible.Count;
        return eligible[index];
    }

    private static bool ChatEligible(SosariaCharacter speaker, SosariaCharacter other) =>
        other != null &&
        other != speaker &&
        !other.Deleted &&
        other.Alive &&
        !other.Hidden &&
        other.Map == speaker?.Map &&
        other.Motor.Action == CharacterAction.Wander &&
        MeetingRules.InChatRange(speaker.Location, other.Location);

    public static bool TryChat(
        SosariaCharacter speaker,
        SosariaCharacter other,
        DateTime now,
        BotConversationTracker tracker,
        IReadOnlyList<string> recentSpeech,
        int seed
    )
    {
        if (speaker == null || other == null || tracker == null)
        {
            return false;
        }

        if (MeetingRules.SameWornName(speaker.Name, other.Name))
        {
            return false;
        }

        if (!tracker.CanGreet(speaker.Serial, other.Serial, now, MeetingRules.ChatPairQuiet))
        {
            return false;
        }

        var roll = MeetingRules.ChanceRoll(seed);
        var toldByModel = false;
        var news = roll < MeetingRules.ChatGossipChancePercent ? PickNews(speaker, now, out toldByModel) : null;

        if (toldByModel)
        {
            tracker.RecordGreeting(speaker.Serial, other.Serial, now);
            return true;
        }

        var line = TellTo(other, news, speaker, now, seed);
        Recollection? recalled = null;

        if (string.IsNullOrEmpty(line))
        {
            news = null;
            recalled = RecallShared(speaker, other, TalkCategory.RecallAdventure, MeetingRules.ChatRecallPercent, seed);
            line = recalled?.Line ?? ChatLine(speaker, other, seed);
        }

        if (string.IsNullOrEmpty(line) || !ShouldSpeak(line, recentSpeech))
        {
            return false;
        }

        tracker.RecordGreeting(speaker.Serial, other.Serial, now);
        speaker.Direction = speaker.GetDirectionTo(other);
        Meet(speaker, other, BondRules.ChatBonus, BondRules.ChattedReason);
        speaker.SpeakAloud(line);
        NoteTold(recalled);

        var replyRoll =MeetingRules.ChanceRoll(seed * 31 + 17);

        // With a player listening and a free local model, the listener answers in its own words.
        if (replyRoll < MeetingRules.ChatReplyChancePercent && !Brain.HearCharacter(other, speaker, line))
        {
            var reply = news != null
                ? GossipLines.Reply(news, seed)
                : ChatReply(other, seed);

            // SayTo paces the turn: an answer in the same second reads as one script.
            if (ShouldSpeak(reply, other.RecentSpeech()))
            {
                SpeechResponder.SayTo(other, speaker, reply);
            }
        }

        if (news != null)
        {
            BystanderReply(speaker, other, news, seed);
        }

        speaker.ConsumeNearbyNotice(other.Name);
        other.ConsumeNearbyNotice(speaker.Name);

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} chatted with {Other}", speaker.Name, other.Name);
        }

        return true;
    }

    // The two met here, both ways, and each thinks a little better of the other.
    private static void Meet(SosariaCharacter speaker, SosariaCharacter other, int bonus, string reason)
    {
        speaker.Memory.Met(other);
        other.Memory.Met(speaker);
        speaker.Memory.ShiftBond(other, bonus, reason);
        other.Memory.ShiftBond(speaker, bonus, reason);
    }
}
