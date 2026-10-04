using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class SpeechFloorTests
{
    public SpeechFloorTests() => TestMap.EnsureInternal();

    [Theory]
    [InlineData("Bob", SpeechIntentKind.NameCall)]
    [InlineData("bob?", SpeechIntentKind.NameCall)]
    [InlineData("wts gm katana 5k", SpeechIntentKind.Trade)]
    [InlineData("how much for that ore", SpeechIntentKind.Trade)]
    [InlineData("lfg despise anyone?", SpeechIntentKind.Party)]
    [InlineData("wanna group?", SpeechIntentKind.Party)]
    [InlineData("me", SpeechIntentKind.Join)]
    [InlineData("im in!", SpeechIntentKind.Join)]
    [InlineData("ur such a noob", SpeechIntentKind.Insult)]
    [InlineData("gtg, cya all", SpeechIntentKind.Goodbye)]
    [InlineData("hail all", SpeechIntentKind.Greeting)]
    [InlineData("hi Bob", SpeechIntentKind.Greeting)]
    [InlineData("where is the healer", SpeechIntentKind.Question)]
    [InlineData("nice weather", SpeechIntentKind.Other)]
    [InlineData("", SpeechIntentKind.Other)]
    public void Classify_ReadsTheLineLikeAPlayer(string text, SpeechIntentKind expected)
    {
        Assert.Equal(expected, SpeechIntent.Classify(text, "Bob"));
    }

    [Fact]
    public void IsJoin_LongLineWithMeIsNotAJoin()
    {
        Assert.True(SpeechIntent.IsJoin("me pls"));
        Assert.False(SpeechIntent.IsJoin("tell me where the bank is please"));
        Assert.False(SpeechIntent.IsJoin("no"));
    }

    [Fact]
    public void AsksWhatDoing_KnowsTheUsualForms()
    {
        Assert.True(SpeechIntent.AsksWhatDoing("what is everyone doing?"));
        Assert.True(SpeechIntent.AsksWhatDoing("wyd"));
        Assert.True(SpeechIntent.AsksWhatDoing("Bob, what r u doing"));
        Assert.False(SpeechIntent.AsksWhatDoing("what time is it"));
    }

    [Fact]
    public void ContainsPhrase_MatchesWholeWordsOnly()
    {
        Assert.True(SpeechIntent.ContainsPhrase("ok, lets go", ["lets go"]));
        Assert.False(SpeechIntent.ContainsPhrase("hiking", ["hi"]));
        Assert.False(SpeechIntent.ContainsPhrase(null, ["hi"]));
    }

    [Fact]
    public void DoingAnswer_SaysTheRealSkillAndPlace()
    {
        var line = SpeechLines.DoingAnswer(SkillKinds.Mine, "minoc", 0);
        Assert.Equal("mining at minoc", line);
    }

    [Fact]
    public void DoingAnswer_ThiefDoesNotAdmitIt()
    {
        var line = SpeechLines.DoingAnswer(SkillKinds.Steal, "britain", 0);
        Assert.Equal("nothing much", line);
    }

    [Fact]
    public void Lines_Are1999Chat_NoRoleplayEmotes()
    {
        string[] floor =
        [
            TalkCategory.RespondName, TalkCategory.RespondRoomGreet, TalkCategory.RespondGreet, TalkCategory.FriendArrival,
            TalkCategory.RespondGoodbye, TalkCategory.RespondShrug, TalkCategory.RespondInsult, TalkCategory.RespondAck,
            TalkCategory.RespondWhat, TalkCategory.LfgJoinCall, TalkCategory.LfgOfferPlayer
        ];

        foreach (var topic in TalkDefaults.All)
        {
            if (Array.IndexOf(floor, topic.Name) < 0)
            {
                continue;
            }

            foreach (var line in topic.Lines)
            {
                Assert.DoesNotContain("*", line);
                Assert.Equal(line.Replace("{name}", "", StringComparison.Ordinal).ToLowerInvariant(),
                    line.Replace("{name}", "", StringComparison.Ordinal));
            }
        }
    }

    [Theory]
    [InlineData(SpeechIntentKind.Trade, false, true)]
    [InlineData(SpeechIntentKind.Trade, true, false)]
    [InlineData(SpeechIntentKind.Greeting, false, false)]
    public void LeftToTrade_OnlyAnUnnamedTradeLineIsNotAnswered(SpeechIntentKind intent, bool named, bool leftToTrade) =>
        Assert.Equal(leftToTrade, SpeechResponder.LeftToTrade(intent, named));

    [Fact]
    public void Greeting_FillsTheName_AndSkipsNamedLinesWithoutOne()
    {
        Assert.Equal("hey Mira", Talk.Line(TalkCategory.RespondGreet, 0, new TalkSlots { Name = "Mira" }));
        Assert.Null(Talk.Line(TalkCategory.RespondGreet, 0, default));
        Assert.Equal("hey", Talk.Line(TalkCategory.RespondRoomGreet, 0, default));
    }

    [Fact]
    public void RoomMayAnswer_CapsAnswersPerLine()
    {
        Assert.True(SpeechResponder.RoomMayAnswer(0, 0, SpeechResponder.RoomGreetReplyPercent));
        Assert.False(SpeechResponder.RoomMayAnswer(SpeechResponder.RoomReplyLimit, 0, SpeechResponder.RoomGreetReplyPercent));
        Assert.False(SpeechResponder.RoomMayAnswer(0, SpeechResponder.RoomGreetReplyPercent, SpeechResponder.RoomGreetReplyPercent));
    }

    [Fact]
    public void MayAnswerNamed_SometimesStaysQuiet()
    {
        Assert.True(SpeechResponder.MayAnswerNamed(0, SpeechResponder.NamedGreetReplyPercent));
        Assert.False(SpeechResponder.MayAnswerNamed(SpeechResponder.NamedGreetReplyPercent, SpeechResponder.NamedGreetReplyPercent));
    }

    [Fact]
    public void AnswersToFace_OnlyTheNearestOneRightBesideAnUnnamedLine()
    {
        Assert.True(SpeechResponder.AnswersToFace(named: false, SpeechResponder.FaceRange, nearest: true));
        Assert.False(SpeechResponder.AnswersToFace(named: false, SpeechResponder.FaceRange + 1, nearest: true));
        Assert.False(SpeechResponder.AnswersToFace(named: false, SpeechResponder.FaceRange, nearest: false));
        Assert.False(SpeechResponder.AnswersToFace(named: true, SpeechResponder.FaceRange, nearest: true));
    }

    [Fact]
    public void NameReplyDue_ARepeatedNameIsAnsweredOnceInTheGuard()
    {
        var now = new DateTime(2026, 9, 27, 3, 0, 0, DateTimeKind.Utc);

        Assert.True(SpeechResponder.NameReplyDue(null, now));
        Assert.False(SpeechResponder.NameReplyDue(now - SpeechResponder.NameReplyGuard + TimeSpan.FromSeconds(1), now));
        Assert.True(SpeechResponder.NameReplyDue(now - SpeechResponder.NameReplyGuard, now));
    }

    [Fact]
    public void StandsNearer_APlayerAsNearTheSpeakerTakesTheLine()
    {
        // Two players talking a tile apart: the bot beside them is not the one spoken to.
        Assert.True(SpeechResponder.StandsNearer(mine: 1, theirs: 1, isHuman: true, lowerSerial: false));
        Assert.True(SpeechResponder.StandsNearer(mine: 2, theirs: 1, isHuman: true, lowerSerial: false));
        Assert.False(SpeechResponder.StandsNearer(mine: 1, theirs: 2, isHuman: true, lowerSerial: true));

        // Between two characters a tie goes to the lower serial.
        Assert.True(SpeechResponder.StandsNearer(mine: 1, theirs: 1, isHuman: false, lowerSerial: true));
        Assert.False(SpeechResponder.StandsNearer(mine: 1, theirs: 1, isHuman: false, lowerSerial: false));
        Assert.True(SpeechResponder.StandsNearer(mine: 2, theirs: 1, isHuman: false, lowerSerial: false));
    }

    [Fact]
    public void FaceReplyDue_OneSpeakerGetsOneHmInTheGuard()
    {
        var now = new DateTime(2026, 9, 27, 3, 0, 0, DateTimeKind.Utc);

        Assert.True(SpeechResponder.FaceReplyDue(null, now));
        Assert.False(SpeechResponder.FaceReplyDue(now - SpeechResponder.FaceReplyGuard + TimeSpan.FromSeconds(1), now));
        Assert.True(SpeechResponder.FaceReplyDue(now - SpeechResponder.FaceReplyGuard, now));
    }

    [Fact]
    public void KnowsWell_FriendByBond()
    {
        var character = new SosariaCharacter((Serial)0x7A01) { Name = "Bob", CharacterId = "Felucca:knowswell-bob" };
        var friend = new SosariaCharacter((Serial)0x7A02) { Name = "Mira", CharacterId = "Felucca:knowswell-mira" };
        var stranger = new SosariaCharacter((Serial)0x7A03) { Name = "Kerr", CharacterId = "Felucca:knowswell-kerr" };
        character.Memory.ShiftBond(friend, BondRules.WarmThreshold, "hunted together");

        Assert.True(SpeechResponder.KnowsWell(character, friend));
        Assert.False(SpeechResponder.KnowsWell(character, stranger));
        Assert.False(SpeechResponder.KnowsWell(character, null));
    }

    [Fact]
    public void Hears_OnlyAPlayer()
    {
        var character = new SosariaCharacter((Serial)0x7A04) { Name = "Bob" };
        var other = new SosariaCharacter((Serial)0x7A05) { Name = "Mira" };

        Assert.False(SpeechResponder.Hears(character, other));
        Assert.False(SpeechResponder.Hears(character, null));
    }
}
