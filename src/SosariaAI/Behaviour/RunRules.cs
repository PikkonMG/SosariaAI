using Server;
using Moves = Server.Movement.Movement;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Behaviour;

/// <summary>
/// Players ran almost everywhere they had somewhere to be; walking was for
/// wandering, watching, and working a spot. A skill that moves the character
/// toward an errand sets run pace; ambient skills keep the town walk.
/// </summary>
public static class RunRules
{
    /// <summary>Step delay in milliseconds for the pace a mounted or walking character takes.</summary>
    public static int StepDelay(bool mounted, bool running) =>
        running
            ? mounted ? Moves.RunMountDelay : Moves.RunFootDelay
            : mounted ? Moves.WalkMountDelay : Moves.WalkFootDelay;

    /// <summary>Step delays and thinks a walk window spans (<see cref="OnItsWay"/>).</summary>
    public const int WalkWindowCount = 2;

    /// <summary>
    /// How long a walk goal lives once renewed, and how long after its last step a walker is
    /// still on its way: two step delays and two thinks. A walker with no player near thinks
    /// every two seconds, so a window of step delays alone read every walker as standing at
    /// each think: beside a Covetous fire trap it stepped off, the walk stepped back, and 155
    /// trips stalled at the traps in one night.
    /// </summary>
    public static long WalkWindowMs(int stepDelayMs, double thinkDelayMs) =>
        (long)stepDelayMs * WalkWindowCount + (long)(thinkDelayMs * WalkWindowCount);

    /// <summary>True when the last step left the walker inside its walk window (<see cref="WalkWindowMs"/>).</summary>
    public static bool OnItsWay(long msSinceStep, int stepDelayMs, double thinkDelayMs) =>
        msSinceStep < WalkWindowMs(stepDelayMs, thinkDelayMs);

    /// <summary>
    /// A run the engine refused where a walk the same way went through: the mount is spent,
    /// the way a rider hears "Your mount is too fatigued to move". The engine gives a mount one
    /// run step back per idle second and starts that clock again on every refused run, so a
    /// rider that kept asking to run never got a step back: Den reds stood on one tile for an
    /// hour, and the walk stalls rose every ten minutes as more mounts ran out. A tired rider
    /// walks this long, which the engine does not charge, before it asks for a run again.
    /// </summary>
    public const int TiredRunRestMs = 60_000;

    /// <summary>True when a runner runs this step: its pace is a run and no tired rest holds it to a walk.</summary>
    public static bool RunsNow(bool running, long now, long walksUntil) => running && now - walksUntil >= 0;

    /// <summary>Errands, travel, and hunts run. Ambient town and in-place skills walk.</summary>
    public static bool RunsErrand(string skillKind) =>
        skillKind is SkillKinds.GoTo
            or SkillKinds.GoHome
            or SkillKinds.VendorBuy
            or SkillKinds.VendorSell
            or SkillKinds.BankDeposit
            or SkillKinds.BankShop
            or SkillKinds.BuyMount
            or SkillKinds.UpgradeGear
            or SkillKinds.Visit
            or SkillKinds.Sightsee
            or SkillKinds.Tavern
            or SkillKinds.Recall
            or SkillKinds.Gate
            or SkillKinds.Follow
            or SkillKinds.Conflict
            or SkillKinds.Track
            or SkillKinds.Lumberjack
            or SkillKinds.Mine
            or SkillKinds.Fish
            or SkillKinds.House
            or SkillKinds.PlayerVendor
            or SkillKinds.Hunt
            or SkillKinds.Dungeon
            or SkillKinds.Flee
            or SkillKinds.Tame
            or Skills.GhostSkill.SkillName;

    public static void ApplyPace(SosariaCharacter character, string skillKind)
    {
        if (character == null)
        {
            return;
        }

        if (RunsErrand(skillKind))
        {
            character.SetRunPace();
        }
        else
        {
            character.SetWalkPace();
        }
    }
}
