using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class PromiseLinesTests
{
    // Each of these sat in a persona's lines, said at random to whoever stood near, with no
    // group, trip or meeting behind it. A player who answered yes got nothing.
    [Theory]
    [InlineData("hail {name}, heading to shame, come along")]
    [InlineData("hail, {name}, join me at the graveyard")]
    [InlineData("{name}, you coming or do I solo it?")]
    [InlineData("You coming, {name}?")]
    [InlineData("heading to shame soon, want to come")]
    [InlineData("anyone want to hunt ogres")]
    [InlineData("anyone up for despise?")]
    [InlineData("who wants to hunt with me")]
    [InlineData("going to hunt skara if anyone joins")]
    [InlineData("Dungeon crawl tonight. Anyone joining?")]
    [InlineData("heading to covetous if you want in")]
    [InlineData("heading to the graveyard, need company")]
    [InlineData("Hello {name}, follow me to the good spot.")]
    [InlineData("gate to yew is open, follow me")]
    [InlineData("{name}, meet me by the moongate")]
    [InlineData("newbs welcome to tag along")]
    [InlineData("Miners, walk with me. No charge.")]
    [InlineData("cmon lets go hunt, im bored stiff")]
    [InlineData("{name}! come hunt with us")]
    [InlineData("With me, {name}.")]
    [InlineData("With me, {name}. Stay close.")]
    [InlineData("Moving shot, stay with me.")]
    public void IsInvite_TrueForAnOfferNoCodeKeeps(string line)
    {
        Assert.True(PromiseLines.IsInvite(line));
    }

    [Theory]
    [InlineData("pk on me, run")]
    [InlineData("all follow me")]
    [InlineData("Got a map. Do not follow me.")]
    [InlineData("dragons follow me everywhere")]
    [InlineData("lol she wont follow me")]
    [InlineData("{name}! didnt hear you coming")]
    [InlineData("hail, you coming from brit")]
    [InlineData("Good to meet you, {name}.")]
    [InlineData("meet me at {price}")]
    [InlineData("meet me at 400 and its yours")]
    [InlineData("meet me halfway")]
    [InlineData("for jhelom, lets go")]
    [InlineData("Warning, pk heading to the bank.")]
    [InlineData("")]
    [InlineData(null)]
    public void IsInvite_FalseForOrdinaryChat(string line)
    {
        Assert.False(PromiseLines.IsInvite(line));
    }

    [Theory]
    [InlineData("hail {name}, you heading to brit too")]
    [InlineData("hail {name}, heading to despise?")]
    [InlineData("{name} you headed to the graveyard?")]
    [InlineData("bag full, heading to bank")]
    [InlineData("im heading to the mines, brb")]
    [InlineData("Off to the mine again. Back later.")]
    [InlineData("just passing through on my way to britain")]
    [InlineData("On my way, {name}.")]
    public void ClaimsTrip_TrueForTheSpeakersOrListenersOwnTrip(string line)
    {
        // Ansgar stood at the Britain bank with the player and asked whether they were
        // heading to Britain too.
        Assert.True(PromiseLines.ClaimsTrip(line));
        Assert.True(PromiseLines.IsPromise(line));
    }

    [Theory]
    [InlineData("Warning, pk heading to the bank.")]
    [InlineData("lotta folks heading to the dungeon today")]
    [InlineData("Found ingots at 5 a piece on my way back.")]
    [InlineData("my foil is going to snap soon i swear")]
    [InlineData("pk on me, run")]
    [InlineData("Restocked the vendor on my way back.")]
    [InlineData(null)]
    public void ClaimsTrip_FalseForOtherPeoplesTripsAndOrdinaryChat(string line)
    {
        Assert.False(PromiseLines.ClaimsTrip(line));
    }
}
