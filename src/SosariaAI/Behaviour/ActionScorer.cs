using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>
/// Scores catalog actions from a situation and a goal. Free. No model.
/// </summary>
public static class ActionScorer
{
    public const int TopCount = 3;
    public const double IneligibleScore = NeedsBrain.UnmetPowerScore;
    public const double AtPlaceBoost = 2.0;
    public const double AwayFromPlaceCut = 0.2;
    public const double GoalMismatchCut = 0.4;
    public const double TownWalkPackedBoost = 2.5;
    public const double SameActionCut = 0.2;
    public const double RecentActionCut = 0.2;
    public const double SameFamilyCut = 0.1;
    public const double HouseBoost = 8.0;

    /// <summary>
    /// Far from home and not on a trip, going home is the soft pull of the leash. A job
    /// step still outscores it: the leash never stops a trip the job is making.
    /// </summary>
    public const double HomeLeashBoost = 4.0;

    /// <summary>A caster low on mana sits down to meditate before its next job, as mages did between fights.</summary>
    public const double LowManaBoost = 4.0;

    /// <summary>
    /// A crafter's own trade outscores a wander or a walk: a smith picked "wander" at 1.16 over
    /// "work the forge" at 0.71 and the smithy stood empty.
    /// </summary>
    public const double CrafterTradeBoost = 2.0;

    /// <summary>Above a plan step, so a party member leaves its errand to walk with the group.</summary>
    public const double PartyFollowScore = GoalPlanRules.PlanBoost * 2;

    /// <summary>The reason an authored crew's follow step waits while that crew is broken up.</summary>
    public const string CrewDisbandedWhy = "the crew has broken up";
    public const int RecentSkillLimit = 2;

