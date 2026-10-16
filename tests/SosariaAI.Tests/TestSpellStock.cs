using System;
using SosariaAI.Combat;

namespace SosariaAI.Tests;

/// <summary>Packs for the spell rules: every spell's reagents, or every spell's but a few.</summary>
internal static class TestSpellStock
{
    /// <summary>A pack that holds the reagents of every spell.</summary>
    internal static readonly SpellStock Every = Except();

    /// <summary>A pack that holds the reagents of every spell but <paramref name="left"/>.</summary>
    internal static SpellStock Except(params SpellKind[] left)
    {
        var stock = SpellStock.None;

        foreach (var kind in Enum.GetValues<SpellKind>())
        {
            if (Array.IndexOf(left, kind) < 0)
            {
                stock = stock.With(kind);
            }
        }

        return stock;
    }
}
