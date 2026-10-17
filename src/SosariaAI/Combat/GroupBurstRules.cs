using System;

namespace SosariaAI.Combat;

/// <summary>
/// When a side's casters land their spells as one: 1999 groups had two or three mages burst one
/// called target with an Explosion and a Flamestrike inside the same second or two, so no heal
/// came between the blows. A call holds the tick its spells land at; a caster joins an open call
/// and times its own cast to it. Pure over ticks (<c>Core.TickCount</c>).
/// </summary>
public static class GroupBurstRules
{
    /// <summary>
    /// A call is struck off this long after its spells landed: late casts of that burst are in
    /// by then, and the next burst is a new call.
    /// </summary>
    public const long ExpireAfterMs = 2000;

    /// <summary>A call is open while its landing tick is still to come: a caster can still time its spell to it.</summary>
    public static bool IsOpen(long landAtTick, long now) => landAtTick - now > 0;

    /// <summary>A call is over <see cref="ExpireAfterMs"/> after its landing tick.</summary>
    public static bool IsExpired(long landAtTick, long now) => now - landAtTick >= ExpireAfterMs;

    /// <summary>
    /// The landing tick a side keeps when a caster joins with <paramref name="proposedTick"/>:
    /// the earlier of it and an open call's, else the proposed one.
    /// </summary>
    public static long Keep(bool open, long openTick, long proposedTick) =>
        open ? Math.Min(openTick, proposedTick) : proposedTick;
}