    public static ScoreResult Rank(
        IReadOnlyList<ActionCandidate> candidates,
        Situation situation,
        Goal goal,
        int seed,
        DestinationCatalog catalog = null,
        GoalPlan plan = null
    )
    {
        var needs = situation?.Needs ?? new NeedsSnapshot();
        var ranked = new List<ScoredAction>();

        if (candidates != null)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                ranked.Add(ScoreOne(candidates[i], situation, goal, needs, seed, catalog, plan));
            }
        }

        ranked.Sort((left, right) => right.Score.CompareTo(left.Score));
        var winner = ranked.Count == 0 || !IsEligible(ranked[0])
            ? default
            : ranked[0];

        return new ScoreResult
        {
            Goal = goal,
            Ranked = ranked,
            Winner = winner,
            WinnerReason = Reason(winner, ranked.Count > 1 ? ranked[1] : default, goal, situation, plan),
            Plan = plan
        };
    }

    /// <summary>An action the rules forbid never wins, even when every action is forbidden.</summary>
    public static bool IsEligible(ScoredAction action) => action.Score > IneligibleScore;

    /// <summary>
    /// Why this action cannot run now, or null when it can. The model plan path commits
    /// steps without scoring, so it checks here first: a plan that says "go home" while
    /// the character is already home would run it every second forever.
    /// </summary>
    public static string Unavailable(
        ActionCandidate candidate,
        Situation situation,
        DestinationCatalog catalog,
        Goal? goal = null
    ) =>
        WhyIneligible(
            candidate,
            situation,
            goal ?? situation?.CurrentGoal ?? default,
            situation?.Needs ?? new NeedsSnapshot(),
            catalog
        );

    public static string Reason(
        ScoredAction winner,
        ScoredAction second,
        Goal goal,
        Situation situation,
        GoalPlan plan = null
    )
    {
        if (string.IsNullOrWhiteSpace(winner.SkillKind))
        {
            return "no action";
        }

        var phrase = ActionDescriptions.Phrase(winner.SkillKind, winner.Id.Value);

        if (GoalPlanRules.MatchesCurrent(plan, winner.SkillKind))
        {
            return $"{phrase} because {plan.CurrentWhy}";
        }

        if (situation?.MustFlee == true)
        {
            return $"{phrase} because a threat is in sight";
        }

        if (situation?.Needs != null && situation.Needs.HitsFraction < NeedsBrain.HurtHitsFraction)
        {
            return $"{phrase} because hits are low";
        }

        if (winner.SkillKind == SkillKinds.Meditate && IsLowOnMana(situation))
        {
            return $"{phrase} because mana is low";
        }

        if (winner.SkillKind == SkillKinds.House)
        {
            return situation?.HouseDue == true
                ? $"{phrase} because its house needs a visit"
                : $"{phrase} because it can afford a house";
        }

        if (situation?.HasSellGoods == true && situation.InTownRegion &&
            NeedsBrain.FamilyOf(winner.RoutineId) == RoutineFamily.Trade)
        {
            return $"{phrase} because the pack has goods to sell in town";
        }

        if (situation?.HasSellGoods == true && !situation.InTownRegion &&
            ActionPlaceRules.IsTownWalk(new SkillStepDefinition { Skill = winner.SkillKind, Target = default }))
        {
            return $"{phrase} because goods must go to town to sell";
        }

        if (situation?.LastWalkFailed == true)
        {
            return $"{phrase} because the last walk failed";
        }

        if (second.Score > 0)
        {
            var other = ActionDescriptions.Phrase(second.SkillKind, second.Id.Value);
            return $"{phrase} scored {winner.Score:0.00} over {other} at {second.Score:0.00} for {GoalRules.Describe(goal)}";
        }

        return $"{phrase} for {GoalRules.Describe(goal)}";
    }

    private static ScoredAction ScoreOne(
        ActionCandidate candidate,
        Situation situation,
        Goal goal,
        NeedsSnapshot needs,
        int seed,
        DestinationCatalog catalog,
        GoalPlan plan
    )
    {
        var why = "ok";
        var ineligible = WhyIneligible(candidate, situation, goal, needs, catalog);

        if (ineligible != null)
        {
            return new ScoredAction(candidate.Id, candidate.SkillKind, candidate.RoutineId, IneligibleScore, ineligible);
        }

        // Staying a while belongs to a job that just arrived somewhere. Picked on its own
        // it is standing about for no reason.
        if (candidate.SkillKind == SkillKinds.Arrive && !GoalPlanRules.MatchesCurrent(plan, candidate.SkillKind))
        {
            return new ScoredAction(candidate.Id, candidate.SkillKind, candidate.RoutineId, IneligibleScore, "not arriving anywhere");
        }

        var family = FamilyOf(candidate);
        var score = NeedsBrain.Score(family, needs);
        var keepTrade = KeepTradeFamily(candidate, situation, goal);
        var formingCrew = IsPartyFormingCrew(family, needs);
        var atPlace = ActionPlaceRules.IsAt(
            candidate.Step,
            situation?.Location ?? Point3D.Zero,
            ActionPlaceRules.AtPlaceRange
        );

        if (!MatchesGoal(family, candidate.SkillKind, goal.Kind) ||
            IsPartyFormingTownWalkAtPlace(formingCrew, candidate, atPlace))
        {
            score *= GoalMismatchCut;
            why = "goal mismatch";
        }

        if (ActionPlaceRules.IsWorkSite(candidate.Step))
        {
            if (atPlace)
            {
                score += AtPlaceBoost;
                why = "at the work place";
            }
            else if (!formingCrew)
            {
                score *= AwayFromPlaceCut;
                why = "not at the work place";
            }
        }

        if (ShouldBoostPackedTownWalk(candidate, situation, goal))
        {
            score += TownWalkPackedBoost;
            why = "carry goods to town";
        }

        if (ShouldBoostPackedSellInTown(candidate, situation, goal))
        {
            score += TownWalkPackedBoost;
            why = "sell in town";
        }

        if (candidate.SkillKind.Equals(SkillKinds.House, StringComparison.OrdinalIgnoreCase) &&
            (situation?.HouseDue == true || situation?.CanBuyHouse == true))
        {
            score += HouseBoost;
            why = situation.HouseDue ? "house needs a visit" : "can afford a house";
        }

        if (candidate.SkillKind == SkillKinds.GoHome &&
            situation?.BeyondLeash == true &&
            !JobRules.IsTravel(goal.Target))
        {
            score += HomeLeashBoost;
            why = "far from home";
        }

        if (candidate.SkillKind == SkillKinds.GoHome && situation?.LeavesDen == true)
        {
            score += HomeLeashBoost;
            why = GoalPlanRules.LeaveDenPurpose;
        }

        if (candidate.SkillKind == SkillKinds.Meditate)
        {
            score += LowManaBoost;
            why = "mana is low";
        }

        if (IsOwnTrade(candidate, situation))
        {
            score += CrafterTradeBoost;
            why = "the trade it lives by";
        }

        if (IsSameFamily(candidate, situation?.CurrentActionId) && !keepTrade)
        {
            score *= SameFamilyCut;
            why = "same kind of action";
        }

        if (!keepTrade && !formingCrew)
        {
            if (IsSameAction(candidate, situation?.CurrentActionId))
            {
                score *= SameActionCut;
                why = "just finished this";
            }
            else if (IsListedSkill(candidate.SkillKind, situation?.RecentSkillKinds))
            {
                score *= RecentActionCut;
                why = "recently did this";
            }
        }

        if (GoalPlanRules.MatchesCurrent(plan, candidate.SkillKind))
        {
            score += GoalPlanRules.PlanBoost;
            why = plan.CurrentWhy;
        }
        else if (plan is { IsComplete: false } &&
                 !GoalPlanRules.IsInterrupt(situation) &&
                 !GoalPlanRules.ServesPlan(plan, candidate))
        {
            score *= GoalPlanRules.OffPlanCut;
            why = "off the current plan";
        }

        if (candidate.SkillKind == SkillKinds.Dungeon)
        {
            score = DungeonScore(score, situation, needs);

            if (situation?.InDungeon == true)
            {
                why = "fight on in the dungeon";
            }
        }

        if (situation?.DiedRecently == true &&
            candidate.SkillKind is SkillKinds.Hunt or SkillKinds.Dungeon)
        {
            score *= MemoryChoiceRules.HuntCut;
            why = "died recently";
        }

        if (!string.IsNullOrWhiteSpace(situation?.AvoidName) &&
            candidate.SkillKind == SkillKinds.Follow)
        {
            score *= MemoryChoiceRules.EnemyFollowCut;
            why = "will not travel with a foe";
        }

        if (!string.IsNullOrWhiteSpace(situation?.FriendName) &&
            candidate.SkillKind == SkillKinds.Follow)
        {
            score += MemoryChoiceRules.FriendFollowBoost;
            why = "join a friend";
        }

        // A member of a real party goes where its leader goes, a human leader included.
        if (situation?.FollowsPartyLeader == true && ActionCatalog.IsPartyFollow(candidate))
        {
            score = Math.Max(score, PartyFollowScore);
            why = "follow the party leader";
        }

        score += NeedsBrain.Jitter(seed, candidate.Id.Value);
        return new ScoredAction(candidate.Id, candidate.SkillKind, candidate.RoutineId, score, why);
    }

    /// <summary>
    /// A delve scaled by the shard's pull toward the dungeons, and raised for a well-stocked,
    /// healthy fighter who stands in one, so the run a restart put it back in goes on.
    /// </summary>
    public static double DungeonScore(double score, Situation situation, NeedsSnapshot needs)
    {
        var pulled = score * (situation?.DungeonDemand ?? DungeonShareRules.Neutral);
        var stays = DungeonShareRules.StaysUnderground(
            situation?.InDungeon == true,
            (needs?.HitsFraction ?? 1) >= NeedsBrain.HealthyHitsFraction,
            situation?.SuppliesLow == true
        );

        return stays ? pulled + DungeonShareRules.InDungeonScoreBoost : pulled;
    }

    private static string WhyIneligible(
        ActionCandidate candidate,
        Situation situation,
        Goal goal,
        NeedsSnapshot needs,
        DestinationCatalog catalog
    )
    {
        // A flee cooling down after repeated failure is as blocked as one that ran too often:
        // with only the threat rule left, every action was barred.
        var fleeBlocked = FleeRules.FleeIsBlocked(situation?.BlockedActionId, situation?.BlockedCount ?? 0) ||
                          FleeRules.FledEnough(situation?.RecentRuns ?? 0) ||
                          IsListedSkill(SkillKinds.Flee, situation?.BlockedSkillKinds);

        if (situation?.MustFlee == true && candidate.SkillKind != SkillKinds.Flee)
        {
            if (!fleeBlocked || !FleeRules.IsEscapeWhenFleeBlocked(candidate.SkillKind))
            {
                return "a threat is in sight";
            }
        }

        if (situation?.MustFlee != true && candidate.SkillKind == SkillKinds.Flee)
        {
            return "no threat";
        }

        if (candidate.SkillKind == SkillKinds.Flee && FleeRules.FledEnough(situation?.RecentRuns ?? 0))
        {
            return "already escaped repeatedly";
        }

        if (ActionCatalog.IsPartyFollow(candidate) && situation?.FollowsPartyLeader != true)
        {
            return "no party leader to follow";
        }

        // A crew that broke up has no trip to follow: the step ends Done the moment it starts.
        // Tam walked with the disbanded graveyard crew 99 times in four minutes on one tile.
        if (situation?.CrewDisbanded == true && candidate.SkillKind == SkillKinds.Follow && !ActionCatalog.IsPartyFollow(candidate))
        {
            return CrewDisbandedWhy;
        }

        if (situation?.KeepsAwayFromParty == true && candidate.SkillKind == SkillKinds.Follow)
        {
            return "ran from the party's danger too often";
        }

        var goHomeAfterRuns = FleeRules.ShouldGoHomeAfterRuns(
            situation?.InTownRegion == true,
            situation?.RecentRuns ?? 0,
            situation?.Needs?.HitsFraction ?? Vitals.FullHits,
            situation?.SuppliesLow == true
        );

        if (goHomeAfterRuns &&
            candidate.SkillKind != SkillKinds.GoHome &&
            candidate.SkillKind != SkillKinds.Flee)
        {
            return "ran from danger too often";
        }

        // The leash is a soft pull for idling, not a wall: trips to other towns, dungeons
        // and hunt grounds go on. A person in another town idles where it stands; out in the
        // wild far from home it walks back before it idles.
        if (situation?.BeyondLeash == true && situation.InTownRegion != true && IdlesNearHome(candidate.SkillKind))
        {
            return "idles near home only";
        }

        if (candidate.SkillKind == SkillKinds.GoHome &&
            !NeedsWalkHome(situation, goal, goHomeAfterRuns, fleeBlocked))
        {
            return situation?.InTownRegion == true && situation.BeyondLeash
                ? "stays in the town it is in"
                : "already near home";
        }

        if (candidate.SkillKind == SkillKinds.Travel && !JobRules.IsTravel(goal.Target))
        {
            return "not on a trip";
        }

        if (candidate.SkillKind == SkillKinds.Travel &&
            ActionPlaceRules.IsAt(candidate.Step, situation?.Location ?? Point3D.Zero, ActionPlaceRules.AtPlaceRange))
        {
            return "already there";
        }

        if (goal.Kind == GoalKind.Recover &&
            (FamilyOf(candidate) is RoutineFamily.Hunt or RoutineFamily.Dungeon or RoutineFamily.Party ||
             IsRedRunOut(candidate, situation)))
        {
            return "recovering";
        }

        if (situation?.AvoidHuntPlace == true &&
            candidate.SkillKind is SkillKinds.Hunt or SkillKinds.Dungeon)
        {
            return "died there today";
        }

        if (candidate.SkillKind.Equals(SkillKinds.UpgradeGear, StringComparison.OrdinalIgnoreCase) &&
            situation?.CanUpgradeGear != true)
        {
            return "nothing to buy";
        }

        // A shopping trip with nothing to buy, no shop in reach that sells it, or no gold in
        // the pack walked to the counter and failed there: six hundred times in half an hour.
        if (candidate.SkillKind == SkillKinds.VendorBuy && situation?.HasShoppingErrand != true)
        {
            return "nothing to buy in reach";
        }

        if (candidate.SkillKind == SkillKinds.BankCrowd && situation?.BankCrowdOpen != true)
        {
            return "no place in the bank crowd";
        }

        if (candidate.SkillKind == SkillKinds.Meditate && !IsLowOnMana(situation))
        {
            return "mana is fine";
        }

        // A worker runs from any real monster, so a delve ends at the first one on the road.
        if (candidate.SkillKind == SkillKinds.Dungeon && situation?.Role == CharacterRole.Worker)
        {
            return "a worker does not delve";
        }

        if (candidate.SkillKind == SkillKinds.Mount && situation?.CanMount != true)
        {
            return "no mount in reach";
        }

        if (candidate.SkillKind == SkillKinds.BuyMount && situation?.CanBuyMount != true)
        {
            return "cannot buy a mount";
        }

        if (candidate.SkillKind == SkillKinds.PlayerVendor && situation?.HasHouse != true)
        {
            return "no house";
        }

        if (candidate.SkillKind == SkillKinds.House && situation?.HasHouse == true && !situation.HouseDue &&
            !situation.VendorVisitDue)
        {
            return "already has a house";
        }

        if (candidate.SkillKind == SkillKinds.Boat &&
            situation?.HasBoat != true &&
            (situation?.Gold ?? 0) < AmbitionRules.DefaultBoatGold)
        {
            return "not enough gold for a boat";
        }

        if (candidate.SkillKind == SkillKinds.Heal &&
            (situation?.Needs?.HitsFraction ?? 1) >= NeedsBrain.HurtHitsFraction)
        {
            return "not hurt";
        }

        // A rest ends the moment the hits are full, so at full hits it starts and ends in the
        // same second. Picked again as the one action left, Konrad rested 150 times in five
        // minutes on one tile; with nothing else to do the idle wander takes the time instead.
        if (candidate.SkillKind == SkillKinds.Rest &&
            (situation?.Needs?.HitsFraction ?? Vitals.FullHits) >= Vitals.FullHits)
        {
            return "hits are full";
        }

        if (candidate.SkillKind == SkillKinds.Conflict &&
            situation?.Disposition != DispositionKind.Outlaw &&
            situation?.IsPkHunter != true &&
            !GoalRules.AnswersPkReport(situation))
        {
            return "no player conflict to seek";
        }

        // The failure streak has no clock: it ends only when other work succeeds, and a
        // stranded walker beyond the leash has little other work, so going home stays out
        // of it. The skill itself turns to the moongate, and in a dungeon to the way out,
        // after repeated failed walks.
        if (candidate.SkillKind != SkillKinds.GoHome &&
            RepeatFailure.IsBlocked(situation?.BlockedCount ?? needs.BlockedCount) &&
            IsBlockedId(candidate, situation, needs))
        {
            return "blocked after repeated failure";
        }

        // The cooldown has a clock, and going home obeys it: exempt, a walker in a dungeon
        // with no way home started again six seconds after "leaves GoHome for 5 minutes".
        if (IsListedSkill(candidate.SkillKind, situation?.BlockedSkillKinds))
        {
            return "skill cooling down after repeated failure";
        }

        if (IsListedSkill(candidate.SkillKind, situation?.UnmetSkillKinds))
        {
            return "nothing here to use it on";
        }

        var family = FamilyOf(candidate);
        var outing = family is RoutineFamily.Hunt or RoutineFamily.Dungeon or RoutineFamily.Party;
        var power = DecideChoice.EffectivePower(outing ? needs.OutingPower : needs.Power, needs.AlliesPower, outing);

        if (candidate.RequiredPower > 0 && power < candidate.RequiredPower)
        {
            return "below required power";
        }

        if (candidate.SkillKind.Equals(SkillKinds.House, StringComparison.OrdinalIgnoreCase) &&
            situation?.CanBuyHouse != true && situation?.HouseDue != true && situation?.VendorVisitDue != true)
        {
            return "cannot buy a house";
        }

        // Goods count only when a live shop in reach buys them (see SaleGoods).
        if (candidate.SkillKind.Equals(SkillKinds.VendorSell, StringComparison.OrdinalIgnoreCase) &&
            situation?.HasSellGoods != true)
        {
            return "no shop in reach buys the goods";
        }

        if (candidate.SkillKind.Equals(SkillKinds.BankDeposit, StringComparison.OrdinalIgnoreCase) &&
            situation?.NothingToBank == true)
        {
            return "nothing to bank";
        }

        if (IsBankTownSkill(candidate.SkillKind) &&
            situation?.HasSellGoods == true &&
            situation.InTownRegion)
        {
            return "pack has goods to sell";
        }

        if (candidate.SkillKind.Equals(SkillKinds.VendorSell, StringComparison.OrdinalIgnoreCase) &&
            !TownStayRules.MaySell(situation?.Role ?? CharacterRole.Worker, situation?.InTownRegion == true))
        {
            return "worker must sell in town";
        }

        if (situation?.InTownRegion != true && NeedsTown(candidate.SkillKind))
        {
            return "must be in town";
        }

        if (TripIntoDanger(candidate, situation, catalog))
        {
            return "the way there is still dangerous";
        }

        if (TripUnreachable(candidate, situation, catalog))
        {
            return "no way to get there";
        }

        return null;
    }

    /// <summary>
    /// A trip whose destination or straight path crosses a place the character just
    /// fled re-triggers the flee the moment the hostile shows again. Escape skills
    /// are exempt: leaving is how the character gets clear.
    /// </summary>
    private static bool TripIntoDanger(
        ActionCandidate candidate,
        Situation situation,
        DestinationCatalog catalog
    )
    {
        if (situation?.DangerSpots is not { Count: > 0 } spots ||
            candidate.SkillKind is SkillKinds.Flee or SkillKinds.GoHome or SkillKinds.Recall)
        {
            return false;
        }

        var to = TripDestination(candidate, situation, catalog);

        if (to is not { } destination)
        {
            return false;
        }

        if (NavSearch.IsNearAny(destination, spots))
        {
            return true;
        }

        var from = situation.Location;
        var mid = new Point3D((from.X + destination.X) / 2, (from.Y + destination.Y) / 2, from.Z);
        return NavSearch.IsNearAny(mid, spots);
    }

    /// <summary>
    /// A trip whose destination sits where the router just proved unreachable —
    /// no node near the goal, or no path to it — fails the walk every time while
    /// a different work spot would do. Escape skills are exempt: leaving is the
    /// fix, and going home is how a stranded character recovers.
    /// </summary>
    private static bool TripUnreachable(
        ActionCandidate candidate,
        Situation situation,
        DestinationCatalog catalog
    )
    {
        if (situation?.UnreachableGoals is not { Count: > 0 } goals ||
            candidate.SkillKind is SkillKinds.Flee or SkillKinds.GoHome or SkillKinds.Recall)
        {
            return false;
        }

        return TripDestination(candidate, situation, catalog) is { } destination &&
               NavSearch.IsNearAny(destination, goals);
    }

    private static Point3D? TripDestination(
        ActionCandidate candidate,
        Situation situation,
        DestinationCatalog catalog
    )
    {
        var step = candidate.Step;

        if (step == null)
        {
            return null;
        }

        if (step.Target != Point3D.Zero)
        {
            return step.Target;
        }

        if (step.Center != Point3D.Zero)
        {
            return step.Center;
        }

        return string.IsNullOrWhiteSpace(step.Destination)
            ? null
            : catalog?.Resolve(step.Destination, situation.Location)?.Arrival;
    }

    /// <summary>The crafter's trade, or the harvest that yields its own stock: that one runs only when it must.</summary>
    private static bool IsOwnTrade(ActionCandidate candidate, Situation situation) =>
        !string.IsNullOrWhiteSpace(situation?.CraftTrade) &&
        (string.Equals(candidate.SkillKind, situation.CraftTrade, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(candidate.SkillKind, CraftCareerRules.TradeByKind(situation.CraftTrade)?.GatherKind, StringComparison.OrdinalIgnoreCase));

    private static bool IdlesNearHome(string skill) =>
        skill is SkillKinds.Loiter or SkillKinds.IdleWander;

    /// <summary>
    /// Going home is a walk out of the wild: away from a threat the character can no longer
    /// run from, back after a flee, to a town job from outside town, back inside the leash,
    /// or a red's ride back to the Den from a run. In any town the person does its town job
    /// in that town, as players did: a traveller who came to see Trinsic walked straight home
    /// to Yew, and a hundred and fifty people at a time were on their way home.
    /// </summary>
    private static bool NeedsWalkHome(Situation situation, Goal goal, bool goHomeAfterRuns, bool fleeBlocked)
    {
        // The Den is a town, but not a blue's: "already near home" kept ex-reds at its tavern.
        if (goHomeAfterRuns || situation?.LeavesDen == true || EscapesHome(situation, fleeBlocked))
        {
            return true;
        }

        if (situation?.InTownRegion == true)
        {
            return false;
        }

        return situation?.BeyondLeash == true || WantsTownFromOutside(situation, goal) || RidesBackToDen(situation, goal);
    }

    /// <summary>
    /// A red out of town rides back to the Den when its run ends, however near: a red farming
    /// the ground by the Den stood inside the leash, "already near home", and its bank step
    /// outside town had no way to start.
    /// </summary>
    private static bool RidesBackToDen(Situation situation, Goal goal) =>
        goal.Kind == GoalKind.Pk && situation is { Disposition: DispositionKind.Outlaw, InTownRegion: false };

    /// <summary>A red's ride out to the hot spots: a hurt red heals before it goes.</summary>
    private static bool IsRedRunOut(ActionCandidate candidate, Situation situation) =>
        candidate.SkillKind == SkillKinds.Conflict && situation?.Disposition == DispositionKind.Outlaw;

    /// <summary>
    /// A threat in sight and no flee left: the way home, toward the guards, is the escape.
    /// Without it every action was barred, and the fallback wander failed at the threat
    /// every four seconds.
    /// </summary>
    private static bool EscapesHome(Situation situation, bool fleeBlocked) =>
        situation?.MustFlee == true && fleeBlocked && situation.DistanceFromHome > ActionPlaceRules.AtPlaceRange;

    private static bool IsLowOnMana(Situation situation) =>
        situation is { IsCaster: true } && situation.ManaFraction < SelfCareRules.MeditateBelowManaFraction;

    /// <summary>A town job out of town starts with the walk home.</summary>
    private static bool WantsTownFromOutside(Situation situation, Goal goal) =>
        situation?.InTownRegion == false && JobRules.IsTownJob(goal.Target);

    private static bool NeedsTown(string skill) =>
        IsBankTownSkill(skill) ||
        skill is SkillKinds.Tavern or SkillKinds.Visit or SkillKinds.UpgradeGear or SkillKinds.VendorBuy
            or SkillKinds.BuyMount or SkillKinds.Browse;

    private static bool IsSameAction(ActionCandidate candidate, string currentId)
    {
        if (candidate == null || string.IsNullOrWhiteSpace(currentId))
        {
            return false;
        }

        if (candidate.Id.Value.Equals(currentId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var skill = ActionId.SkillKindOf(currentId);
        return !string.IsNullOrWhiteSpace(skill) &&
               skill.Equals(candidate.SkillKind, StringComparison.OrdinalIgnoreCase);
    }

    private static bool KeepTradeFamily(ActionCandidate candidate, Situation situation, Goal goal) =>
        IsPackedTrade(situation, goal) && FamilyOf(candidate) == RoutineFamily.Trade;

    private static bool IsPackedTrade(Situation situation, Goal goal) =>
        goal.Kind == GoalKind.Trade && situation?.HasSellGoods == true;

    private static bool IsBankTownSkill(string skill) =>
        skill is SkillKinds.BankDeposit or SkillKinds.BankShop;

    private static bool IsPartyFormingCrew(RoutineFamily family, NeedsSnapshot needs) =>
        needs is { PartyForming: true } &&
        family is RoutineFamily.Hunt or RoutineFamily.Dungeon or RoutineFamily.Party;

    private static bool IsPartyFormingTownWalkAtPlace(
        bool formingCrew,
        ActionCandidate candidate,
        bool atPlace
    ) =>
        formingCrew && atPlace && ActionPlaceRules.IsTownWalk(candidate.Step);

    private static bool ShouldBoostPackedTownWalk(ActionCandidate candidate, Situation situation, Goal goal) =>
        IsPackedTrade(situation, goal) &&
        ActionPlaceRules.IsTownWalk(candidate.Step) &&
        !IsBankTownSkill(candidate.SkillKind);

    private static bool ShouldBoostPackedSellInTown(ActionCandidate candidate, Situation situation, Goal goal) =>
        IsPackedTrade(situation, goal) &&
        situation.InTownRegion &&
        candidate.SkillKind.Equals(SkillKinds.VendorSell, StringComparison.OrdinalIgnoreCase);

    private static bool IsSameFamily(ActionCandidate candidate, string currentId)
    {
        var skill = ActionId.SkillKindOf(currentId);

        if (candidate == null || string.IsNullOrWhiteSpace(skill))
        {
            return false;
        }

        return FamilyOf(candidate) == FamilyOf(new ActionCandidate { SkillKind = skill, RoutineId = currentId });
    }

    private static bool IsListedSkill(string skillKind, IReadOnlyList<string> kinds) =>
        RepeatFailure.Lists(kinds, skillKind);

    private static bool IsBlockedId(ActionCandidate candidate, Situation situation, NeedsSnapshot needs)
    {
        var blocked = situation?.BlockedActionId ?? needs.BlockedRoutine;

        if (string.IsNullOrWhiteSpace(blocked))
        {
            return false;
        }

        return blocked.Equals(candidate.Id.Value, StringComparison.OrdinalIgnoreCase) ||
               blocked.Equals(candidate.RoutineId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   ActionId.SkillKindOf(blocked),
                   candidate.SkillKind,
                   StringComparison.OrdinalIgnoreCase
               );
    }

    private static bool MatchesGoal(RoutineFamily family, string skillKind, GoalKind goal)
    {
        if (goal is GoalKind.Ghost)
        {
            return true;
        }

        if (goal == GoalKind.Flee)
        {
            return skillKind == SkillKinds.Flee;
        }

        if (skillKind == SkillKinds.Follow &&
            goal is GoalKind.Hunt or GoalKind.Dungeon)
        {
            return true;
        }

        return family == GoalRules.FamilyOf(goal);
    }

    public static RoutineFamily FamilyOf(ActionCandidate candidate)
    {
        if (candidate == null)
        {
            return RoutineFamily.Other;
        }

        return candidate.SkillKind switch
        {
            SkillKinds.Hunt => RoutineFamily.Hunt,
            SkillKinds.Dungeon => RoutineFamily.Dungeon,
            SkillKinds.Follow => RoutineFamily.Party,
            SkillKinds.Flee => RoutineFamily.Other,
            SkillKinds.GoHome => RoutineFamily.Rest,
            SkillKinds.VendorSell or SkillKinds.VendorBuy or SkillKinds.BankDeposit or SkillKinds.BankShop
                or SkillKinds.BuyMount or SkillKinds.Browse => RoutineFamily.Trade,
            SkillKinds.Lumberjack or SkillKinds.Mine or SkillKinds.Fish or SkillKinds.House
                or SkillKinds.Smith or SkillKinds.Boat or SkillKinds.Alchemy or SkillKinds.Inscription
                or SkillKinds.Tailor or SkillKinds.Carpentry or SkillKinds.Fletch or SkillKinds.Tinker
                or SkillKinds.Cartography => RoutineFamily.Work,
            SkillKinds.Rest or SkillKinds.IdleWander or SkillKinds.UpgradeGear or SkillKinds.Heal
                or SkillKinds.Meditate or SkillKinds.Vet or SkillKinds.Spirit or SkillKinds.Camp
                => RoutineFamily.Rest,
            SkillKinds.Track or SkillKinds.Lore or SkillKinds.Anatomy or SkillKinds.EvalInt
                or SkillKinds.ArmsLore or SkillKinds.ItemId => RoutineFamily.Hunt,
            SkillKinds.Tavern or SkillKinds.Visit or SkillKinds.Sightsee or SkillKinds.Loiter
                or SkillKinds.Cook or SkillKinds.Beg or SkillKinds.Taste or SkillKinds.Arrive
                or SkillKinds.Travel => RoutineFamily.Leisure,
            SkillKinds.Patrol => RoutineFamily.Work,
            _ => NeedsBrain.FamilyOf(candidate.RoutineId)
        };
    }

}
