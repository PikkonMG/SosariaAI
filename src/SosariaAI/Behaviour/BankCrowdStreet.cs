using System;
using Server;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Social;

namespace SosariaAI.Behaviour;

/// <summary>
/// The street life every bank grew: the beggar and the lost newbie. Each stands about with
/// its own chatter and now and then latches onto a real player passing by, trails a couple
/// of steps behind saying its line, and gives up after a while or when the player leaves
/// the bank floor. Only a person at a keyboard is trailed; characters leave each other be.
/// </summary>
public sealed class BankCrowdStreet : BankCrowdAct
{
    private readonly string _line;
    private readonly string _giveUpLine;
    private Mobile _player;
    private DateTime _trailSince;
    private DateTime _nextTrail;
    private DateTime _nextLine;

    public BankCrowdStreet(string line, string giveUpLine)
    {
        _line = line;
        _giveUpLine = giveUpLine;
    }

    public override bool Tick(DateTime now)
    {
        if (_player != null)
        {
            Trail(now);
            return true;
        }

        if (now >= _nextLine)
        {
            _nextLine = now + BankCrowdRules.StreetChatterGap(Roll());
            FaceNearest();
            Talk.Maybe(Member, _line, BankCrowdRules.StreetChatterPercent);
        }

        if (now >= _nextTrail && BankCrowdRules.Chance(Roll(), BankCrowdRules.StreetTrailPercent) &&
            NearestPlayer() is { } player)
        {
            _player = player;
            _trailSince = now;
            Talk.Say(Member, _line, new TalkSlots { Name = player.Name });
        }

        return true;
    }

    public override void End() => _player = null;

    protected override bool OnStart()
    {
        _nextLine = Core.Now + BankCrowdRules.StreetChatterGap(Roll());
        _nextTrail = Core.Now;
        return true;
    }

    private void Trail(DateTime now)
    {
        var player = _player;
        var gone = player.Deleted || !player.Alive || !People.Perceives(Member, player) || player.Map != Member.Map ||
                   BankCrowdRules.StopsTrailing(
                       NavMetric.Chebyshev(player.Location, Seat.Bank),
                       NavMetric.Chebyshev(player.Location, Member.Location),
                       now - _trailSince
                   );

        if (gone)
        {
            _player = null;
            _nextTrail = now + BankCrowdRules.StreetTrailRest;
            Member.Motor.Stop();
            Talk.Say(Member, _giveUpLine);
            return;
        }

        Member.Motor.MoveTo(player, BankCrowdRules.StreetTrailTiles);
    }

    private Mobile NearestPlayer()
    {
        Mobile nearest = null;
        var best = int.MaxValue;

        foreach (var mobile in Member.GetMobilesInRange(BankCrowdRules.StreetNoticeTiles))
        {
            if (!People.IsHuman(mobile) || !mobile.Alive || !People.Perceives(Member, mobile) || mobile.AccessLevel > AccessLevel.Player)
            {
                continue;
            }

            var tiles = NavMetric.Chebyshev(mobile.Location, Member.Location);

            if (tiles < best)
            {
                best = tiles;
                nearest = mobile;
            }
        }

        return nearest;
    }
}
