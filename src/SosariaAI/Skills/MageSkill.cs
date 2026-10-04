using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
using Server.Spells.First;
using SosariaAI.Combat;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// First-circle magery through the real spell path: one try speaks the words of power, the
/// next aims the finished spell. Mana and reagents are the engine's to take.
/// </summary>
public sealed class MageSkill : Skill
{
    private SosariaCharacter _character;
    private Mobile _aim;

    public override string Name => MageRules.Kind;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        return People.InWorld(character);
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

        return TryHealSelf() || TryMagicArrow() ? SkillStatus.Running : SkillStatus.Failed;
    }

    public override void Abort()
    {
        _character = null;
        _aim = null;
    }

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
        if (!MageRules.MayCastHeal(
                _character.Hits,
                _character.HitsMax,
                _character.Mana,
                _character.Skills.Magery.Value))
        {
            return false;
        }

        return CastOn(_character, new HealSpell(_character));
    }

    private bool TryMagicArrow()
    {
        var target = FindMageTarget();

        if (target == null || !MageRules.MayCastArrow(_character.Mana, _character.Skills.Magery.Value))
        {
            return false;
        }

        if (!_character.CanBeHarmful(target, false))
        {
            return false;
        }

        return CastOn(target, new MagicArrowSpell(_character));
    }

    private bool CastOn(Mobile target, Spell spell)
    {
        if (target == null || !SpellCasting.TryBegin(_character, spell))
        {
            return false;
        }

        _aim = target;
        return true;
    }

    private Mobile FindMageTarget()
    {
        foreach (var mobile in _character.GetMobilesInRange(MageRules.ReachTiles))
        {
            if (mobile == null || mobile.Deleted || !mobile.Alive)
            {
                continue;
            }

            if (!_character.CanSee(mobile) || !_character.InLOS(mobile))
            {
                continue;
            }

            var isPlayer = People.IsHuman(mobile);
            var isSosaria = mobile is SosariaCharacter;
            var creature = mobile as BaseCreature;
            var isVendor = mobile is BaseVendor;
            var attackerGuards = SosariaCharacter.UnderGuards(_character);
            var victimGuards = SosariaCharacter.UnderGuards(mobile);

            if (!MageRules.IsMageTarget(
                    isSelf: mobile == _character,
                    isPlayer: isPlayer,
                    isSosaria: isSosaria,
                    isVendor: isVendor,
                    invulnerable: mobile.Blessed || mobile.AccessLevel > AccessLevel.Player,
                    isControlled: creature?.Controlled == true,
                    isSummoned: creature?.Summoned == true,
                    karma: mobile.Karma,
                    alwaysMurderer: creature?.AlwaysMurderer == true,
                    attackerIsPk: _character.IsPk
                ))
            {
                continue;
            }

            if ((isPlayer || isSosaria) &&
                !PkRules.MayAttack(
                    _character.IsPk,
                    attackerGuards,
                    victimGuards,
                    PkRules.IsRed(mobile.Kills),
                    PkRules.InBuccaneersDen(mobile.X, mobile.Y)
                ))
            {
                continue;
            }

            return mobile;
        }

        return null;
    }
}
