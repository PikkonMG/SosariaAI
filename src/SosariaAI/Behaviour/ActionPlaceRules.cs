using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>
/// Whether a character is already at the place a skill step needs.
/// </summary>
public static class ActionPlaceRules
{
    public const int AtPlaceRange = 8;

    public static bool IsAt(SkillStepDefinition step, Point3D location, int range)
    {
        if (step == null)
        {
            return false;
        }

        if (step.Area is { Width: > 0, Height: > 0 })
        {
            return step.Area.ToRectangle().Contains(location);
        }

        if (step.BankSpot != Point3D.Zero)
        {
            return NavMetric.Chebyshev(location, step.BankSpot) <= range;
        }

        if (step.Target != Point3D.Zero)
        {
            return NavMetric.Chebyshev(location, step.Target) <= range;
        }

        if (step.Center != Point3D.Zero)
        {
            return NavMetric.Chebyshev(location, step.Center) <= range;
        }

        if (step.Points is { Count: > 0 })
        {
            return NavMetric.Chebyshev(location, step.Points[0]) <= range;
        }

        return false;
    }

    public static bool IsTownWalk(SkillStepDefinition step)
    {
        if (step == null)
        {
            return false;
        }

        if (step.Skill is SkillKinds.BankDeposit or SkillKinds.BankShop or SkillKinds.VendorSell
            or SkillKinds.VendorBuy or SkillKinds.BuyMount or SkillKinds.UpgradeGear)
        {
            return true;
        }

        if (step.BankSpot != Point3D.Zero)
        {
            return true;
        }

        return step.Target == CharactersFile.DefaultBankSpot;
    }

    public static bool IsWorkSite(SkillStepDefinition step) =>
        step?.Skill is SkillKinds.Lumberjack or SkillKinds.Mine or SkillKinds.Fish
            or SkillKinds.Hunt or SkillKinds.Dungeon;
}
