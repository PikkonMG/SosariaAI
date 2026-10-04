using System.Collections.Generic;
using Server;
using SosariaAI.Combat;
using SosariaAI.Spawning;

namespace SosariaAI.Behaviour;

/// <summary>
/// Loop-side snapshot. Built without a model call.
/// </summary>
public sealed record Situation
{
    public NeedsSnapshot Needs { get; init; } = new();

    public Point3D Location { get; init; }

    public bool InTownRegion { get; init; }

    public bool LastWalkFailed { get; init; }

    public bool IsGhost { get; init; }

    public bool InCombat { get; init; }

    public CharacterRole Role { get; init; } = CharacterRole.Worker;

    /// <summary>The pack or the pack beast holds harvest a live shop in reach buys.</summary>
    public bool HasHarvestGoods { get; init; }

    /// <summary>The pack holds looted gear or trinkets a live shop in reach buys: the take from a fight.</summary>
    public bool HasLootGoods { get; init; }

    /// <summary>Anything in the pack a live shop in reach pays for: the harvest or the take from a fight.</summary>
    public bool HasSellGoods => HasHarvestGoods || HasLootGoods;

    public string CurrentActionId { get; init; }

    public IReadOnlyList<string> RecentSkillKinds { get; init; }

    public Goal CurrentGoal { get; init; }

    public bool MustFlee { get; init; }

    /// <summary>Ran from danger too often to walk back to the party.</summary>
    public bool KeepsAwayFromParty { get; init; }

    public int RecentRuns { get; init; }

    /// <summary>Places the character recently ran from. Trips into them keep failing.</summary>
    public IReadOnlyList<Point3D> DangerSpots { get; init; }

    /// <summary>Places the router proved unreachable. Trips to them are refused.</summary>
    public IReadOnlyList<Point3D> UnreachableGoals { get; init; }

    public string BlockedActionId { get; init; }

    public int BlockedCount { get; init; }

    /// <summary>Skills cooling down after repeated failure, or barred near where they just failed.</summary>
    public IReadOnlyList<string> BlockedSkillKinds { get; init; }

    /// <summary>Skills with nothing here to use them on: no beast to tame, no one to beg from, no heat to cook at.</summary>
    public IReadOnlyList<string> UnmetSkillKinds { get; init; }

    public int Gold { get; init; }

    public bool HasHouse { get; init; }

    public bool CanBuyHouse { get; init; }

    /// <summary>The house it owns has worn far enough that it goes back to look in on it (see <see cref="HouseRules.VisitDue"/>).</summary>
    public bool HouseDue { get; init; }

    public DispositionKind Disposition { get; init; } = DispositionKind.Neutral;

    public bool BeyondLeash { get; init; }

    public bool AvoidHuntPlace { get; init; }

    public bool CanUpgradeGear { get; init; }

    /// <summary>Not riding, and an owned mount stands in reach to climb onto.</summary>
    public bool CanMount { get; init; }

    /// <summary>Not riding, no owned mount near, a free follower slot, and the price of a horse.</summary>
    public bool CanBuyMount { get; init; }

    public int DistanceFromHome { get; init; }

    public bool HasWeapon { get; init; }

    /// <summary>A bank trip would do nothing: no goods, no gold, no arrows, no party trip to end.</summary>
    public bool NothingToBank { get; init; }

    public int FailedGoalCount { get; init; }

    /// <summary>The skill a Skill ambition names, or null for any other ambition.</summary>
    public string AmbitionSkill { get; init; }

    public bool HasBoat { get; init; }

    public bool HasVendor { get; init; }

    /// <summary>The vendor it owns has pack goods to put up or takings to collect (see <see cref="Skills.PlayerVendorRules.HasErrand"/>).</summary>
    public bool VendorErrand { get; init; }

    /// <summary>
    /// A house owner goes home to its vendor: to place one when it has none, or to stock it and
    /// collect. Owners never placed a vendor in a house they already had, so no vendor stood.
    /// </summary>
    public bool VendorVisitDue => HasHouse && (!HasVendor || VendorErrand);

    public bool DiedRecently { get; init; }

