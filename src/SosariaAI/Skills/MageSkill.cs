using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
using Server.Spells.First;
using SosariaAI.Combat;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// First-circle magery through the real spell path: a try speaks the words of power, then
/// aims the finished spell. Mana and reagents are the engine's to take.
/// </summary>
public sealed class MageSkill : Skill
{
    /// <summary>Why a practice cast does not start or a try finds nothing to cast.</summary>
    public const string NoCastWhy = "no hurt to heal and no fair game in sight, or no reagents for the spell";

    private SosariaCharacter _character;
    private Mobile _aim;

    public override string Name => MageRules.Kind;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        return HasWork(character) || CannotStart(NoCastWhy);
    }

    public override SkillStatus Tick()
    {
        if (!People.InWorld(_character) || _character.Deleted)
        {
            return SkillStatus.Failed;
        }

        if (_aim != null)
        {
            return AimPending();
        }

        return TryHealSelf() || TryMagicArrow() ? SkillStatus.Running : Fail(NoCastWhy);
    }

    public override void Abort()
    {
        _character = null;
        _aim = null;
    }

    /// <summary>
    /// A cast the mage can pay for and aim where it stands: a Heal on its own hurt body, or a
    /// Magic Arrow at fair game in sight. The scorer asks it before it offers the practice
    /// (<see cref="SkillReadiness"/>): in a town with no hurt and nothing to shoot, every try failed.
    /// </summary>
    public static bool HasWork(SosariaCharacter mage) =>
        People.InWorld(mage) &&
        (MayHealSelf(mage, new HealSpell(mage)) || MayShoot(mage, new MagicArrowSpell(mage)) && FindMageTarget(mage) != null);

    private static bool MayHealSelf(SosariaCharacter mage, Spell heal) =>
        MageRules.MayCastHeal(mage.Hits, mage.HitsMax, mage.Mana, mage.Skills.Magery.Value) && CanPay(mage, heal);

    private static bool MayShoot(SosariaCharacter mage, Spell arrow) =>
        MageRules.MayCastArrow(mage.Mana, mage.Skills.Magery.Value) && CanPay(mage, arrow);

    /// <summary>The book holds the spell and the pack its reagents, as <see cref="SpellCasting.TryBegin"/> asks.</summary>
    private static bool CanPay(SosariaCharacter mage, Spell spell) =>
        SpellCasting.Knows(mage, spell) && SpellCasting.HasReagents(mage.Backpack, spell.Info);

    private SkillStatus AimPending()
    {
        var target = _aim;

        if (target.Deleted || !target.Alive || target.Map != _character.Map)
        {
            _aim = null;
            return SkillStatus.Failed;
        }

        if (SpellCasting.TryAim(_character, target))
        {
            _aim = null;
            return SkillStatus.Done;
        }

        if (_character.Spell == null)
        {
            _aim = null;
            return SkillStatus.Failed;
        }

        return SkillStatus.Running;
    }

    private bool TryHealSelf()
    {
        var heal = new HealSpell(_character);
        return MayHealSelf(_character, heal) && CastOn(_character, heal);
    }

    private bool TryMagicArrow()
    {
        var arrow = new MagicArrowSpell(_character);
        return MayShoot(_character, arrow) && FindMageTarget(_character) is { } target && CastOn(target, arrow);
    }

    private bool CastOn(Mobile target, Spell spell)
    {
        if (!SpellCasting.TryBegin(_character, spell))
        {
            return false;
        }

        _aim = target;
        return true;
    }

    /// <summary>The first living mobile in reach and sight that is fair game the mage may harm now.</summary>
    private static Mobile FindMageTarget(SosariaCharacter mage)
    {
        var attackerGuards = SosariaCharacter.UnderGuards(mage);

        foreach (var mobile in mage.GetMobilesInRange(MageRules.ReachTiles))
        {
            if (mobile == null || mobile.Deleted || !mobile.Alive)
            {
                continue;
            }

            if (!mage.CanSee(mobile) || !mage.InLOS(mobile))
            {
                continue;
            }

            var isPlayer = People.IsHuman(mobile);
            var isSosaria = mobile is SosariaCharacter;
            var creature = mobile as BaseCreature;
            var isVendor = mobile is BaseVendor;

            if (!MageRules.IsMageTarget(
                    isSelf: mobile == mage,
                    isPlayer: isPlayer,
                    isSosaria: isSosaria,
                    isVendor: isVendor,
                    invulnerable: mobile.Blessed || mobile.AccessLevel > AccessLevel.Player,
                    isControlled: creature?.Controlled == true,
                    isSummoned: creature?.Summoned == true,
                    karma: mobile.Karma,
                    alwaysMurderer: creature?.AlwaysMurderer == true,
                    attackerIsPk: mage.IsPk
                ))
            {
                continue;
            }

            if ((isPlayer || isSosaria) &&
                !PkRules.MayAttack(
                    mage.IsPk,
                    attackerGuards,
                    SosariaCharacter.UnderGuards(mobile),
                    PkRules.IsRed(mobile.Kills),
                    PkRules.InBuccaneersDen(mobile.X, mobile.Y)
                ))
            {
                continue;
            }

            if (!mage.CanBeHarmful(mobile, false))
            {
                continue;
            }

            return mobile;
        }

        return null;
    }
}
