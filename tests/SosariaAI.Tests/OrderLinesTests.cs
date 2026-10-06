using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class OrderLinesTests
{
    [Theory]
    [InlineData("gm smith here, taking orders")]
    [InlineData("full plate made to order")]
    [InlineData("i can make you one")]
    [InlineData("Hello everyone. Crafter here, taking orders.")]
    [InlineData("Who wants plate? I take orders at the bank.")]
    public void OffersWork(string line) => Assert.True(OrderLines.OffersWork(line));

    [Theory]
    [InlineData("u take orders {name}?")]
    [InlineData("{name} can u make me some {item}?")]
    [InlineData("make me a gm katana")]
    public void AsksForWork(string line) => Assert.True(OrderLines.AsksForWork(line));

    [Theory]
    [InlineData("anyone need repairs?")]
    [InlineData("repairs while you wait")]
    public void ClaimsRepairs(string line) => Assert.True(OrderLines.ClaimsRepairs(line));

    [Theory]
    [InlineData("ingots r so expensive")]
    [InlineData("selling plate at the bank later")]
    [InlineData("the order of the silver serpent")]
    [InlineData("i need to repair")]
    public void PlainTalkIsNoOrder(string line) => Assert.False(OrderLines.IsOrderTalk(line) || OrderLines.ClaimsRepairs(line));
}
