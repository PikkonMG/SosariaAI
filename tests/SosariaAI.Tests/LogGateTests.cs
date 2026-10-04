using System;
using SosariaAI.Logging;
using Xunit;

namespace SosariaAI.Tests;

public class LogGateTests
{
    private static readonly DateTime Start = new(2026, 9, 24, 20, 0, 0);
    private static readonly TimeSpan Quiet = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Think = TimeSpan.FromSeconds(2);

    [Fact]
    public void Opens_OncePerEvent_WhileTheLineKeepsComing()
    {
        var gate = new LogGate<string>(Quiet);
        var opened = 0;

        // A think every two seconds for five minutes is one event.
        for (var at = Start; at < Start + TimeSpan.FromMinutes(5); at += Think)
        {
            opened += gate.Opens("bobby picks off a dread spider", at) ? 1 : 0;
        }

        Assert.Equal(1, opened);
    }

    [Fact]
    public void Opens_AgainAfterTheKeyWentQuiet()
    {
        var gate = new LogGate<string>(Quiet);

        Assert.True(gate.Opens("camp", Start));
        Assert.False(gate.Opens("camp", Start + Quiet - Think));
        Assert.True(gate.Opens("camp", Start + Quiet - Think + Quiet));
    }

    [Fact]
    public void Opens_EachKeyOnItsOwn()
    {
        var gate = new LogGate<(int Crew, string Spot)>(Quiet);

        Assert.True(gate.Opens((1, "yew moongate"), Start));
        Assert.False(gate.Opens((1, "yew moongate"), Start + Think));
        Assert.True(gate.Opens((2, "yew moongate"), Start + Think));
        Assert.True(gate.Opens((1, "buccaneer's den"), Start + Think));
    }

    [Fact]
    public void IsOpen_NotesNothing_OnlyNoteShutsTheGate()
    {
        var gate = new LogGate<string>(Quiet);

        Assert.True(gate.IsOpen("warn", Start));
        Assert.True(gate.IsOpen("warn", Start + Think));

        gate.Note("warn", Start + Think);

        Assert.False(gate.IsOpen("warn", Start + Think + Think));
        Assert.True(gate.IsOpen("warn", Start + Think + Quiet));
    }

    [Fact]
    public void Sweep_KeepsFreshKeysShut_AndQuietKeysOpen()
    {
        var gate = new LogGate<int>(Quiet);

        for (var key = 0; key < LogGate<int>.SweepAbove; key++)
        {
            gate.Opens(key, Start);
        }

        var later = Start + Quiet;
        Assert.True(gate.Opens(-1, later));
        Assert.True(gate.Opens(-2, later));
        Assert.False(gate.Opens(-1, later + Think));
        Assert.True(gate.Opens(0, later + Think));
    }
}
