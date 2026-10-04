using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Combat;

namespace SosariaAI.Navigation;

public sealed class DestinationCatalog
{
    private readonly List<Destination> _all = [];
    private readonly Dictionary<string, Destination> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Destination>> _named = new(StringComparer.OrdinalIgnoreCase);

    public DestinationCatalog(IEnumerable<Destination> destinations)
    {
        if (destinations == null)
        {
            return;
        }

        foreach (var dest in destinations)
        {
            if (dest == null || string.IsNullOrWhiteSpace(dest.Name))
            {
                continue;
            }

            _all.Add(dest);
            _byName[dest.Name] = dest;
            AddNamed(dest.Name, dest);

            if (dest.Aliases == null)
            {
                continue;
            }

            for (var i = 0; i < dest.Aliases.Count; i++)
            {
                var alias = dest.Aliases[i];

                if (string.IsNullOrWhiteSpace(alias))
                {
                    continue;
                }

                AddNamed(alias, dest);

                if (!_byName.ContainsKey(alias))
                {
                    _byName[alias] = dest;
                }
            }
        }
    }

    public IReadOnlyList<Destination> All => _all;

    public Destination GetByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _byName.TryGetValue(name, out var found) ? found : null;
    }

    /// <summary>
    /// The destination a token names, nearest first. Many places share one alias: every
    /// inn answers to "tavern". Taking the first one sent Britain folk toward an inn on
    /// the far side of the world, and every visit to the inn failed.
    /// </summary>
    public Destination Resolve(string token, Point3D from)
    {
        if (TryParseKind(token, out var kind, out var role))
        {
            return Nearest(from, kind, role) ?? GetByName(token);
        }

        var named = NearestFirst(token, from, 1);
        return named.Count == 0 ? null : named[0];
    }

    /// <summary>
    /// As <see cref="Resolve(string, Point3D)"/>, but with <paramref name="avoidRedTown"/> a match
    /// in Buccaneer's Den is passed over while one stands anywhere else. A blue's shop trip from
    /// Magincia took the moongate to a Den shop, and the anti-PK fought reds there for an hour.
    /// </summary>
    public Destination Resolve(string token, Point3D from, bool avoidRedTown)
    {
        if (avoidRedTown)
        {
            var matches = NearestFirst(token, from, _all.Count);

            for (var i = 0; i < matches.Count; i++)
            {
                if (!PkRules.InBuccaneersDen(matches[i].Arrival.X, matches[i].Arrival.Y))
                {
                    return matches[i];
                }
            }
        }

        return Resolve(token, from);
    }

    /// <summary>
    /// Every destination the token names, nearest first, at most <paramref name="limit"/>.
    /// The nearest smith is not always one a character can reach, so callers try the next.
    /// </summary>
    public IReadOnlyList<Destination> NearestFirst(string token, Point3D from, int limit)
    {
        var found = new List<Destination>();

        if (!TryParseKind(token, out var kind, out var role))
        {
            if (!string.IsNullOrWhiteSpace(token) && _named.TryGetValue(token, out var named))
            {
                found.AddRange(named);
            }
        }
        else
        {
            for (var i = 0; i < _all.Count; i++)
            {
                var dest = _all[i];

                if (Matches(dest, kind, role))
                {
                    found.Add(dest);
                }
            }
        }

        found.Sort((a, b) => NavMetric.Chebyshev(from, a.Arrival).CompareTo(NavMetric.Chebyshev(from, b.Arrival)));

        if (found.Count > limit)
        {
            found.RemoveRange(limit, found.Count - limit);
        }

        return found;
    }

    public Destination Nearest(Point3D from, DestinationKind kind, string role = null) =>
        Nearest(from, kind, role, static _ => true);

    /// <summary>
    /// The nearest bank this person may use: any for a blue, and for a murderer only one
    /// the guards leave it, the Buccaneer's Den teller (<see cref="PkRules.MayBankAt"/>).
    /// </summary>
    public Destination NearestBank(Point3D from, bool murderer) =>
        Nearest(from, DestinationKind.Bank, role: null, dest => PkRules.MayBankAt(murderer, dest.Arrival.X, dest.Arrival.Y));

    private Destination Nearest(Point3D from, DestinationKind kind, string role, Func<Destination, bool> allowed)
    {
        Destination best = null;
        var bestDist = int.MaxValue;

        for (var i = 0; i < _all.Count; i++)
        {
            var dest = _all[i];

            if (!Matches(dest, kind, role) || !allowed(dest))
            {
                continue;
            }

            var dist = NavMetric.Chebyshev(from, dest.Arrival);

            if (dist < bestDist)
            {
                bestDist = dist;
                best = dest;
            }
        }

        return best;
    }

    private static bool Matches(Destination dest, DestinationKind kind, string role)
    {
        if (dest.ParsedKind != kind)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            return true;
        }

        if (!string.Equals(dest.Role, role, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return kind != DestinationKind.Vendor ||
               !role.Equals(ForgeShop.SmithRole, StringComparison.OrdinalIgnoreCase) ||
               ForgeShop.IsForgeShop(dest.Name, dest.Role);
    }

    private void AddNamed(string name, Destination dest)
    {
        if (!_named.TryGetValue(name, out var list))
        {
            list = [];
            _named[name] = list;
        }

        if (!list.Contains(dest))
        {
            list.Add(dest);
        }
    }

    public const string VendorTokenPrefix = "vendor:";

    /// <summary>A forge: the token for <see cref="ForgeShop.SmithRole"/>, beside "blacksmith" and "forge".</summary>
    public const string SmithToken = "smith";

    public const string CarpenterRole = "Carpenter";
    public const string FishermanToken = "fisherman";
    public const string FishermanRole = "Fisherman";
    public const string GraveyardToken = "graveyard";
    public const string GraveyardRole = "Graveyard";
    public const string DespiseToken = "despise";
    public const string DespiseRole = "Despise";

    private const string BankToken = "bank";
    private const string HealerToken = "healer";
    private const string ShrineToken = "shrine";
    private const string AnkhToken = "ankh";

    public static bool TryParseKind(string token, out DestinationKind kind, out string role)
    {
        kind = DestinationKind.Bank;
        role = null;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        // "vendor:Tailor" names any shop by the role the world generator recorded.
        if (token.StartsWith(VendorTokenPrefix, StringComparison.OrdinalIgnoreCase) &&
            token.Length > VendorTokenPrefix.Length)
        {
            kind = DestinationKind.Vendor;
            role = token[VendorTokenPrefix.Length..];
            return true;
        }

        if (IsToken(token, BankToken))
        {
            kind = DestinationKind.Bank;
            return true;
        }

        if (IsToken(token, ShopFinder.SmithToken) || IsToken(token, SmithToken) || IsToken(token, ForgeShop.ForgeToken))
        {
            kind = DestinationKind.Vendor;
            role = ForgeShop.SmithRole;
            return true;
        }

        if (IsToken(token, ShopFinder.CarpenterToken))
        {
            kind = DestinationKind.Vendor;
            role = CarpenterRole;
            return true;
        }

        if (IsToken(token, FishermanToken))
        {
            kind = DestinationKind.Vendor;
            role = FishermanRole;
            return true;
        }

        if (IsToken(token, GraveyardToken))
        {
            kind = DestinationKind.Hunt;
            role = GraveyardRole;
            return true;
        }

        if (IsToken(token, DespiseToken))
        {
            kind = DestinationKind.Dungeon;
            role = DespiseRole;
            return true;
        }

        if (IsToken(token, HealerToken))
        {
            kind = DestinationKind.Healer;
            return true;
        }

        if (IsToken(token, ShrineToken) || IsToken(token, AnkhToken))
        {
            kind = DestinationKind.Shrine;
            return true;
        }

        return false;
    }

    private static bool IsToken(string token, string word) => token.Equals(word, StringComparison.OrdinalIgnoreCase);
}
