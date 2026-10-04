using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;

namespace SosariaAI.Economy;

/// <summary>
/// Who buys what, and where they stood when counted: one line per buyer and type from the
/// buyer's own list. A seller asks it before the trip, so it never walks to a shop kind whose
/// counter takes none of its goods. Pure: the census fills it with world vendors, a test with
/// names.
/// </summary>
public sealed class BuyerBook<T>
{
    private readonly Dictionary<Type, List<(T Buyer, Point3D At)>> _byType = new();

    /// <summary>A book with nobody in it: no map, or before the first count.</summary>
    public static BuyerBook<T> Empty { get; } = new();

    /// <summary>Enters a buyer standing at <paramref name="at"/> that takes every type in <paramref name="buys"/>.</summary>
    public void Add(T buyer, Point3D at, IEnumerable<Type> buys)
    {
        foreach (var type in buys ?? [])
        {
            if (type == null)
            {
                continue;
            }

            if (!_byType.TryGetValue(type, out var buyers))
            {
                buyers = [];
                _byType[type] = buyers;
            }

            if (!buyers.Exists(line => EqualityComparer<T>.Default.Equals(line.Buyer, buyer)))
            {
                buyers.Add((buyer, at));
            }
        }
    }

    /// <summary>
    /// True when a buyer of <paramref name="type"/> stood within <paramref name="reach"/>
    /// tiles of <paramref name="from"/> and <paramref name="trades"/> says it still deals.
    /// </summary>
    public bool AnyBuys(Type type, Point3D from, int reach, Func<T, bool> trades)
    {
        if (type == null || !_byType.TryGetValue(type, out var buyers))
        {
            return false;
        }

        for (var i = 0; i < buyers.Count; i++)
        {
            if (NavMetric.Chebyshev(from, buyers[i].At) <= reach && trades?.Invoke(buyers[i].Buyer) != false)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Buyers of any of <paramref name="types"/> within <paramref name="reach"/> tiles of
    /// <paramref name="from"/>, nearest first, each once, and only those
    /// <paramref name="trades"/> accepts.
    /// </summary>
    public List<T> NearestBuying(IEnumerable<Type> types, Point3D from, int reach, Func<T, bool> trades)
    {
        var found = new List<(T Buyer, int Tiles)>();

        foreach (var type in types ?? [])
        {
            if (type == null || !_byType.TryGetValue(type, out var buyers))
            {
                continue;
            }

            for (var i = 0; i < buyers.Count; i++)
            {
                var (buyer, at) = buyers[i];
                var tiles = NavMetric.Chebyshev(from, at);

                if (tiles <= reach &&
                    !found.Exists(line => EqualityComparer<T>.Default.Equals(line.Buyer, buyer)) &&
                    trades?.Invoke(buyer) != false)
                {
                    found.Add((buyer, tiles));
                }
            }
        }

        found.Sort((a, b) => a.Tiles.CompareTo(b.Tiles));
        return found.ConvertAll(line => line.Buyer);
    }
}
