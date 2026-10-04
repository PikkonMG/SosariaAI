using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Common;

namespace SosariaAI.Combat;

public static class CharacterPower
{
    /// <summary>
    /// The power a person fights with now: its wounds take a share of it (<see cref="PowerRating.HitsFloor"/>).
    /// A body with no hit point maximum counts as having no hits.
    /// </summary>
    public static int For(Mobile mobile) =>
        mobile == null ? 0 : Score(mobile, mobile.HitsMax > 0 ? Vitals.HitsFraction(mobile) : 0);

    /// <summary>
    /// The power a person has once its wounds are mended: what a place is judged against, so
    /// a crawler fresh from a fight does not call its floor too hard for the hits it will heal.
    /// </summary>
    public static int Healthy(Mobile mobile) => mobile == null ? 0 : Score(mobile, PowerRating.MaxHitsFraction);

    private static int Score(Mobile mobile, double hitsFraction)
    {
        var weaponSkill = PrimaryWeaponSkill(mobile);
        var tactics = (int)mobile.Skills[SkillName.Tactics].Value;
        var anatomy = (int)mobile.Skills[SkillName.Anatomy].Value;
        var evalInt = (int)mobile.Skills[SkillName.EvalInt].Value;
        var pack = mobile.Backpack;
        var bandages = pack?.GetAmount(typeof(Bandage)) ?? 0;
        var potions = pack?.GetAmount(typeof(GreaterHealPotion)) ?? 0;
        var reagents = pack?.GetAmount(typeof(BaseReagent)) ?? 0;
        return PowerRating.Score(
            weaponSkill,
            tactics,
            anatomy,
            mobile.RawStr,
            mobile.RawDex,
            mobile.RawInt,
            hitsFraction,
            GearScore.Of(mobile),
            bandages,
            potions,
            evalInt,
            mobile.Mana,
            reagents
        );
    }

    private static int PrimaryWeaponSkill(Mobile mobile)
    {
        var sword = (int)mobile.Skills[SkillName.Swords].Value;
        var fencing = (int)mobile.Skills[SkillName.Fencing].Value;
        var mace = (int)mobile.Skills[SkillName.Macing].Value;
        var archery = (int)mobile.Skills[SkillName.Archery].Value;
        var wrestling = (int)mobile.Skills[SkillName.Wrestling].Value;
        var magery = (int)mobile.Skills[SkillName.Magery].Value;
        var best = sword;
        if (fencing > best)
        {
            best = fencing;
        }

        if (mace > best)
        {
            best = mace;
        }

        if (archery > best)
        {
            best = archery;
        }

        if (wrestling > best)
        {
            best = wrestling;
        }

        if (magery > best)
        {
            best = magery;
        }

        return best;
    }
}
