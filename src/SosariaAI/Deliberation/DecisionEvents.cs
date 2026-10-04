using System;
using SosariaAI.Configuration;
using SosariaAI.Skills;

namespace SosariaAI.Deliberation;

/// <summary>
/// Model decision calls are allowed only for these events. Monster combat never qualifies.
/// </summary>
public static class DecisionEvents
{
    public static bool Qualifies(BrainEventKind kind, bool speakerIsPlayer)
    {
        return kind switch
        {
            BrainEventKind.Plan => true,
            BrainEventKind.Attacked => speakerIsPlayer,
            BrainEventKind.DungeonEnded => true,
            BrainEventKind.PlayerNoticed => true,
            _ => false
        };
    }

    public static bool IsChat(BrainEventKind kind) =>
        kind is BrainEventKind.Spoken or BrainEventKind.Musing;

    /// <summary>
    /// A decision's line is spoken only when it cannot promise something the character
    /// then does not do: the model chose nothing, or the job it chose is the one applied.
    /// Dagfinn said "off to despise" and went to practise mace, because a hunt he could
    /// not drop kept him and the line went out anyway. An invite is never spoken: no
    /// decision forms a group with the person it noticed. A trip claim may stay, because
    /// it names the routine the choice applied.
    /// </summary>
    public static bool MaySpeakChoice(string choose, bool choiceApplied, string say) =>
        !PromiseLines.IsInvite(say) && (string.IsNullOrWhiteSpace(choose) || choiceApplied);

    /// <summary>
    /// A plan's line is spoken only when it promises no group, trip or meeting: plan steps
    /// never form a group with a person nearby, and the line goes out as the plan starts,
    /// before any step that could keep a trip claim has run.
    /// </summary>
    public static bool MaySpeakPlan(string say) => !PromiseLines.IsPromise(say);

    /// <summary>
    /// A decision's choice stays applied only when its act left the job as the choice set it.
    /// A go_hunt act after a chosen trip to Minoc sends the character hunting, and the line
    /// about Minoc would then promise a trip no code makes.
    /// </summary>
    public static bool ActKeptChoice(string jobBeforeAct, string jobAfterAct) =>
        string.Equals(jobBeforeAct, jobAfterAct, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Seeing a player is not a reason to drop a dungeon, a hunt or a red's conflict run.
    /// Bayta left Despise at the bank because PlayerNoticed picked hunt, and a red camping
    /// Destard is there for the players it notices.
    /// </summary>
    public static bool MayReplaceJob(BrainEventKind kind, string currentSkillKind)
    {
        if (kind is BrainEventKind.Attacked or BrainEventKind.DungeonEnded)
        {
            return true;
        }

        if (kind == BrainEventKind.Plan)
        {
            return currentSkillKind is not SkillKinds.Flee and not GhostSkill.SkillName;
        }

        if (string.IsNullOrWhiteSpace(currentSkillKind))
        {
            return true;
        }

        return currentSkillKind is not SkillKinds.Dungeon
            and not SkillKinds.Hunt
            and not SkillKinds.Conflict
            and not SkillKinds.Follow
            and not SkillKinds.GoTo
            and not SkillKinds.Flee
            and not SkillKinds.GoHome
            and not SkillKinds.Boat
            and not SkillKinds.VendorSell
            and not SkillKinds.Smith
            and not SkillKinds.Tame
            and not SkillKinds.Steal
            and not SkillKinds.Hide
            and not SkillKinds.Lockpick
            and not SkillKinds.Mount
            and not SkillKinds.Mark
            and not SkillKinds.DetectHidden
            and not SkillKinds.Snoop
            and not SkillKinds.Heal
            and not SkillKinds.Alchemy
            and not SkillKinds.Inscription
            and not SkillKinds.Peace
            and not SkillKinds.Track
            and not SkillKinds.Tailor
            and not SkillKinds.Poison
            and not SkillKinds.Carpentry
            and not SkillKinds.Cook
            and not SkillKinds.Fletch
            and not SkillKinds.Tinker
            and not SkillKinds.Meditate
            and not SkillKinds.Cartography
            and not SkillKinds.Lore
            and not SkillKinds.Vet
            and not SkillKinds.Spirit
            and not SkillKinds.Anatomy
            and not SkillKinds.EvalInt
            and not SkillKinds.ArmsLore
            and not SkillKinds.ItemId
            and not SkillKinds.Discord
            and not SkillKinds.Provoke
            and not SkillKinds.Forensic
            and not SkillKinds.Beg
            and not SkillKinds.Camp
            and not SkillKinds.RemoveTrap
            and not SkillKinds.Stealth
            and not SkillKinds.Taste
            and not SkillKinds.Wrestle
            and not SkillKinds.Tactics
            and not SkillKinds.Parry
            and not SkillKinds.Mage
            and not SkillKinds.Resist
            and not SkillKinds.Sword
            and not SkillKinds.Fence
            and not SkillKinds.Archery
            and not SkillKinds.Mace
            and not SkillKinds.Herd
            and not SkillKinds.Music
            and not SkillKinds.Necro
            and not SkillKinds.Gate;
    }
}
