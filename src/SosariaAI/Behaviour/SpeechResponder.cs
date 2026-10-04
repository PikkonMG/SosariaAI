using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Deliberation;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Social;

namespace SosariaAI.Behaviour;

/// <summary>
/// What a character does with a player's spoken line. Trade talk stays with the trade
/// handler, group calls go to the party board, a line aimed at the character goes to the
/// model when one can answer, and everything else gets the free speech floor: turn and
/// answer a name (once, not to every repeat of it), greet back a room (or not: nobody
/// answering is also 1999), say bye, shrug off questions, brush off insults, and "hm?" at a
/// line said right beside it that it did not follow, as a person ignored in their face would:
/// never when a player stands as near, whom the line was for, and to one speaker once a
/// minute at most.
/// </summary>
public static class SpeechResponder
{
    /// <summary>A line said to the room gets at most this many answers.</summary>
    public const int RoomReplyLimit = 2;

    public const int RoomGreetReplyPercent = 35;
    public const int RoomGoodbyeReplyPercent = 25;
    public const int RoomQuestionReplyPercent = 15;

    /// <summary>Even a greeting said to one person by name goes unanswered sometimes.</summary>
    public const int NamedGreetReplyPercent = 90;

    public const int NamedAckReplyPercent = 60;
    public const int InsultPenalty = 15;

    public const string InsultReason = "insulted me";

    /// <summary>A line said this close is said to the face of whoever stands nearest.</summary>
    public const int FaceRange = 2;

    /// <summary>A line said to its face gets a "hm?" this often; the rest of the time, nothing.</summary>
    public const int FaceReplyPercent = 45;

    /// <summary>One speaker gets at most one "hm?" in this time, however many lines it says.</summary>
    public static readonly TimeSpan FaceReplyGuard = TimeSpan.FromMinutes(1);

    /// <summary>A name said again inside this time gets no new answer: "bob bob bob" is not a chat.</summary>
    public static readonly TimeSpan NameReplyGuard = TimeSpan.FromSeconds(15);

    /// <summary>The same words from the same speaker inside this window are one room line.</summary>
    public static readonly TimeSpan RoomLineWindow = TimeSpan.FromSeconds(10);

    /// <summary>A friend or guildmate is greeted by name on arrival once in this time.</summary>
    public static readonly TimeSpan ArrivalGreetRest = TimeSpan.FromMinutes(20);

    private static readonly Dictionary<Serial, RoomLine> RoomLines = new();
    private static readonly Dictionary<Serial, DateTime> LastNameReply = new();
    private static readonly Dictionary<Serial, DateTime> LastFaceReply = new();

    /// <summary>A living player this character can see, close enough to be heard. The Brain being off does not matter.</summary>
    public static bool Hears(SosariaCharacter character, Mobile from) =>
        character is { Deleted: false, Alive: true } && from != null && from != character && People.IsHuman(from) &&
        from.Alive && People.Perceives(character, from) && from.Map == character.Map && from.InRange(character, Brain.HearRange);

    public static void Hear(SosariaCharacter character, Mobile speaker, string text)
    {
        // Inside a haggle "ok", "4k" and "no" belong to the trade, whose handler hears every line itself.
        if (!Hears(character, speaker) || string.IsNullOrWhiteSpace(text) || TradeSessions.IsTrading(character, speaker))
        {
            return;
        }

        var intent = SpeechIntent.Classify(text, character.Name);
        var named = AttentionGate.MentionsName(text, character.Name);

        // Trade talk has its own handler too.
        if (LeftToTrade(intent, named) ||
            (intent is SpeechIntentKind.Party or SpeechIntentKind.Join && LfgBoard.HearPlayer(character, speaker, text)) ||
            Brain.TakesPlayerLine(character, speaker, text, intent))
        {
            return;
        }

        Answer(character, speaker, text, intent, named);
    }

    /// <summary>
    /// True when a trade line is the trade handler's alone: one said to the room. A trade line
    /// said to this character by name that opened no haggle with it ("bob wanna buy my sword",
    /// asked of someone with nothing held up) is talk to it, and gets an answer.
    /// </summary>
    public static bool LeftToTrade(SpeechIntentKind intent, bool named) => intent == SpeechIntentKind.Trade && !named;