    public string FriendName { get; init; }

    public string AvoidName { get; init; }

    public bool HasTreasureMap { get; init; }

    public bool HasPkReport { get; init; }

    /// <summary>How welcome the person's hunt ground is after recent murders there: 1 is calm.</summary>
    public double PlaceDanger { get; init; } = 1.0;

    /// <summary>This person is in a real party whose leader it should walk with.</summary>
    public bool FollowsPartyLeader { get; init; }

    /// <summary>
    /// The authored crew this person belongs to broke up after its trip, and its leader has not
    /// called it together again (see <see cref="Party.Disband"/>).
    /// </summary>
    public bool CrewDisbanded { get; init; }

    /// <summary>The nearest bank crowd has a role this person fits and room for it.</summary>
    public bool BankCrowdOpen { get; init; }

    /// <summary>
    /// A supply a shop sells is below what the build needs (arrows, bandages, reagents or runes)
    /// and a stocked shop in reach sells it at a price the person can pay. One who cannot refill
    /// goes out with what it has: 408 of 500 dungeon stays of one run ended at the door with
    /// "its supplies ran low", the fighters too poor for the healer's twenty bandages.
    /// </summary>
    public bool SuppliesLow { get; init; }

    /// <summary>A red lost its combat kit or its weapon and can re-arm in the Den (see <see cref="SosariaAI.Combat.SpareKitRules.MustReArm"/>).</summary>
    public bool MustReArm { get; init; }

    /// <summary>
    /// A red whose next run would end at its first look: its pack is heavy with loot or its
    /// supplies ran short (see <see cref="RedGangRunRules.WhyNotSetOut"/>).
    /// </summary>
    public bool MustRestock { get; init; }

    /// <summary>
    /// A red that must go back to the Den's bank and shops before it rides out: it lost its
    /// kit (<see cref="MustReArm"/>) or its run would turn back at once (<see cref="MustRestock"/>).
    /// </summary>
    public bool GoesToDenFirst => MustReArm || MustRestock;

    /// <summary>
    /// A blue in Buccaneer's Den with no fight there: not on a Den raid, a posse or a PK
    /// hunter's run (<see cref="PartyRoads.RidesAgainstDen"/>). It goes home before anything
    /// else: blues who woke in the Den as ex-reds stood at its bank and tavern for an hour.
    /// </summary>
    public bool LeavesDen { get; init; }

    /// <summary>The rules keep the next job: a red goes back to the Den first, or a blue leaves it.</summary>
    public bool DenTripFirst => GoesToDenFirst || LeavesDen;

    /// <summary>One of the few blue fighters who log in to hunt reds (<see cref="PkHunterRules"/>).</summary>
    public bool IsPkHunter { get; init; }

    /// <summary>
    /// The person is short of something a shop in reach sells (a supply, a harvest tool or a
    /// kit piece) and carries gold to pay. A shopping trip is offered only then.
    /// </summary>
    public bool HasShoppingErrand { get; init; }

    /// <summary>A worker lost the axe, pick or pole its trade cannot work without.</summary>
    public bool NeedsTool { get; init; }

    /// <summary>A tamer holds too little of the food its pets eat (<see cref="SosariaAI.Skills.PetPantry.IsLow"/>).</summary>
    public bool NeedsPetFood { get; init; }

    /// <summary>Mana left, from 0 to 1.</summary>
    public double ManaFraction { get; init; } = 1.0;

    /// <summary>The person casts spells, so its mana matters.</summary>
    public bool IsCaster { get; init; }

    /// <summary>The person's leanings toward banking, adventure, travel, craft and idling.</summary>
    public ActivityTendencies Tendencies { get; init; }

    /// <summary>The station trade a crafter lives by, as its skill kind ("Smith", "Tailor"), or null.</summary>
    public string CraftTrade { get; init; }

    /// <summary>The person stands in a dungeon region.</summary>
    public bool InDungeon { get; init; }

    /// <summary>The shard's pull toward dungeon runs (<see cref="DungeonShareRules"/>): 1 is neutral.</summary>
    public double DungeonDemand { get; init; } = DungeonShareRules.Neutral;
}
