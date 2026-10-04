using System;

namespace SosariaAI.Memory;

/// <summary>
/// How long long-term memories last. Big moments (deaths, red kills, rescues, house buys, first
/// kills, dungeon runs with company) are kept forever. Small moments fade after
/// <see cref="SmallFadeDays"/>; each telling of a small adventure adds <see cref="TellExtensionDays"/>.
/// A bond that only came from greeting or chatting fades after <see cref="SmallFadeDays"/>
/// without a meeting.
/// </summary>
public static class MemoryFade
{
    /// <summary>Days a small moment lasts without a telling or a meeting.</summary>
    public const int SmallFadeDays = 30;

    /// <summary>Days each telling adds before a small adventure fades.</summary>
    public const int TellExtensionDays = 7;

    /// <summary>An adventure this heavy or heavier is kept forever.</summary>
    public const int KeepForeverWeight = 20;

    public const int DeathWeight = 50;
    public const int RedKillWeight = 45;
    public const int FirstKillWeight = 40;
    public const int RescueWeight = 35;
    public const int HouseWeight = 30;
    public const int DungeonWithCompanyWeight = KeepForeverWeight;
    public const int SoloDungeonWeight = 8;
    public const int DuelWeight = 6;
    public const int HuntWeight = 4;
    public const int OutingWeight = 1;

    /// <summary>A dungeon run with at least this many people on one side is a run with company.</summary>
    public const int CompanySize = 2;

    /// <summary>
    /// How big the moment was. <paramref name="sideSize"/> is the largest number of members on
    /// one side.
    /// </summary>
    public static int WeightOf(string kind, int sideSize) =>
        kind switch
        {
            AdventureKinds.Death => DeathWeight,
            AdventureKinds.RedKill => RedKillWeight,
            AdventureKinds.FirstKill => FirstKillWeight,
            AdventureKinds.Rescue => RescueWeight,
            AdventureKinds.House => HouseWeight,
            AdventureKinds.Dungeon => sideSize >= CompanySize ? DungeonWithCompanyWeight : SoloDungeonWeight,
            AdventureKinds.Duel => DuelWeight,
            AdventureKinds.Hunt => HuntWeight,
            _ => OutingWeight
        };

    public static bool KeptForever(int weight) => weight >= KeepForeverWeight;

    /// <summary>True when a small adventure has outlived its time, told tellings included.</summary>
    public static bool AdventureFades(int weight, DateTime endedAt, int toldCount, DateTime now) =>
        !KeptForever(weight) &&
        now - endedAt > TimeSpan.FromDays(SmallFadeDays + (long)Math.Max(0, toldCount) * TellExtensionDays);

    /// <summary>
    /// True when a bond came only from greeting or chatting (no shared adventure, neither warm
    /// nor cold) and the two have not met for <see cref="SmallFadeDays"/>.
    /// </summary>
    public static bool BondFades(int score, int sharedCount, DateTime lastSeenAt, DateTime now) =>
        sharedCount == 0 &&
        !BondRules.IsWarm(score) &&
        !BondRules.IsCold(score) &&
        now - lastSeenAt > TimeSpan.FromDays(SmallFadeDays);
}