    /// <summary>The floor's written answer, said after a short typing delay. Silence is a valid answer.</summary>
    public static void Answer(SosariaCharacter character, Mobile speaker, string text, SpeechIntentKind intent, bool named)
    {
        var now = Core.Now;
        var seed = Utility.Random(int.MaxValue);
        var roll = Utility.Random(100);
        var friendly = KnowsWell(character, speaker);
        string line = null;

        switch (intent)
        {
            case SpeechIntentKind.NameCall:
                {
                    line = NameReplyDue(LastNameReply.TryGetValue(character.Serial, out var last) ? last : null, now)
                        ? Talk.Line(TalkCategory.RespondName, seed, Named(speaker))
                        : null;

                    if (line != null)
                    {
                        LastNameReply[character.Serial] = now;
                    }

                    break;
                }
            case SpeechIntentKind.Greeting:
                {
                    line = named || friendly
                        ? MayAnswerNamed(roll, NamedGreetReplyPercent) ? Talk.Line(TalkCategory.RespondGreet, seed, Named(speaker)) : null
                        : TakeRoomTurn(speaker, text, now, roll, RoomGreetReplyPercent)
                            ? Talk.Line(TalkCategory.RespondRoomGreet, seed, default)
                            : null;
                    break;
                }
            case SpeechIntentKind.Goodbye:
                {
                    line = named || friendly || TakeRoomTurn(speaker, text, now, roll, RoomGoodbyeReplyPercent)
                        ? Talk.Line(TalkCategory.RespondGoodbye, seed, Named(speaker))
                        : null;
                    break;
                }
            case SpeechIntentKind.Question:
            case SpeechIntentKind.Trade:
                {
                    if (named && SpeechIntent.AsksWhatDoing(text))
                    {
                        line = SpeechLines.DoingAnswer(
                            character.Routine?.CurrentSkill?.Name,
                            GossipLines.PlaceWord(PlaceNames.Of(character)),
                            seed
                        );
                    }
                    else if (named || TakeRoomTurn(speaker, text, now, roll, RoomQuestionReplyPercent))
                    {
                        line = Talk.Line(TalkCategory.RespondShrug, seed, default);
                    }

                    break;
                }
            case SpeechIntentKind.Insult when named:
                {
                    character.Memory.ShiftBond(speaker, -InsultPenalty, InsultReason);
                    line = Talk.Line(TalkCategory.RespondInsult, seed, default);
                    break;
                }
            case SpeechIntentKind.Join or SpeechIntentKind.Other when named:
                {
                    line = MayAnswerNamed(roll, NamedAckReplyPercent) ? Talk.Line(TalkCategory.RespondAck, seed, default) : null;
                    break;
                }
            case SpeechIntentKind.Other when AnswersToFace(named, NavMetric.Chebyshev(character.Location, speaker.Location), NearestTo(character, speaker)):
                {
                    line = FaceReplyDue(LastFaceReply.TryGetValue(speaker.Serial, out var last) ? last : null, now) &&
                           MayAnswerNamed(roll, FaceReplyPercent)
                        ? Talk.Line(TalkCategory.RespondWhat, seed, default)
                        : null;

                    if (line != null)
                    {
                        LastFaceReply[speaker.Serial] = now;
                    }

                    break;
                }
        }

        if (named)
        {
            // Spoken to by name: stop, turn, and wait for the next line.
            character.Direction = character.GetDirectionTo(speaker);
            character.Conversation.Hold(speaker.Serial, now, Brain.ChatHold);
        }

        if (!string.IsNullOrEmpty(line))
        {
            SayTo(character, speaker, line);
        }
    }

    /// <summary>
    /// A friend or a guildmate player walked up: greet them by name. Strangers are left be,
    /// as people were. Runs on the ambient scan, so only near a player.
    /// </summary>
    public static void GreetArrivals(SosariaCharacter character)
    {
        if (character?.Map == null || character.Map == Map.Internal || !character.Alive ||
            character.Hidden || character.Combatant != null || character.Conversation.IsActive(Core.Now))
        {
            return;
        }

        var now = Core.Now;

        foreach (var mobile in character.Map.GetMobilesInRange(character.Location, MeetingRules.GreetingRange))
        {
            if (!People.IsHuman(mobile) || !mobile.Alive || !People.Perceives(character, mobile) || !KnowsWell(character, mobile) ||
                !Brain.Conversations.CanGreet(character.Serial, mobile.Serial, now, ArrivalGreetRest))
            {
                continue;
            }

            Brain.Conversations.RecordGreeting(character.Serial, mobile.Serial, now);
            SayTo(character, mobile, Talk.Line(TalkCategory.FriendArrival, Named(mobile)));
            return;
        }
    }

    private static TalkSlots Named(Mobile person) => new() { Name = person.Name };

