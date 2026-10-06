namespace SosariaAI.Mobiles;

/// <summary>
/// Save versions. Version 1 added the one-time rune kit flag; a version 0 save loads every
/// field as it was, and its people get the rune kit once on their next bind. Version 2 added
/// the rule clocks and the job target streaks; an older save loads with none running. Version 3
/// dropped the session end: nobody logs out on a session clock, and an older save's is dropped.
/// Version 4 dropped the recent ring, the life pins, the opinions and the places seen: long-term
/// memory lives in <c>memory.db</c> (<see cref="SosariaAI.Memory.MemoryStore"/>) and recent thoughts in
/// RAM, so an older save's lists are ignored and everyone starts fresh. Version 5 added the
/// orders a crafter took; an older save loads with none.
/// </summary>
public partial class SosariaCharacter
{
    private void MigrateFrom(V4Content content)
    {
        _routineStepIndex = content.RoutineStepIndex;
        _idleElapsed = content.IdleElapsed;
        _characterId = content.CharacterId;
        _homeSpawn = content.HomeSpawn;
        _homeMapName = content.HomeMapName;
        _returnAt = content.ReturnAt;
        _returnAfterDeath = content.ReturnAfterDeath;
        _activeRoutineId = content.ActiveRoutineId;
        _homeFacet = content.HomeFacet;
        _lastHuntAt = content.LastHuntAt;
        _lastRestAt = content.LastRestAt;
        _lastTownAt = content.LastTownAt;
        _guildIndex = content.GuildIndex;
        _corpseSerial = content.CorpseSerial;
        _corpseLocation = content.CorpseLocation;
        _deathPlace = content.DeathPlace;
        _lastKillerName = content.LastKillerName;
        _ghostSince = content.GhostSince;
        _careerStarted = content.CareerStarted;
        _ambitionKind = content.AmbitionKind;
        _ambitionTarget = content.AmbitionTarget;
        _ambitionGoal = content.AmbitionGoal;
        _ambitionProgress = content.AmbitionProgress;
        _activeGoalKind = content.ActiveGoalKind;
        _activeGoalTarget = content.ActiveGoalTarget;
        _activeActionId = content.ActiveActionId;
        _lastWalkFailed = content.LastWalkFailed;
        _goldAtDeath = content.GoldAtDeath;
        _houseSerial = content.HouseSerial;
        _houseLocation = content.HouseLocation;
        _lastDeathAt = content.LastDeathAt;
        _deathsAtPlace = content.DeathsAtPlace;
        _planId = content.PlanId;
        _planStepIndex = content.PlanStepIndex;
        _planStepFailures = content.PlanStepFailures;
        _boatSerial = content.BoatSerial;
        _vendorSerial = content.VendorSerial;
        _modelPlanLines = content.ModelPlanLines ?? [];
        _runeKitPacked = content.RuneKitPacked;
        _ruleClocks = content.RuleClocks ?? new();
        _targetStreaks = content.TargetStreaks ?? new();
    }

    private void MigrateFrom(V3Content content)
    {
        _routineStepIndex = content.RoutineStepIndex;
        _idleElapsed = content.IdleElapsed;
        _characterId = content.CharacterId;
        _homeSpawn = content.HomeSpawn;
        _homeMapName = content.HomeMapName;
        _returnAt = content.ReturnAt;
        _returnAfterDeath = content.ReturnAfterDeath;
        _activeRoutineId = content.ActiveRoutineId;
        _homeFacet = content.HomeFacet;
        _lastHuntAt = content.LastHuntAt;
        _lastRestAt = content.LastRestAt;
        _lastTownAt = content.LastTownAt;
        _guildIndex = content.GuildIndex;
        _corpseSerial = content.CorpseSerial;
        _corpseLocation = content.CorpseLocation;
        _deathPlace = content.DeathPlace;
        _lastKillerName = content.LastKillerName;
        _ghostSince = content.GhostSince;
        _careerStarted = content.CareerStarted;
        _ambitionKind = content.AmbitionKind;
        _ambitionTarget = content.AmbitionTarget;
        _ambitionGoal = content.AmbitionGoal;
        _ambitionProgress = content.AmbitionProgress;
        _activeGoalKind = content.ActiveGoalKind;
        _activeGoalTarget = content.ActiveGoalTarget;
        _activeActionId = content.ActiveActionId;
        _lastWalkFailed = content.LastWalkFailed;
        _goldAtDeath = content.GoldAtDeath;
        _houseSerial = content.HouseSerial;
        _houseLocation = content.HouseLocation;
        _lastDeathAt = content.LastDeathAt;
        _deathsAtPlace = content.DeathsAtPlace;
        _planId = content.PlanId;
        _planStepIndex = content.PlanStepIndex;
        _planStepFailures = content.PlanStepFailures;
        _boatSerial = content.BoatSerial;
        _vendorSerial = content.VendorSerial;
        _modelPlanLines = content.ModelPlanLines ?? [];
        _runeKitPacked = content.RuneKitPacked;
        _ruleClocks = content.RuleClocks ?? new();
        _targetStreaks = content.TargetStreaks ?? new();
    }

