using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Mobiles;

namespace SosariaAI.Behaviour;

/// <summary>
/// Every live haggle with a person, one per character, every character inside a deal with
/// another character, and every deal a bystander answered on the bank floor
/// (<see cref="FloorDeal"/>), which no routine of either side ticks. A single timer ticks them
/// while any is open and stops when none is. World thread only.
/// </summary>
public static class TradeSessions
{
    public const int TickMilliseconds = 500;

    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(TickMilliseconds);
    private static readonly Dictionary<Serial, TradeSession> ByCharacter = new();
    private static readonly HashSet<Serial> InDeals = [];
    private static readonly List<TradeSession> TickScratch = [];
    private static readonly List<FloorDeal> FloorDeals = [];
    private static readonly List<FloorDeal> FloorScratch = [];
    private static TimerExecutionToken _timer;

    public static void Open(TradeSession session)
    {
        if (session?.Character == null)
        {
            return;
        }

        if (ByCharacter.TryGetValue(session.Character.Serial, out var old) && old != session)
        {
            old.End(null);
        }

        ByCharacter[session.Character.Serial] = session;
        StartTicking();
    }

    /// <summary>A bystander's answer on the bank floor ticks here until the deal is over.</summary>
    public static void OpenFloor(FloorDeal deal)
    {
        if (deal == null)
        {
            return;
        }

        FloorDeals.Add(deal);
        StartTicking();
    }

    public static void Close(TradeSession session)
    {
        if (session?.Character != null && ByCharacter.TryGetValue(session.Character.Serial, out var held) &&
            held == session)
        {
            ByCharacter.Remove(session.Character.Serial);
        }
    }

    /// <summary>The live haggle between this character and this person, or null.</summary>
    public static TradeSession Find(SosariaCharacter character, Mobile partner)
    {
        if (character == null || partner == null || !ByCharacter.TryGetValue(character.Serial, out var session))
        {
            return null;
        }

        return session.Partner == partner && !session.Ended ? session : null;
    }

    /// <summary>The nearest live haggle this person is in, or null.</summary>
    public static TradeSession FindFor(Mobile partner)
    {
        TradeSession nearest = null;
        var best = double.MaxValue;

        foreach (var session in ByCharacter.Values)
        {
            if (session.Partner != partner || session.Ended)
            {
                continue;
            }

            var distance = session.Character.GetDistanceToSqrt(partner);

            if (distance < best)
            {
                best = distance;
                nearest = session;
            }
        }

        return nearest;
    }

    /// <summary>True when the character is haggling with this person. Its chat brain stays out of the way.</summary>
    public static bool IsTrading(SosariaCharacter character, Mobile partner) => Find(character, partner) != null;

    /// <summary>True when the character is in any haggle or deal and cannot take another.</summary>
    public static bool IsBusy(SosariaCharacter character) =>
        character != null && (ByCharacter.ContainsKey(character.Serial) || InDeals.Contains(character.Serial));

    public static void JoinDeal(SosariaCharacter character) => InDeals.Add(character.Serial);

    public static void LeaveDeal(SosariaCharacter character) => InDeals.Remove(character.Serial);

    private static void Tick()
    {
        TickScratch.Clear();
        TickScratch.AddRange(ByCharacter.Values);
        var now = Core.Now;

        foreach (var session in TickScratch)
        {
            if (!session.Tick(now))
            {
                Close(session);
            }
        }

        FloorScratch.Clear();
        FloorScratch.AddRange(FloorDeals);

        foreach (var deal in FloorScratch)
        {
            if (!deal.Tick(now))
            {
                FloorDeals.Remove(deal);
            }
        }

        if (ByCharacter.Count == 0 && FloorDeals.Count == 0)
        {
            _timer.Cancel();
        }
    }

    private static void StartTicking()
    {
        if (!_timer.Running)
        {
            Timer.StartTimer(TickInterval, TickInterval, Tick, out _timer);
        }
    }
}
