using System;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class BrainReplyRangeTests
{
    [Fact]
    public void ReplyRangeTiles_IsHearRangeTimesMultiplier()
    {
        Assert.Equal(
            BrainConfiguration.DefaultHearRange * Brain.HearRangeMultiplier,
            Brain.ReplyRangeTiles(BrainConfiguration.DefaultHearRange)
        );
        Assert.Equal(12, Brain.ReplyRangeTiles(6));
        Assert.Equal(0, Brain.ReplyRangeTiles(0));
    }

    [Fact]
    public void FromParty_IsCarriedFromEventToResult()
    {
        var evt = new BrainEvent(
            BrainEventKind.Spoken,
            (Serial)1u,
            "Bran",
            "Aria",
            (Serial)2u,
            true,
            "where are you",
            "walking",
            "Britain",
            DateTime.UnixEpoch
        );

        Assert.False(evt.FromParty);
        var party = evt with { FromParty = true };
        Assert.True(party.FromParty);

        var request = new BrainRequest(1, party.CharacterSerial, party.SpeakerSerial, party.CharacterName, party.Kind, "", "", FromParty: party.FromParty);
        var result = new BrainResult(1, request.CharacterSerial, request.SpeakerSerial, request.CharacterName, request.Kind, "here", 0, null, FromParty: request.FromParty);
        Assert.True(result.FromParty);
    }
}
