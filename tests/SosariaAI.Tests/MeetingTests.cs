using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class MeetingTests : IDisposable
{
    private readonly List<Mobile> _placed = [];

    static MeetingTests() => Timer.Init(0);

    public MeetingTests()
    {
        TestMap.EnsureInternal();
        TestMap.EnsureRunningWorld();
    }

    public void Dispose() => TestMap.Remove(_placed);

    [Fact]
    public void ShouldSpeak_NewLine_Does()
    {
        Assert.True(Meeting.ShouldSpeak("Well met, Joel.", []));
        Assert.True(Meeting.ShouldSpeak("Well met, Joel.", ["Anyone seen a good deal on ingots today?"]));
    }

    [Fact]
    public void ShouldSpeak_NearRepeat_DoesNot()
    {
        const string line = "Well met, Joel.";
        Assert.False(Meeting.ShouldSpeak(line, [line]));
        Assert.False(Meeting.ShouldSpeak("Well met, Joel", [line]));
        Assert.False(Meeting.ShouldSpeak(" ", null));
        Assert.False(Meeting.ShouldSpeak(null, []));
    }

    private static readonly DateTime NewsNow = new(2026, 1, 1, 12, 0, 0);
    private static readonly Point3D Teller = new(1000, 1000, 0);

    [Fact]
    public void GossipReply_VariesBySeed()
    {
        var news = new ShardEvent { Type = ShardEventType.Death, Actor = "Mira", Place = "Covetous" };

        Assert.NotEqual(GossipLines.Reply(news, 0), GossipLines.Reply(news, 1));
    }

    [Fact]
    public void GossipReply_FillsTheNames()
    {
        var news = new ShardEvent
        {
            Type = ShardEventType.Pk,
            Actor = "Mira",
            Other = "Kerr",
            Place = "Yew moongate"
        };

        Assert.Contains("Kerr", GossipLines.Reply(news, 7));
    }

    [Fact]
    public void ChatLine_PrefersThePersonaVoice()
    {
        var speaker = new SosariaCharacter((Serial)0x7309) { Name = "Isolde" };
        speaker.Persona = new Persona { IdleLines = ["Only Isolde says this."] };
        var other = new SosariaCharacter((Serial)0x730A) { Name = "Joren" };

        Assert.Equal("Only Isolde says this.", Meeting.ChatLine(speaker, other, 0));
    }

    [Fact]
    public void ChatLine_WithoutPersonaLines_UsesTheSharedPool()
    {
        var speaker = new SosariaCharacter((Serial)0x730B) { Name = "Kel" };
        speaker.Persona = new Persona();
        var other = new SosariaCharacter((Serial)0x730C) { Name = "Lora" };

        Assert.Equal("hows the hunting Lora?", Meeting.ChatLine(speaker, other, 0));
    }

    [Fact]
    public void GossipLine_UsesTheRealPkEvent()
    {
        var line = Meeting.GossipLine(new ShardEvent
        {
            At = NewsNow - TimeSpan.FromMinutes(10),
            Type = ShardEventType.Pk,
            Actor = "Mira",
            Other = "Kerr",
            Place = "Yew moongate, Felucca",
            X = Teller.X + 100,
            Y = Teller.Y
        }, "Someone", Teller, NewsNow, 1);

        Assert.Contains("Kerr", line);
        Assert.Contains("Mira", line);
        Assert.Contains("yew moongate", line);
        Assert.DoesNotContain("Felucca", line);
    }

    [Fact]
    public void GossipLine_OwnStory_SpeaksFirstPerson()
    {
        var line = Meeting.GossipLine(new ShardEvent
        {
            At = NewsNow - TimeSpan.FromMinutes(2),
            Type = ShardEventType.Death,
            Actor = "Mira",
            Place = "Covetous",
            X = Teller.X + 300,
            Y = Teller.Y
        }, "Mira", Teller, NewsNow, 0);

        Assert.DoesNotContain("Mira", line);
        Assert.Equal("died at covetous just now ugh", line);
    }

    [Fact]
    public void GossipLine_Theft_IsNeverToldAsADeath()
    {
        for (var seed = 0; seed < 10; seed++)
        {
            var line = Meeting.GossipLine(new ShardEvent
            {
                At = NewsNow - TimeSpan.FromMinutes(10),
                Type = ShardEventType.Theft,
                Actor = "Slick",
                Other = "Mira",
                Place = "Britain",
                X = Teller.X + 100,
                Y = Teller.Y
            }, "Someone", Teller, NewsNow, seed);

            Assert.DoesNotContain("died", line);
            Assert.Contains("Slick", line);
        }
    }

    [Fact]
    public void GossipLine_TheftVictim_TellsItFirstPerson()
    {
        var line = Meeting.GossipLine(new ShardEvent
        {
            At = NewsNow - TimeSpan.FromMinutes(10),
            Type = ShardEventType.Theft,
            Actor = "Slick",
            Other = "Mira",
            Place = "Britain",
            X = Teller.X + 100,
            Y = Teller.Y
        }, "Mira", Teller, NewsNow, 0);

        Assert.Equal("Slick stole from me at britain!!", line);
    }

    [Fact]
    public void NewsForModel_CarriesOnlyTheFacts()
    {
        var text = Meeting.NewsForModel(new ShardEvent
        {
            At = NewsNow - TimeSpan.FromMinutes(20),
            Type = ShardEventType.Pk,
            Actor = "Aldreth",
            Other = "Grim",
            Place = "Despise, Felucca"
        }, "Someone", NewsNow);

        Assert.Contains("Aldreth was murdered by Grim at despise a bit ago.", text);
    }

    [Fact]
    public void TryChat_SamePair_RestsBetweenTurns()
    {
        var tracker = new BotConversationTracker();
        var speaker = new SosariaCharacter((Serial)0x7201) { Name = "Aldric" };
        var other = new SosariaCharacter((Serial)0x7202) { Name = "Bryn" };
        var now = new DateTime(2025, 1, 1, 12, 0, 0);

        Assert.True(Meeting.TryChat(speaker, other, now, tracker, speaker.RecentSpeech(), 7));
        Assert.False(Meeting.TryChat(speaker, other, now.AddSeconds(10), tracker, speaker.RecentSpeech(), 8));
        Assert.True(Meeting.TryChat(
            speaker,
            other,
            now + MeetingRules.ChatPairQuiet,
            tracker,
            speaker.RecentSpeech(),
            9));
    }

    [Fact]
    public void TryChat_SameWornName_IsSilent()
    {
        var tracker = new BotConversationTracker();
        var speaker = new SosariaCharacter((Serial)0x7204) { Name = "Aldric" };
        var other = new SosariaCharacter((Serial)0x7205) { Name = "aldric" };

        Assert.False(Meeting.TryChat(speaker, other, DateTime.UtcNow, tracker, [], 3));
    }

    [Fact]
    public void TryChat_WarmsBondsBothWaysAndNotesTheMeeting()
    {
        var tracker = new BotConversationTracker();
        var speaker = new SosariaCharacter((Serial)0x7206) { Name = "Cedric", CharacterId = "Felucca:chat-cedric" };
        var other = new SosariaCharacter((Serial)0x7207) { Name = "Dara", CharacterId = "Felucca:chat-dara" };

        Assert.True(Meeting.TryChat(speaker, other, DateTime.UtcNow, tracker, [], 5));
        var spoken = MemoryStore.Shared.BondOf(Recall.IdOf(speaker), Recall.IdOf(other));
        var heard = MemoryStore.Shared.BondOf(Recall.IdOf(other), Recall.IdOf(speaker));
        Assert.Equal(BondRules.ChatBonus, spoken?.Score);
        Assert.Equal(BondRules.ChattedReason, spoken?.LastReason);
        Assert.Equal(BondRules.ChatBonus, heard?.Score);
    }

    [Fact]
    public void InChatRange_UsesTheChebyshevBoundary()
    {
        var at = new Point3D(100, 100, 0);
        Assert.True(MeetingRules.InChatRange(at, new Point3D(100 + MeetingRules.ChatRange, 100, 0)));
        Assert.False(MeetingRules.InChatRange(at, new Point3D(100 + MeetingRules.ChatRange + 1, 100, 0)));
    }

    [Fact]
    public void GreetReply_UnderChance_AnswersByName()
    {
        var speaker = new SosariaCharacter((Serial)0x7301) { Name = "Aldric" };
        var other = new SosariaCharacter((Serial)0x7302) { Name = "Bryn" };

        var reply = Meeting.GreetReply(speaker, other, 10);

        Assert.NotNull(reply);
        Assert.Contains("Aldric", reply);
    }

    [Fact]
    public void GreetReply_OverChance_StaysSilent()
    {
        var speaker = new SosariaCharacter((Serial)0x7303) { Name = "Cedric" };
        var other = new SosariaCharacter((Serial)0x7304) { Name = "Dara" };

        Assert.Null(Meeting.GreetReply(speaker, other, MeetingRules.GreetReplyChancePercent + 10));
    }

    [Fact]
    public void GreetReply_ColdBond_StaysSilent()
    {
        var speaker = new SosariaCharacter((Serial)0x7305) { Name = "Edda", CharacterId = "Felucca:greetreply-edda" };
        var other = new SosariaCharacter((Serial)0x7306) { Name = "Fenn", CharacterId = "Felucca:greetreply-fenn" };
        other.Memory.ShiftBond(speaker, BondRules.ColdThreshold, "feud");

        Assert.Null(Meeting.GreetReply(speaker, other, 1));
    }

    [Fact]
    public void TryGreet_NormalGreet_SpeaksReply()
    {
        var tracker = new BotConversationTracker();
        var speaker = new SosariaCharacter((Serial)0x7307) { Name = "Gorm" };
        var other = new SosariaCharacter((Serial)0x7308) { Name = "Hale" };
        OnLand(speaker, other);
        var now = new DateTime(2025, 1, 1, 12, 0, 0);

        Assert.True(Meeting.TryGreet(speaker, other, now, tracker, [], null));

        // The greeting goes out at once; the answer waits on a typing delay
        // (SpeechResponder.SayTo), so only the opener lands in recent speech.
        Assert.NotEmpty(speaker.RecentSpeech());
    }

    [Fact]
    public void TryGreet_NewsAboutTheListener_IsNotToldToThem()
    {
        var tracker = new BotConversationTracker();
        var speaker = new SosariaCharacter((Serial)0x730D) { Name = "Gorm" };
        var other = new SosariaCharacter((Serial)0x730E) { Name = "Hale" };
        OnLand(speaker, other);
        var news = new ShardEvent
        {
            At = NewsNow - TimeSpan.FromMinutes(10),
            Type = ShardEventType.Death,
            Actor = "Hale",
            Place = "Covetous",
            X = 900,
            Y = 900
        };

        Assert.True(Meeting.TryGreet(speaker, other, NewsNow, tracker, [], news));
        Assert.DoesNotContain(speaker.RecentSpeech(), line => line.Contains("covetous", StringComparison.Ordinal));
    }

    [Fact]
    public void PickChatPartner_PrefersTheVisitTargetWhenClose()
    {
        var speaker = new SosariaCharacter((Serial)0x7208) { Name = "Edda" };
        var friend = new SosariaCharacter((Serial)0x7209) { Name = "Fenn" };
        var stranger = new SosariaCharacter((Serial)0x720A) { Name = "Gorm" };

        Assert.Same(friend, Meeting.PickChatPartner(speaker, friend, [stranger], 0));
    }

    [Fact]
    public void PickChatPartner_SkipsSelfAndEmptyLists()
    {
        var speaker = new SosariaCharacter((Serial)0x720B) { Name = "Hale" };
        var near = new SosariaCharacter((Serial)0x720C) { Name = "Isolde" };

        Assert.Null(Meeting.PickChatPartner(speaker, null, null, 0));
        Assert.Null(Meeting.PickChatPartner(speaker, null, [speaker], 0));
        Assert.Same(near, Meeting.PickChatPartner(speaker, null, [speaker, near], 0));
    }

    // The people stand on the test land, and Dispose takes them off it after the test.
    private void OnLand(params SosariaCharacter[] people)
    {
        var land = TestMap.EnsureLand();

        foreach (var person in people)
        {
            person.DefaultMobileInit();
            person.Map = land;
            _placed.Add(person);
        }
    }
}
