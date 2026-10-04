using System;
using Server;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class WarningGateTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 9, 0, 0);
    private static readonly TimeSpan Scan = TimeSpan.FromSeconds(2);
    private static readonly Serial Speaker = (Serial)0x200u;
    private static readonly Serial OtherSpeaker = (Serial)0x201u;
    private static readonly Serial Listener = (Serial)0x300u;
    private static readonly Serial OtherListener = (Serial)0x301u;
    private static readonly Serial Red = (Serial)0x100u;
    private static readonly Serial OtherRed = (Serial)0x101u;

    [Fact]
    public void TryWarn_FirstWarning_GoesOut() =>
        Assert.True(new WarningGate().TryWarn(Speaker, Listener, Red, Now));

    [Fact]
    public void TryWarn_OneLinePerSpeakerInTheRest_WhateverTheRed()
    {
        var gate = new WarningGate();

        Assert.True(gate.TryWarn(Speaker, Listener, Red, Now));
        Assert.False(gate.TryWarn(Speaker, OtherListener, OtherRed, Now + Scan));
        Assert.False(gate.SpeakerReady(Speaker, Now + WarningGate.SpeakerRest - Scan));
        Assert.True(gate.TryWarn(Speaker, OtherListener, OtherRed, Now + WarningGate.SpeakerRest));
    }

    [Fact]
    public void TryWarn_SameWarning_OnceInTheLongerRest()
    {
        var gate = new WarningGate();

        Assert.True(gate.TryWarn(Speaker, Listener, Red, Now));
        Assert.False(gate.TryWarn(Speaker, Listener, Red, Now + WarningGate.SpeakerRest));
        Assert.False(gate.TryWarn(Speaker, Listener, Red, Now + WarningGate.SameWarningRest - Scan));
        Assert.True(gate.TryWarn(Speaker, Listener, Red, Now + WarningGate.SameWarningRest));
    }

    [Fact]
    public void TryWarn_ARefusedWarning_DoesNotPushTheRestBack()
    {
        var gate = new WarningGate();

        Assert.True(gate.TryWarn(Speaker, Listener, Red, Now));

        // A red in sight on every scan: the refusals must not keep the speaker quiet for good.
        for (var at = Now + Scan; at < Now + WarningGate.SpeakerRest; at += Scan)
        {
            Assert.False(gate.TryWarn(Speaker, OtherListener, OtherRed, at));
        }

        Assert.True(gate.TryWarn(Speaker, OtherListener, OtherRed, Now + WarningGate.SpeakerRest));
    }

    [Fact]
    public void TryWarn_EachSpeakerOnItsOwn()
    {
        var gate = new WarningGate();

        Assert.True(gate.TryWarn(Speaker, Listener, Red, Now));
        Assert.True(gate.TryWarn(OtherSpeaker, Listener, Red, Now));
    }

    [Fact]
    public void ToldLately_OnceTold_TheCrowdStaysQuiet()
    {
        var gate = new WarningGate();

        Assert.False(gate.ToldLately(Red, 0, 1000, 1000, Now));

        gate.NoteTold(Red, 0, 1000, 1000, Now);

        // The same cell and the next one over already heard the sighting.
        Assert.True(gate.ToldLately(Red, 0, 1008, 1008, Now + Scan));
        Assert.True(gate.ToldLately(Red, 0, 1000, 1000, Now + WarningGate.ToldRest - Scan));
        Assert.False(gate.ToldLately(Red, 0, 1000, 1000, Now + WarningGate.ToldRest));
    }

    [Fact]
    public void ToldLately_OtherRedOrMap_StillWarned()
    {
        var gate = new WarningGate();
        gate.NoteTold(Red, 0, 1000, 1000, Now);

        Assert.False(gate.ToldLately(OtherRed, 0, 1000, 1000, Now));
        Assert.False(gate.ToldLately(Red, 1, 1000, 1000, Now));
        Assert.False(gate.ToldLately(Red, 0, 2000, 2000, Now));
    }

    [Fact]
    public void TryWarn_AnHourInTheDen_AtMostTwoLinesAMinute()
    {
        var gate = new WarningGate();
        var said = 0;
        var reds = new[] { Red, OtherRed, (Serial)0x102u, (Serial)0x103u };
        var scans = 0;

        for (var at = Now; at < Now + TimeSpan.FromHours(1); at += Scan, scans++)
        {
            said += gate.TryWarn(Speaker, Serial.Zero, reds[scans % reds.Length], at) ? 1 : 0;
        }

        Assert.True(said <= TimeSpan.FromHours(1) / WarningGate.SpeakerRest);
    }
}
