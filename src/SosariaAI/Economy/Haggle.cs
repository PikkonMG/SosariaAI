using System;

namespace SosariaAI.Economy;

/// <summary>Which end of the deal the character holds.</summary>
public enum HaggleSide
{
    Sells,
    Buys
}

/// <summary>How a person bargains: a greedy one gives little, a generous one meets you.</summary>
public enum HaggleTemper
{
    Hard,
    Fair,
    Soft
}

/// <summary>What the character says back to a number.</summary>
public enum HaggleMove
{
    /// <summary>The number works: the deal is at <see cref="HaggleStep.Price"/>.</summary>
    Accept,

    /// <summary>A new number, closer to theirs.</summary>
    Counter,

    /// <summary>The same number again: "5k is firm", "2k is my max".</summary>
    Firm,

    /// <summary>The character is done: too far apart, or it lost patience.</summary>
    WalkAway,

    /// <summary>A number so far off it is not worth an answer: "lol no".</summary>
    Insulted
}

public readonly record struct HaggleStep(HaggleMove Move, int Price);

/// <summary>
/// One haggle, seen from the character's side. The character holds a hidden limit (the
/// lowest it sells for, or the most it pays) and a standing number it last said. Each number
/// heard moves the standing number toward the other side by a share set by temper, a close
/// enough number is taken, a far one may end it, and after a few rounds the character either
/// meets its limit or walks. "Deal" locks the standing number. Pure.
/// </summary>
public sealed class Haggle
{
    public const int MaxRounds = 4;

    /// <summary>From this round a number past the limit is simply taken.</summary>
    public const int ClosingRound = 2;

    /// <summary>From this round the character goes all the way to its limit.</summary>
    public const int LastStandRound = 3;

    /// <summary>A seller offered under half its floor is insulted.</summary>
    public const int InsultDivisor = 2;

    /// <summary>A buyer asked more than this many times its ceiling does not bother.</summary>
    public const int SillyMultiple = 10;

    public const int PercentScale = 100;
    private const double MidpointShare = 0.5;

    private Haggle(HaggleSide side, int standing, int limit, HaggleTemper temper)
    {
        Side = side;
        Standing = standing;
        Limit = limit;
        Temper = temper;
    }

    public HaggleSide Side { get; }

    public HaggleTemper Temper { get; }

    /// <summary>The number the character said last.</summary>
    public int Standing { get; private set; }

    /// <summary>The hidden floor (selling) or ceiling (buying).</summary>
    public int Limit { get; }

    public int Round { get; private set; }

    /// <summary>The locked price, or 0 while the haggle is open.</summary>
    public int Agreed { get; private set; }

    public bool Ended { get; private set; }

    public bool IsOpen => Agreed == 0 && !Ended;

    public static HaggleTemper TemperOf(bool greedy, bool generous) =>
        greedy == generous ? HaggleTemper.Fair : greedy ? HaggleTemper.Hard : HaggleTemper.Soft;

    /// <summary>A seller asking <paramref name="asking"/>, with a floor its temper sets.</summary>
    public static Haggle Selling(int asking, HaggleTemper temper)
    {
        var ask = GoldWords.RoundSpoken(asking);
        var floor = Math.Min(ask, GoldWords.RoundSpoken((int)(ask * FloorShare(temper))));
        return new Haggle(HaggleSide.Sells, ask, floor, temper);
    }

    /// <summary>
    /// A buyer for goods worth <paramref name="value"/>. Its ceiling is set by temper and capped by
    /// the purse; it opens below the ceiling.
    /// </summary>
    public static Haggle Buying(int value, int purse, HaggleTemper temper)
    {
        var ceiling = Math.Max(0, Math.Min(purse, GoldWords.RoundSpoken((int)(value * CeilingShare(temper)))));
        var opening = Math.Min(ceiling, GoldWords.RoundSpoken((int)(ceiling * OpeningShare(temper))));
        return new Haggle(HaggleSide.Buys, opening, ceiling, temper);
    }

    /// <summary>A price that is not haggled: an order's deposit or its pickup. It is agreed from the start.</summary>
    public static Haggle Fixed(int price)
    {
        var ask = Math.Max(GoldWords.SmallestPrice, price);
        return new Haggle(HaggleSide.Sells, ask, ask, HaggleTemper.Hard) { Agreed = ask };
    }

    public static double FloorShare(HaggleTemper temper) =>
        temper switch
        {
            HaggleTemper.Hard => 0.85,
            HaggleTemper.Soft => 0.6,
            _ => 0.72
        };