    /// <summary>A friend (warm bond) or someone in the same guild.</summary>
    public static bool KnowsWell(SosariaCharacter character, Mobile other) =>
        other != null &&
        (BondRules.IsWarm(Recall.ScoreOf(MemoryStore.Shared, character, other)) ||
         SameGuild(character, other));

    public static bool SameGuild(Mobile first, Mobile second) =>
        first?.Guild != null && second != null && first.Guild == second.Guild;

    public static bool MayAnswerNamed(int roll100, int chancePercent) => roll100 >= 0 && roll100 < chancePercent;

    /// <summary>
    /// True when an unnamed line was said to this character's face: within
    /// <see cref="FaceRange"/>, and no one else stood nearer the speaker.
    /// </summary>
    public static bool AnswersToFace(bool named, int tiles, bool nearest) => !named && tiles <= FaceRange && nearest;

    /// <summary>True when the character last answered its name <see cref="NameReplyGuard"/> ago or longer, or never.</summary>
    public static bool NameReplyDue(DateTime? lastReply, DateTime now) => GuardOver(lastReply, now, NameReplyGuard);

    /// <summary>True when the speaker last got a "hm?" <see cref="FaceReplyGuard"/> ago or longer, or never.</summary>
    public static bool FaceReplyDue(DateTime? lastReply, DateTime now) => GuardOver(lastReply, now, FaceReplyGuard);

    /// <summary>
    /// True when another person stands nearer the speaker than this character, <paramref name="mine"/>
    /// tiles off: a player as near is the one spoken to, and between characters a tie goes
    /// to the lower serial.
    /// </summary>
    public static bool StandsNearer(int mine, int theirs, bool isHuman, bool lowerSerial) =>
        theirs < mine || theirs == mine && (isHuman || lowerSerial);

    private static bool GuardOver(DateTime? last, DateTime now, TimeSpan guard) => last is not { } at || now - at >= guard;

    // Nobody stands nearer the speaker than this character: not another character, and not
    // a player, who is whom a player beside one talks to.
    private static bool NearestTo(SosariaCharacter character, Mobile speaker)
    {
        var mine = NavMetric.Chebyshev(character.Location, speaker.Location);

        foreach (var mobile in speaker.Map.GetMobilesInRange(speaker.Location, FaceRange))
        {
            var isHuman = People.IsHuman(mobile);

            if (mobile is not { Deleted: false, Alive: true } || mobile == character || mobile == speaker ||
                (isHuman ? !People.Perceives(character, mobile) : mobile is not SosariaCharacter))
            {
                continue;
            }

            if (StandsNearer(mine, NavMetric.Chebyshev(mobile.Location, speaker.Location), isHuman, mobile.Serial < character.Serial))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A room line takes an answer when under the limit and the hearer's roll passes.</summary>
    public static bool RoomMayAnswer(int repliesSoFar, int roll100, int chancePercent) =>
        repliesSoFar < RoomReplyLimit && roll100 >= 0 && roll100 < chancePercent;

    private static bool TakeRoomTurn(Mobile speaker, string text, DateTime now, int roll100, int chancePercent)
    {
        if (!RoomLines.TryGetValue(speaker.Serial, out var room) ||
            !string.Equals(room.Text, text, StringComparison.Ordinal) || now - room.At > RoomLineWindow)
        {
            room = new RoomLine(text, now, 0);
        }

        if (!RoomMayAnswer(room.Replies, roll100, chancePercent))
        {
            RoomLines[speaker.Serial] = room;
            return false;
        }

        RoomLines[speaker.Serial] = room with { Replies = room.Replies + 1 };
        return true;
    }

    /// <summary>
    /// Says a line to someone after a short typing delay, facing them, if both are still about.
    /// No line (an emptied talk file) says nothing. The answer goes out as directed speech:
    /// the ambient heard-nearby hold swallows "hey" every time a crowd just heard one, which
    /// left greets and questions visibly unanswered.
    /// </summary>
    public static void SayTo(SosariaCharacter character, Mobile speaker, string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        var serial = character.Serial;
        var speakerSerial = speaker.Serial;

        Timer.StartTimer(ReplyDelay.For(line), () =>
        {
            if (World.FindMobile(serial) is not SosariaCharacter { Deleted: false, Alive: true } living ||
                World.FindMobile(speakerSerial) is not { Deleted: false } listener || !People.Perceives(living, listener))
            {
                return;
            }

            living.Direction = living.GetDirectionTo(listener);
            living.SpeakDirected(line);
        });
    }

    private readonly record struct RoomLine(string Text, DateTime At, int Replies);
}