    private void MigrateFrom(V2Content content)
    {
        _routineStepIndex = content.RoutineStepIndex;
        _idleElapsed = content.IdleElapsed;
        _characterId = content.CharacterId;
        _homeSpawn = content.HomeSpawn;
        _homeMapName = content.HomeMapName;
        _returnAt = content.ReturnAt;
        _returnAfterDeath = content.ReturnAfterDeath;
        _activeRoutineId = content.ActiveRoutineId;
        _homeFacet = content.HomeFacet;
        _lastHuntAt = content.LastHuntAt;
        _lastRestAt = content.LastRestAt;
        _lastTownAt = content.LastTownAt;
        _guildIndex = content.GuildIndex;
        _corpseSerial = content.CorpseSerial;
        _corpseLocation = content.CorpseLocation;
        _deathPlace = content.DeathPlace;
        _lastKillerName = content.LastKillerName;
        _ghostSince = content.GhostSince;
        _careerStarted = content.CareerStarted;
        _ambitionKind = content.AmbitionKind;
        _ambitionTarget = content.AmbitionTarget;
        _ambitionGoal = content.AmbitionGoal;
        _ambitionProgress = content.AmbitionProgress;
        _activeGoalKind = content.ActiveGoalKind;
        _activeGoalTarget = content.ActiveGoalTarget;
        _activeActionId = content.ActiveActionId;
        _lastWalkFailed = content.LastWalkFailed;
        _goldAtDeath = content.GoldAtDeath;
        _houseSerial = content.HouseSerial;
        _houseLocation = content.HouseLocation;
        _lastDeathAt = content.LastDeathAt;
        _deathsAtPlace = content.DeathsAtPlace;
        _planId = content.PlanId;
        _planStepIndex = content.PlanStepIndex;
        _planStepFailures = content.PlanStepFailures;
        _boatSerial = content.BoatSerial;
        _vendorSerial = content.VendorSerial;
        _modelPlanLines = content.ModelPlanLines ?? [];
        _runeKitPacked = content.RuneKitPacked;
        _ruleClocks = content.RuleClocks ?? new();
        _targetStreaks = content.TargetStreaks ?? new();
    }

    private void MigrateFrom(V1Content content)
    {
        _routineStepIndex = content.RoutineStepIndex;
        _idleElapsed = content.IdleElapsed;
        _characterId = content.CharacterId;
        _homeSpawn = content.HomeSpawn;
        _homeMapName = content.HomeMapName;
        _returnAt = content.ReturnAt;
        _returnAfterDeath = content.ReturnAfterDeath;
        _activeRoutineId = content.ActiveRoutineId;
        _homeFacet = content.HomeFacet;
        _lastHuntAt = content.LastHuntAt;
        _lastRestAt = content.LastRestAt;
        _lastTownAt = content.LastTownAt;
        _guildIndex = content.GuildIndex;
        _corpseSerial = content.CorpseSerial;
        _corpseLocation = content.CorpseLocation;
        _deathPlace = content.DeathPlace;
        _lastKillerName = content.LastKillerName;
        _ghostSince = content.GhostSince;
        _careerStarted = content.CareerStarted;
        _ambitionKind = content.AmbitionKind;
        _ambitionTarget = content.AmbitionTarget;
        _ambitionGoal = content.AmbitionGoal;
        _ambitionProgress = content.AmbitionProgress;
        _activeGoalKind = content.ActiveGoalKind;
        _activeGoalTarget = content.ActiveGoalTarget;
        _activeActionId = content.ActiveActionId;
        _lastWalkFailed = content.LastWalkFailed;
        _goldAtDeath = content.GoldAtDeath;
        _houseSerial = content.HouseSerial;
        _houseLocation = content.HouseLocation;
        _lastDeathAt = content.LastDeathAt;
        _deathsAtPlace = content.DeathsAtPlace;
        _planId = content.PlanId;
        _planStepIndex = content.PlanStepIndex;
        _planStepFailures = content.PlanStepFailures;
        _boatSerial = content.BoatSerial;
        _vendorSerial = content.VendorSerial;
        _modelPlanLines = content.ModelPlanLines ?? [];
        _runeKitPacked = content.RuneKitPacked;
    }

    private void MigrateFrom(V0Content content)
    {
        _routineStepIndex = content.RoutineStepIndex;
        _idleElapsed = content.IdleElapsed;
        _characterId = content.CharacterId;
        _homeSpawn = content.HomeSpawn;
        _homeMapName = content.HomeMapName;
        _returnAt = content.ReturnAt;
        _returnAfterDeath = content.ReturnAfterDeath;
        _activeRoutineId = content.ActiveRoutineId;
        _homeFacet = content.HomeFacet;
        _lastHuntAt = content.LastHuntAt;
        _lastRestAt = content.LastRestAt;
        _lastTownAt = content.LastTownAt;
        _guildIndex = content.GuildIndex;
        _corpseSerial = content.CorpseSerial;
        _corpseLocation = content.CorpseLocation;
        _deathPlace = content.DeathPlace;
        _lastKillerName = content.LastKillerName;
        _ghostSince = content.GhostSince;
        _careerStarted = content.CareerStarted;
        _ambitionKind = content.AmbitionKind;
        _ambitionTarget = content.AmbitionTarget;
        _ambitionGoal = content.AmbitionGoal;
        _ambitionProgress = content.AmbitionProgress;
        _activeGoalKind = content.ActiveGoalKind;
        _activeGoalTarget = content.ActiveGoalTarget;
        _activeActionId = content.ActiveActionId;
        _lastWalkFailed = content.LastWalkFailed;
        _goldAtDeath = content.GoldAtDeath;
        _houseSerial = content.HouseSerial;
        _houseLocation = content.HouseLocation;
        _lastDeathAt = content.LastDeathAt;
        _deathsAtPlace = content.DeathsAtPlace;
        _planId = content.PlanId;
        _planStepIndex = content.PlanStepIndex;
        _planStepFailures = content.PlanStepFailures;
        _boatSerial = content.BoatSerial;
        _vendorSerial = content.VendorSerial;
        _modelPlanLines = content.ModelPlanLines ?? [];
    }
}
