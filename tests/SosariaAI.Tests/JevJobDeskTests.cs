using System;
using Server;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class JevJobDeskTests
{
    private static readonly DateTime Start = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Serial Connor = (Serial)0x10u;
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(3);

    [Fact]
    public void Waiting_HoldsTheCharacterUntilTheWaitRunsOut()
    {
        var desk = new JevJobDesk();
        desk.Open(Connor, new JevJobAsk(7, Start, []));

        Assert.True(desk.Waiting(Connor, Start.AddSeconds(2), Wait));
        Assert.False(desk.Waiting(Connor, Start.AddSeconds(3), Wait));
        Assert.True(desk.TakeExpired(Connor));
        Assert.False(desk.TakeExpired(Connor));
    }

    [Fact]
    public void TryTake_MatchesOnlyTheOpenRequest()
    {
        var desk = new JevJobDesk();
        desk.Open(Connor, new JevJobAsk(7, Start, []));

        Assert.False(desk.TryTake(Connor, 6, out _));
        Assert.True(desk.TryTake(Connor, 7, out var ask));
        Assert.Equal(7, ask.RequestId);
        Assert.False(desk.TryTake(Connor, 7, out _));
        Assert.False(desk.Waiting(Connor, Start, Wait));
    }

    [Fact]
    public void LateAnswer_FindsNothingAfterTheWait()
    {
        var desk = new JevJobDesk();
        desk.Open(Connor, new JevJobAsk(7, Start, []));

        Assert.False(desk.Waiting(Connor, Start.AddSeconds(4), Wait));
        Assert.False(desk.TryTake(Connor, 7, out _));
    }

    [Fact]
    public void CoolingDown_KeepsASecondAskAwayForTheCooldown()
    {
        var desk = new JevJobDesk();
        var cooldown = TimeSpan.FromSeconds(20);
        desk.Open(Connor, new JevJobAsk(7, Start, []));

        Assert.True(desk.CoolingDown(Connor, Start.AddSeconds(19), cooldown));
        Assert.False(desk.CoolingDown(Connor, Start.AddSeconds(20), cooldown));
    }

    [Fact]
    public void Forget_DropsTheOpenAsk()
    {
        var desk = new JevJobDesk();
        desk.Open(Connor, new JevJobAsk(7, Start, []));
        desk.Forget(Connor);

        Assert.False(desk.Waiting(Connor, Start, Wait));
        Assert.False(desk.TakeExpired(Connor));
    }

    [Fact]
    public void NewMoment_OncePerMomentUntilItEnds()
    {
        const string LowGold = "low on gold";
        const string PlayerNear = "a player stands near";
        var desk = new JevJobDesk();

        Assert.True(desk.NewMoment(Connor, LowGold));
        Assert.False(desk.NewMoment(Connor, LowGold));
        Assert.True(desk.NewMoment(Connor, PlayerNear));
        Assert.False(desk.NewMoment(Connor, null));
        Assert.True(desk.NewMoment(Connor, PlayerNear));
    }
}
