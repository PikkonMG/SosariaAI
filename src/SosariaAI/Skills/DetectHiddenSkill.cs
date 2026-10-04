using Server;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Search nearby tiles. CheckSkill(DetectHidden); on success reveal Hidden mobiles. The
/// search has work only while someone hides in reach (<see cref="HasWork"/>): searched with
/// nobody hidden it found nothing 298 times in one night.
/// </summary>
public sealed class DetectHiddenSkill : Skill
{
    private SosariaCharacter _detector;

    public override string Name => SkillKinds.DetectHidden;

    /// <summary>Someone besides the detector hides within its reach.</summary>
    public static bool HasWork(SosariaCharacter detector)
    {
        if (!People.InWorld(detector))
        {
            return false;
        }

        foreach (var mobile in detector.GetMobilesInRange(DetectHiddenRules.ReachTiles))
        {
            if (mobile is { Deleted: false } && DetectHiddenRules.IsHiddenTarget(mobile.Hidden, mobile == detector))
            {
                return true;
            }
        }

        return false;
    }

    public override bool Begin(SosariaCharacter character)
    {
        _detector = character;
        return People.InWorld(character);
    }

    public override SkillStatus Tick()
    {
        if (!People.InWorld(_detector) || _detector.Deleted)
        {
            return SkillStatus.Failed;
        }

        var ok = _detector.CheckSkill(
            SkillName.DetectHidden,
            DetectHiddenRules.PracticeMin,
            DetectHiddenRules.PracticeMax
        );

        if (!ok)
        {
            return SkillStatus.Failed;
        }

        var found = false;

        foreach (var mobile in _detector.GetMobilesInRange(DetectHiddenRules.ReachTiles))
        {
            if (mobile is not { Deleted: false } || !DetectHiddenRules.IsHiddenTarget(mobile.Hidden, mobile == _detector))
            {
                continue;
            }

            mobile.RevealingAction();
            found = true;
        }

        return found ? SkillStatus.Done : SkillStatus.Failed;
    }

    public override void Abort() => _detector = null;
}
