using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Memory;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// A tamer walks to the animal trainer (<see cref="StableWalk"/>) and says "claim". The trainer's
/// own speech handler brings out every stabled pet the follower slots hold, following, as it does
/// for a player. The claim ends the tamer's stable holds and starts its
/// <see cref="RuleClock.PetsClaimed"/> clock, and writes one line naming the pets that came out.
/// </summary>
public sealed class StableClaimSkill : Skill
{
    public const string SkillName = "StableClaim";

    public const string NothingStabledWhy = "nothing in the stables";
    public const string NoPetOutWhy = "the trainer brought no pet out";

    private static readonly ILogger logger = SosariaLog.For(typeof(StableClaimSkill));

    private SosariaCharacter _owner;
    private StableWalk _walk;

    public override string Name => SkillName;

    /// <summary>The stable it walks to.</summary>
    public override JobTarget? AimedAt => _walk?.AimedAt;

    public override bool Begin(SosariaCharacter character)
    {
        _owner = character;
        _walk = null;

        if (!People.InWorld(character))
        {
            return CannotStart(NotInWorldReason);
        }

        if ((character.Stabled?.Count ?? 0) == 0)
        {
            return CannotStart(NothingStabledWhy);
        }

        _walk = new StableWalk(SkillName);
        return _walk.Begin(character) || CannotStart(_walk.FailReason);
    }

    public override SkillStatus Tick()
    {
        if (_walk == null)
        {
            return Fail(LeftWorldReason);
        }

        var status = _walk.Tick();

        if (status == SkillStatus.Running)
        {
            return status;
        }

        if (status == SkillStatus.Failed)
        {
            return Fail(_walk.FailReason);
        }

        var stabled = new List<BaseCreature>();

        foreach (var mobile in _owner.Stabled ?? [])
        {
            if (mobile is BaseCreature { Deleted: false } pet)
            {
                stabled.Add(pet);
            }
        }

        PetOrders.Speak(_owner, PetRules.ClaimLine, PetRules.ClaimKeyword);

        var claimed = stabled.FindAll(pet => !pet.IsStabled).ConvertAll(static pet => pet.GetType().Name);

        if (claimed.Count == 0)
        {
            return Fail(NoPetOutWhy);
        }

        PetKeeper.NoteClaim(_owner, Core.Now);

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Line}", StableRules.ClaimedLine(_owner.Name, claimed, _walk.Stable));
        }

        return SkillStatus.Done;
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
    }

    public override void Resume(TimeSpan held) => _walk?.Resume(held);
}