    public static double CeilingShare(HaggleTemper temper) =>
        temper switch
        {
            HaggleTemper.Hard => 0.8,
            HaggleTemper.Soft => 1.1,
            _ => 0.95
        };

    public static double OpeningShare(HaggleTemper temper) =>
        temper switch
        {
            HaggleTemper.Hard => 0.55,
            HaggleTemper.Soft => 0.75,
            _ => 0.65
        };

    /// <summary>The share of the gap a round gives up; from the last stand, all of it.</summary>
    public static double Give(HaggleTemper temper, int round)
    {
        if (round >= LastStandRound)
        {
            return 1;
        }

        var first = round <= 1;

        return temper switch
        {
            HaggleTemper.Hard => first ? 0.3 : 0.5,
            HaggleTemper.Soft => first ? 0.6 : 0.85,
            _ => first ? 0.45 : 0.7
        };
    }

    /// <summary>Out of a hundred, per round, how likely a character is to give up on a number short of its limit.</summary>
    public static int WalkPercent(HaggleTemper temper) =>
        temper switch
        {
            HaggleTemper.Hard => 15,
            HaggleTemper.Soft => 5,
            _ => 10
        };

    /// <summary>True when a buyer would not even answer an asking price this far above its ceiling.</summary>
    public static bool IsSilly(int asking, int ceiling) => ceiling <= 0 || (long)asking > (long)ceiling * SillyMultiple;

    /// <summary>The other side named <paramref name="theirs"/>. <paramref name="roll"/> is 0..99.</summary>
    public HaggleStep Hear(int theirs, int roll)
    {
        if (!IsOpen || theirs <= 0)
        {
            return new HaggleStep(HaggleMove.Firm, Standing);
        }

        Round++;
        return Side == HaggleSide.Sells ? HearAsSeller(theirs, roll) : HearAsBuyer(theirs, roll);
    }

    /// <summary>The other side said "deal" to the standing number.</summary>
    public HaggleStep Agree()
    {
        if (!IsOpen)
        {
            return new HaggleStep(HaggleMove.Firm, Standing);
        }

        return Take(Standing);
    }

    public void End() => Ended = true;

    private HaggleStep HearAsSeller(int offer, int roll)
    {
        if (offer >= Standing)
        {
            return Take(Standing);
        }

        if (offer < Limit / InsultDivisor)
        {
            return Stop(HaggleMove.Insulted);
        }

        if (offer >= Limit)
        {
            if (Round >= ClosingRound || offer >= Midpoint(Standing, Limit))
            {
                return Take(offer);
            }

            var counter = GoldWords.RoundSpoken((int)(Standing - (Standing - offer) * Give(Temper, Round)));
            return counter <= offer ? Take(offer) : Move(counter);
        }

        if (Round >= MaxRounds || WalksAway(roll))
        {
            return Stop(HaggleMove.WalkAway);
        }

        var lower = Math.Max(Limit, GoldWords.RoundSpoken((int)(Standing - (Standing - Limit) * Give(Temper, Round))));
        return lower >= Standing ? new HaggleStep(HaggleMove.Firm, Standing) : Move(lower);
    }

    private HaggleStep HearAsBuyer(int asking, int roll)
    {
        if (asking <= Standing)
        {
            return Take(asking);
        }

        if (IsSilly(asking, Limit))
        {
            return Stop(HaggleMove.Insulted);
        }

        if (asking <= Limit)
        {
            if (Round >= ClosingRound || asking <= Midpoint(Standing, Limit))
            {
                return Take(asking);
            }

            var counter = GoldWords.RoundSpoken((int)(Standing + (asking - Standing) * Give(Temper, Round)));
            return counter >= asking ? Take(asking) : Move(counter);
        }

        if (Round >= MaxRounds || WalksAway(roll))
        {
            return Stop(HaggleMove.WalkAway);
        }

        var higher = Math.Min(Limit, GoldWords.RoundSpoken((int)(Standing + (Limit - Standing) * Give(Temper, Round))));
        return higher <= Standing ? new HaggleStep(HaggleMove.Firm, Standing) : Move(higher);
    }

    private bool WalksAway(int roll) => Math.Abs(roll % PercentScale) < WalkPercent(Temper) * Round;

    private HaggleStep Take(int price)
    {
        Agreed = price;
        Standing = price;
        return new HaggleStep(HaggleMove.Accept, price);
    }

    private HaggleStep Move(int price)
    {
        Standing = price;
        return new HaggleStep(HaggleMove.Counter, price);
    }

    private HaggleStep Stop(HaggleMove move)
    {
        Ended = true;
        return new HaggleStep(move, Standing);
    }

    private static int Midpoint(int a, int b) => (int)(a + (b - a) * MidpointShare);
}
