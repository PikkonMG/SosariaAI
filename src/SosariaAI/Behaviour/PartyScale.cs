using System;

namespace SosariaAI.Behaviour;

public static class PartyScale
{
    public const int MinPartySize = 3;
    public const int MaxPartySize = 5;
    /// <summary>
    /// Each this many people online allow one more group run at once: 1120 people carry
    /// eighteen, where one in 120 left ten parties in twenty minutes on a full shard.
    /// </summary>
    public const int PopulationPerHuntParty = 60;
    public const int MinHuntParties = 3;
    public const int FallenGraceMinutes = 3;

    /// <summary>Each this many people online add one seat to a group call, up to the cap.</summary>
    public const int PopulationPerExtraMember = 150;

    public static int HuntPartyCap(int population) =>
        Math.Max(MinHuntParties, population / PopulationPerHuntParty);

    public static int PartySize(int memberCount) =>
        Math.Clamp(memberCount, MinPartySize, MaxPartySize);

    /// <summary>How many a group call wants in all, leader included: 3 on a quiet shard, 5 on a busy one.</summary>
    public static int GroupSizeFor(int population) =>
        PartySize(MinPartySize + Math.Max(0, population) / PopulationPerExtraMember);
}
