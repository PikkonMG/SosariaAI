using Server;
using Server.Mobiles;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Ask a nearby mobile for gold. CheckSkill(Begging). Does not take gold.
/// </summary>
public sealed class BegSkill : Skill
{
    private const string NobodyNearReason = "nobody near to beg from";

    private SosariaCharacter _character;

    public override string Name => SkillKinds.Beg;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        return People.InWorld(character) || CannotStart(NotInWorldReason);
    }

    public override SkillStatus Tick()
    {
        if (!People.InWorld(_character) || _character.Deleted)
        {
            return Fail(LeftWorldReason);
        }

        var target = FindTarget(_character);

        if (target == null)
        {
            return Fail(NobodyNearReason);
        }

        _character.CheckSkill(
            SkillName.Begging,
            BegRules.PracticeMin,
            BegRules.PracticeMax
        );

        return SkillStatus.Done;
    }

    public override void Abort() => _character = null;

    /// <summary>
    /// Someone within reach the engine lets a beggar ask, a vendor first; null when there
    /// is no one, or the beggar rides where that is refused.
    /// </summary>
    public static Mobile FindTarget(Mobile beggar)
    {
        if (!BegRules.MayBegMounted(beggar.Mounted, Core.ML))
        {
            return null;
        }

        Mobile other = null;

        foreach (var mobile in beggar.GetMobilesInRange(BegRules.ReachTiles))
        {
            if (mobile is not { Deleted: false } || !beggar.CanSee(mobile) ||
                !BegRules.IsBegTarget(mobile == beggar, mobile.Player, mobile.Body.IsHuman))
            {
                continue;
            }

            if (mobile is BaseVendor)
            {
                return mobile;
            }

            other ??= mobile;
        }

        return other;
    }
}
