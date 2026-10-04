using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Memory;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// A tamer walks its pets to the animal trainer (<see cref="StableWalk"/>) and stables them the
/// way a player did: "stable", then the trainer's cursor on each pet. The trainer's own checks run
/// on every pet: its fee from the pack or the bank, its limit of stabled pets, no pet in a fight,
/// no loaded pack beast. The tamer takes no more pets than the trainer has room for and walks
/// only with the fee for each in its pack and bank. It writes one line naming the pets that went
/// in and why, and starts the rule clock of the hold (<see cref="PetKeeper.NoteStabled"/>).
/// </summary>
public sealed class StableInSkill : Skill
{
    public const string SkillName = "StableIn";

    public const string NoPetWhy = "no pet to stable";
    public const string StablesFullWhy = "the stables are full";
    public const string NoFeeWhy = "cannot pay the stable fee";
    public const string NoneTakenWhy = "the trainer took no pet";

    private static readonly ILogger logger = SosariaLog.For(typeof(StableInSkill));

    private readonly StableReason _reason;
    private readonly List<BaseCreature> _pets;
    private SosariaCharacter _owner;
    private StableWalk _walk;

    public StableInSkill(StableReason reason, IReadOnlyList<BaseCreature> pets)
    {
        _reason = reason;
        _pets = [..pets];
    }

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

        _pets.RemoveAll(pet => !IsOwnPet(pet));

        if (_pets.Count == 0)
        {
            return CannotStart(NoPetWhy);
        }

        var room = StableRules.StableRoom(AnimalTrainer.GetMaxStabled(character), character.Stabled?.Count ?? 0);

        if (room == 0)
        {
            return CannotStart(StablesFullWhy);
        }

        if (_pets.Count > room)
        {
            _pets.RemoveRange(room, _pets.Count - room);
        }

        var gold = VendorDeal.GoldHeld(character);

        if (!StableRules.CanPay(gold, _pets.Count))
        {
            return CannotStart(NoFeeWhy);
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

        var stabled = new List<string>();

        for (var i = 0; i < _pets.Count; i++)
        {
            if (StableOne(_pets[i]))
            {
                stabled.Add(_pets[i].GetType().Name);
            }
        }

        if (stabled.Count == 0)
        {
            return Fail(NoneTakenWhy);
        }

        PetKeeper.NoteStabled(_owner, _reason, Core.Now);

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Line}", StableRules.StabledLine(_owner.Name, stabled, _walk.Stable, _reason));
        }

        return SkillStatus.Done;
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
    }

    public override void Resume(TimeSpan held) => _walk?.Resume(held);

    /// <summary>
    /// "stable", and the trainer's cursor on the pet, one pet a time as the trainer asks. Only the
    /// cursor the trainer raised is used: a spell cursor already up is never aimed at a pet.
    /// </summary>
    private bool StableOne(BaseCreature pet)
    {
        if (!IsOwnPet(pet))
        {
            return false;
        }

        var before = _owner.Target;
        PetOrders.Speak(_owner, StableRules.StableLine, StableRules.StableKeyword);

        if (_owner.Target is not { } cursor || cursor == before)
        {
            return false;
        }

        cursor.Invoke(_owner, pet);
        return pet.IsStabled;
    }

    private bool IsOwnPet(BaseCreature pet) =>
        pet is { Deleted: false, Controlled: true, IsDeadPet: false, IsStabled: false } && pet.ControlMaster == _owner;
}
