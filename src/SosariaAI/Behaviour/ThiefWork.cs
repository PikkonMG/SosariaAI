using System;
using System.Collections.Generic;
using Server;
using Server.SkillHandlers;
using Server.Mobiles;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using SosariaAI.Spawning;

namespace SosariaAI.Behaviour;

/// <summary>
/// A thief standing about in a crowd (a bank, a tavern, a shop door) goes to work on it, when
/// the engine lets it lift from people at all: a Thieves Guild member with no murders. It looks
/// the crowd over every few seconds and starts only once it has picked out a mark, the way a
/// thief watched a bank until someone worth it stood clear of the banker. A thief whose lifts
/// failed three times in a row lets the crowd be until the job's rest runs out: 122 lifts
/// started in one night while the job cooled down.
/// The work itself is <see cref="StealSkill"/>; this only decides when a thief starts.
/// </summary>
public static class ThiefWork
{
    private static readonly Dictionary<Serial, DateTime> LastWork = new();
    private static readonly Dictionary<Serial, DateTime> NextLook = new();

    public static bool IsThief(SosariaCharacter character) =>
        character.PersonProfile.Class == PersonClass.Thief || character.NpcGuild == NpcGuild.ThievesGuild;

    /// <summary>The thief looks the crowd over again once its look gap has run out.</summary>
    public static bool LookDue(DateTime nextLookAt, DateTime now) => nextLookAt == default || now >= nextLookAt;

    public static void Consider(SosariaCharacter character)
    {
        if (character.IsPk || !IsThief(character) || character.Combatant != null || !WorldPlay.OpenPvp(character.Map) ||
            !StealRules.MayBegin(character) || !StealRules.MayWorkCrowd(SosariaCharacter.UnderGuards(character)) ||
            !WorldPlay.IsIdle(character) ||
            !StealRules.MayLiftFromPeople(character.NpcGuild == NpcGuild.ThievesGuild, character.Kills, Stealing.SuspendOnMurder) ||
            character.Routine?.CurrentSkill is StealSkill || character.RestsSkill(SkillKinds.Steal))
        {
            return;
        }

        var now = Core.Now;

        if (!TimeRules.Rested(LastWork.GetValueOrDefault(character.Serial), now, StealRules.Rest) ||
            !LookDue(NextLook.GetValueOrDefault(character.Serial), now) ||
            StealMarks.CrowdAround(character) < StealRules.MarkCrowd)
        {
            return;
        }

        NextLook[character.Serial] = now + RedGangRules.Between(StealRules.LookGapMin, StealRules.LookGapMax, Utility.RandomDouble());
        var mark = StealMarks.Find(character, out _);

        if (mark == null)
        {
            return;
        }

        LastWork[character.Serial] = now;
        WorldPlay.StartWork(character, new StealSkill(mark));
        WorldPlay.Log($"{character.Name} works the crowd and marks {mark.Name}");
    }
}
