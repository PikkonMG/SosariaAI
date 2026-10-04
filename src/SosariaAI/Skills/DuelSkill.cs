using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// One side of a friendly duel: the challenge or the "gl", the walk ten tiles clear of the
/// banker, the fight (the combat brain swings; the duel floor stops it), and the "gf" or the
/// loser's excuse. While the fight runs the skill counts as a hunt: the combat brain's turn
/// to fight does not abort it, and the routine keeps ticking beside the combat brain. Every
/// duel used to end on its first blow, when entering the fight aborted the skill.
/// </summary>
public sealed class DuelSkill : Skill, IHuntingSkill
{
    public const string SkillName = "Duel";

    private readonly Duel _duel;
    private SosariaCharacter _self;
    private Skill _walk;
    private bool _closed;

    public DuelSkill(Duel duel) => _duel = duel;

    public override string Name => SkillName;

    /// <summary>True while the two trade blows.</summary>
    public bool IsHunting => _duel?.Stage == DuelStage.Fighting;

    public override bool Begin(SosariaCharacter character)
    {
        _self = character;
        _closed = false;

        if (_duel == null || !_duel.Involves(character) || _duel.Stage != DuelStage.Walking)
        {
            return false;
        }

        var challenger = character.Serial == _duel.Challenger;
        var partner = World.FindMobile(_duel.OtherOf(character.Serial));
        Talk.Say(
            character,
            challenger ? TalkCategory.DuelChallenge : TalkCategory.DuelAccept,
            new TalkSlots { Name = partner?.Name }
        );

        var side = challenger ? -DuelRules.SpacingTiles / 2 : DuelRules.SpacingTiles / 2;
        var stand = new Point3D(_duel.Ground.X + side, _duel.Ground.Y, _duel.Ground.Z);
        _walk = new TravelSkill(stand, DuelRules.SpacingTiles / 2);
        return _walk.Begin(character);
    }

    public override SkillStatus Tick()
    {
        if (_self == null || _self.Deleted || !_self.Alive)
        {
            return SkillStatus.Failed;
        }

        if (_duel.Stage == DuelStage.Over || DuelRules.TimedOut(_duel.Stage, _duel.StageStarted, Core.Now))
        {
            return Close();
        }

        if (_duel.Stage == DuelStage.Fighting)
        {
            // The fight ended for this side without the floor (it fled or lost its foe):
            // step back in while the duel clock runs.
            Fight();
            return SkillStatus.Running;
        }

        if (_walk != null)
        {
            var status = _walk.Tick();

            if (status == SkillStatus.Running)
            {
                return status;
            }

            _walk = null;

            if (status == SkillStatus.Failed)
            {
                Duels.End(_duel, Serial.Zero, Core.Now);
                return Close();
            }

            MarkReady();
        }

        if (_duel.ChallengerReady && _duel.PartnerReady)
        {
            _duel.BeginFight(Core.Now);
            Fight();
        }

        return SkillStatus.Running;
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;

        if (_duel is { Stage: not DuelStage.Over })
        {
            Duels.End(_duel, Serial.Zero, Core.Now);
        }

        if (_self != null)
        {
            Duels.Release(_self);
        }

        _self = null;
    }

    public override void Resume(TimeSpan held) => _walk?.Resume(held);

    private void MarkReady()
    {
        if (_self.Serial == _duel.Challenger)
        {
            _duel.ChallengerReady = true;
        }
        else
        {
            _duel.PartnerReady = true;
        }
    }

    // A duelist that stepped off to bandage is let be; it comes back to the fight on its own.
    private void Fight()
    {
        if (World.FindMobile(_duel.OtherOf(_self.Serial)) is not { Deleted: false, Alive: true } partner ||
            partner.Map != _self.Map)
        {
            Duels.End(_duel, Serial.Zero, Core.Now);
            return;
        }

        if (_self.Combatant == partner || _self.Motor.Action == CharacterAction.Flee)
        {
            return;
        }

        _self.Combatant = partner;
        _self.Warmode = true;
        _self.Motor.Action = CharacterAction.Combat;
    }

    private SkillStatus Close()
    {
        if (_duel.Stage != DuelStage.Over)
        {
            Duels.End(_duel, Serial.Zero, Core.Now);
        }

        if (!_closed && _duel.Loser != Serial.Zero)
        {
            _closed = true;
            Talk.Say(
                _self,
                _duel.Loser == _self.Serial ? TalkCategory.DuelLoss : TalkCategory.DuelWin,
                new TalkSlots { Name = World.FindMobile(_duel.OtherOf(_self.Serial))?.Name }
            );
        }

        Duels.Release(_self);
        return SkillStatus.Done;
    }
}
