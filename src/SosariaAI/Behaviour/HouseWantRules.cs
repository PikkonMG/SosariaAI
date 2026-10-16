using System;

namespace SosariaAI.Behaviour;

public enum HouseSize
{
    Small,
    TwoStory,
    Tower
}

/// <summary>
/// Who wants a house of its own and which. Most players of 1999 never placed one: about one
/// in four wants a house, and most of those a small one; some save for a two-story house and
/// a few for a tower, at the architect's prices. The person's own seed decides, so the want
/// stays the same from boot to boot. Pure.
/// </summary>
public static class HouseWantRules
{
    /// <summary>One person in this many wants a house of its own.</summary>
    public const int WantEvery = 4;

    public const int Percent = 100;

    /// <summary>Of those who want a house, this share in a hundred saves for a tower.</summary>
    public const int TowerShare = 10;

    /// <summary>Of those who want a house, this share in a hundred saves for a two-story house.</summary>
    public const int TwoStoryShare = 30;

    /// <summary>The architect's price of a two-story wood-and-plaster house.</summary>
    public const int TwoStoryPrice = 192400;

    /// <summary>The architect's price of a tower.</summary>
    public const int TowerPrice = 433200;

    public const string SmallTarget = "a small house";
    public const string TwoStoryTarget = "a two-story house";
    public const string TowerTarget = "a tower";

    public static bool WantsHouse(int seed) => (seed & int.MaxValue) % WantEvery == 0;

    public static HouseSize SizeFor(int seed)
    {
        var roll = (seed & int.MaxValue) / WantEvery % Percent;
        return roll < TowerShare ? HouseSize.Tower : roll < TowerShare + TwoStoryShare ? HouseSize.TwoStory : HouseSize.Small;
    }

    /// <summary>What the house costs; a small house costs <paramref name="smallPrice"/>, the shard's <c>houseGold</c>.</summary>
    public static int Price(HouseSize size, int smallPrice) =>
        size switch
        {
            HouseSize.Tower => TowerPrice,
            HouseSize.TwoStory => TwoStoryPrice,
            _ => smallPrice
        };

    public static string Target(HouseSize size) =>
        size switch
        {
            HouseSize.Tower => TowerTarget,
            HouseSize.TwoStory => TwoStoryTarget,
            _ => SmallTarget
        };

    public static bool IsHouseTarget(string target) =>
        string.Equals(target, SmallTarget, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(target, TwoStoryTarget, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(target, TowerTarget, StringComparison.OrdinalIgnoreCase);

    /// <summary>The gold goal of a person that wants a house; null for one that wants none.</summary>
    public static Ambition? Goal(int seed, int smallPrice)
    {
        if (!WantsHouse(seed) || smallPrice <= 0)
        {
            return null;
        }

        var size = SizeFor(seed);
        return new Ambition(AmbitionKind.Gold, Target(size), Price(size, smallPrice), 0);
    }
}
