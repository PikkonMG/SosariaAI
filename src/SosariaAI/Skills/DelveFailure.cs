using Server;
using Server.Logging;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Says once per dungeon trip why it failed. A trip that "ended Dungeon (Failed)" 22 seconds
/// after the choice, three times over, left no word of the cause: an island home with no
/// door in reach, a walk with no route, a hall with nothing to fight. The line names the step.
/// </summary>
public sealed class DelveFailure
{
    public const string NoWayToDoor = "the walk to the dungeon door did not finish";
    public const string DoorPadRefused = "the door pad did not carry it inside";
    public const string NoWayInside = "the walk inside the dungeon did not finish";
    public const string NoWayToHall = "the walk to the hall did not finish";
    public const string HallEmpty = "the hall hunt could not start";
    public const string NoRoomReached = "no room on the floor could be reached";
    public const string Died = "it died on the trip";

    private static readonly ILogger logger = SosariaLog.For(typeof(DelveFailure));

    private bool _said;

    public static string Line(string name, string reason, Point3D at) =>
        $"{name} failed a dungeon trip at {at}: {reason}";

    /// <summary>Starts a new trip: its first failure is said again.</summary>
    public void Reset() => _said = false;

    /// <summary>True the first time on this trip; every later failure stays quiet.</summary>
    public bool ShouldSay()
    {
        if (_said)
        {
            return false;
        }

        _said = true;
        return true;
    }

    /// <summary>Says why a start failed and returns the failed start.</summary>
    public bool Refuse(SosariaCharacter character, string reason)
    {
        Say(character, reason);
        return false;
    }

    /// <summary>Passes a tick status through, saying why when it is a failure.</summary>
    public SkillStatus Check(SosariaCharacter character, SkillStatus status, string reason)
    {
        if (status == SkillStatus.Failed)
        {
            Say(character, reason);
        }

        return status;
    }

    private void Say(SosariaCharacter character, string reason)
    {
        if (character == null || !SosariaSettings.LogActivity || !ShouldSay())
        {
            return;
        }

        logger.Information("{Line}", Line(character.Name, reason, character.Location));
    }
}
