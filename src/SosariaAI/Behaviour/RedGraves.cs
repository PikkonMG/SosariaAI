using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Social;

namespace SosariaAI.Behaviour;

/// <summary>
/// The places reds fell lately, read from the shard's news: every red put down is journaled
/// where it fell (<see cref="ShardEventType.RedKill"/>), and the reds hear of it as the blues
/// do. A red's run takes no hot spot it shuns (<see cref="RedGraveRules.Shuns"/>): one where it
/// or a gang mate fell lately, or one the blues hold. The news is read again at most every
/// <see cref="ReadEvery"/>, since every red's run looks at every spot.
/// </summary>
public static class RedGraves
{
    /// <summary>The news is read again at most this often.</summary>
    public static readonly TimeSpan ReadEvery = TimeSpan.FromSeconds(30);

    private static readonly List<Grave> Graves = [];
    private static DateTime _readAt;

    /// <summary>True when <paramref name="red"/> keeps off the ground round <paramref name="spot"/> now.</summary>
    public static bool Shuns(SosariaCharacter red, Point3D spot, DateTime now)
    {
        if (red == null)
        {
            return false;
        }

        Read(now);
        var redsFell = 0;
        var ownSideFell = false;

        for (var i = 0; i < Graves.Count; i++)
        {
            var grave = Graves[i];

            if (!FacetMaps.Same(grave.Facet, red.HomeFacet) || NavMetric.Chebyshev(grave.At, spot) > RedGraveRules.GraveTiles)
            {
                continue;
            }

            if (RedGraveRules.Fresh(grave.FellAt, now, RedGraveRules.HeldFor))
            {
                redsFell++;
            }

            ownSideFell |= RedGraveRules.Fresh(grave.FellAt, now, RedGraveRules.OwnGraveFor) && RedGang.SidesWith(red, grave.Red);
        }

        return RedGraveRules.Shuns(ownSideFell, redsFell);
    }

    // The red kills of the news still remembered, kept until the next read.
    private static void Read(DateTime now)
    {
        if (_readAt != default && now - _readAt < ReadEvery)
        {
            return;
        }

        _readAt = now;
        Graves.Clear();

        if (SosariaSettings.Journal is not { } journal)
        {
            return;
        }

        foreach (var news in journal.Snapshot())
        {
            if (news.Type == ShardEventType.RedKill && RedGraveRules.Fresh(news.At, now, RedGraveRules.Remembered))
            {
                Graves.Add(new Grave(news.Facet, new Point3D(news.X, news.Y, news.Z), news.Actor, news.At));
            }
        }
    }

    private readonly record struct Grave(string Facet, Point3D At, string Red, DateTime FellAt);
}
