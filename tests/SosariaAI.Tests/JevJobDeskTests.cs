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
    private const string LowGold = "low on gold";
    private const string PlayerNear = "a player stands near";

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
    public void IsNewMoment_OncePerMarkedMomentUntilItEnds()
    {
        var desk = new JevJobDesk();

        Assert.True(desk.IsNewMoment(Connor, LowGold));
        desk.MarkMoment(Connor, LowGold);
        Assert.False(desk.IsNewMoment(Connor, LowGold));
        Assert.True(desk.IsNewMoment(Connor, PlayerNear));
        desk.MarkMoment(Connor, PlayerNear);
        Assert.False(desk.IsNewMoment(Connor, null));
        Assert.True(desk.IsNewMoment(Connor, PlayerNear));
    }

    [Fact]
    public void IsNewMoment_AMomentWhoseAskDidNotGoOutStaysNew()
    {
        // A refused ask (cooldown, budget, busy, full queue) used the moment up, so it was never asked.
        var desk = new JevJobDesk();

        Assert.True(desk.IsNewMoment(Connor, PlayerNear));
        Assert.True(desk.IsNewMoment(Connor, PlayerNear));
        desk.MarkMoment(Connor, PlayerNear);
        Assert.False(desk.IsNewMoment(Connor, PlayerNear));
    }

    [Fact]
    public void MarkMoment_NoMomentRecordsNothing()
    {
        var desk = new JevJobDesk();
        desk.MarkMoment(Connor, null);

        Assert.True(desk.IsNewMoment(Connor, LowGold));
    }
}
