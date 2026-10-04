using System;
using Server;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class CharacterCooldownTests
{
    [Fact]
    public void IsCooling_AfterMark_UntilWindowPasses()
    {
        var cooldown = new CharacterCooldown();
        var serial = (Serial)10u;
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var window = TimeSpan.FromSeconds(8);

        Assert.False(cooldown.IsCooling(serial, start, window));
        cooldown.Mark(serial, start);
        Assert.True(cooldown.IsCooling(serial, start.AddSeconds(7), window));
        Assert.False(cooldown.IsCooling(serial, start.AddSeconds(8), window));
    }

    [Fact]
    public void IsCooling_DoesNotAffectOtherCharacters()
    {
        var cooldown = new CharacterCooldown();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        cooldown.Mark((Serial)1u, start);
        Assert.False(cooldown.IsCooling((Serial)2u, start, TimeSpan.FromSeconds(8)));
    }
}
