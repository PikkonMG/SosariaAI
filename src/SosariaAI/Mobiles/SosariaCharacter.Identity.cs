using System.Collections.Generic;
using Server;
using Server.Guilds;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Economy;
using SosariaAI.Skills;
using SosariaAI.Spawning;

namespace SosariaAI.Mobiles;

/// <summary>
/// Who the character is: its profile (class, skill tier, traits, wealth, leanings),
/// its era build, and what it owns on the day it first walks in. The profile is rolled
/// from the character id on every bind, so nothing here is saved.
/// </summary>
public partial class SosariaCharacter
{
    /// <summary>Class, skill tier, traits, wealth, activity leanings and phase length. Never null.</summary>
    public PersonProfile PersonProfile { get; private set; } = PersonProfile.Default;

    /// <summary>How readily this character stands and fights, 0 to 1: its valor, calm and skill tier.</summary>
    public double Nerve => PersonProfileRules.Nerve(Persona?.ResolvedDrives(), PersonProfile.Tier);

    /// <summary>
    /// Sets the profile and the era caps. A copy wears its class build; a fixture keeps
    /// its authored build, already fitted to the caps by <see cref="BuildPresets"/>.
    /// Call after <see cref="BindIdentity"/>, which sets the id and the job's role.
    /// </summary>
    public void BindProfile(PersonProfile profile)
    {
        PersonProfile = profile ?? PersonProfile.Default;
        StatCap = EraBuildCaps.StatTotalCap;
        SkillsCap = EraBuildCaps.SkillTotalCapFixed;
        DisplayGuildTitle = Guild != null;

        if (WorkSites.IsCopy(CharacterId))
        {
            Build = ClassBuilds.For(PersonProfile, Build?.Role ?? CharacterRole.Worker, CharacterId, EraBands.Current());
        }
    }

    /// <summary>
    /// A fresh character's first day: the worn kit becomes crafted work, coloured ore or era
    /// magic (<see cref="KitFinish"/>), the pack gets its purse and supplies, and a wealthy
    /// veteran rides in. A book that no free hand holds was packed when the kit went on. An
    /// established traveler brings the runes it marked before (<see cref="RuneKit"/>).
    /// </summary>
    public void CompleteFreshStart()
    {
        KitFinish.Apply(
            this,
            PersonProfile.Tier,
            PersonProfile.Wealth,
            CharacterId,
            EraBands.Current(),
            GearLadder.KeepCeiling(ClassBuilds.TemplateOf(this))
        );
        PackStartingStacks();
        RuneKit.Pack(this, firstDay: true);
        RideStartingMount();
    }

    // A player shows the guild tag on a single click, not only in the tooltip.
    public override void OnGuildChange(BaseGuild oldGuild)
    {
        base.OnGuildChange(oldGuild);
        DisplayGuildTitle = Guild != null;
    }

    private void PackStartingStacks()
    {
        var stacks = VeteranKit.For(PersonProfile, ClassBuilds.TemplateOf(this), CharacterId);

        for (var i = 0; i < stacks.Count; i++)
        {
            var stack = stacks[i];
            var item = KitResolver.Create(stack.TypeName, name => AssemblyHandler.FindTypeByName(name));

            if (item == null)
            {
                continue;
            }

            if (item.Stackable)
            {
                item.Amount = stack.Amount;
                EnsurePile(item);
                continue;
            }

            AddToBackpack(item);

            for (var extra = 1; extra < stack.Amount; extra++)
            {
                AddToBackpack(KitResolver.Create(stack.TypeName, name => AssemblyHandler.FindTypeByName(name)));
            }
        }
    }

    private void RideStartingMount()
    {
        if (Mounted || !People.InWorld(this))
        {
            return;
        }

        BaseMount mount = StartingMountRules.Pick(PersonProfile, CharacterId) switch
        {
            StartingMount.Horse => new Horse(),
            StartingMount.ForestOstard => new ForestOstard(),
            StartingMount.DesertOstard => new DesertOstard(),
            StartingMount.Llama => new RidableLlama(),
            _ => null
        };

        if (mount == null)
        {
            return;
        }

        if (!mount.SetControlMaster(this))
        {
            mount.Delete();
            return;
        }

        mount.MoveToWorld(Location, Map);
        mount.Rider = this;
    }
}
