using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Mobiles;

namespace SosariaAI.Social;

/// <summary>
/// Talk about what a character is doing right now, from the world scan: a miner grumbles about
/// ore, a smith offers repairs, a traveler complains about the road, and at the game's night
/// someone remarks on the dark. A cheap roll comes first, so a scan that does not talk
/// costs one random number.
/// </summary>
public static class AmbientTalk
{
    public static void Consider(SosariaCharacter character)
    {
        if (character.Combatant != null || !character.Alive)
        {
            return;
        }

        if (Utility.Random(TalkOdds.WorkTalkOdds) == 0 &&
            WorkCategory(character.Routine?.CurrentSkill?.Name) is { } work)
        {
            Talk.Maybe(character, work, TalkOdds.PercentScale, new TalkSlots { Place = TalkWords.Place(character) });
            return;
        }

        // The person's own written lines, only when a keyboard can hear and it is rested:
        // a mutter nobody hears fills the log, not the room.
        if (Utility.Random(TalkOdds.IdleTalkOdds) == 0 &&
            PresenceFocus.PlayerNearby(character) &&
            Talk.RestedNow(character) &&
            PickAmbientLine(character) is { } mutter)
        {
            character.SpeakAloud(mutter);
            Talk.NoteSpoke(character);
            return;
        }

        // Wordless texture: a real Emote(), not a said line.
        if (Utility.Random(TalkOdds.EmoteOdds) == 0)
        {
            Talk.Maybe(character, TalkCategory.Emotes, TalkOdds.PercentScale);
            return;
        }

        if (Utility.Random(TalkOdds.NightTalkOdds) == 0 && Scenes.IsNight(character))
        {
            Talk.Maybe(character, TalkCategory.NightTalk, TalkOdds.PercentScale, new TalkSlots { Town = TalkWords.Town(character) });
        }
    }

    /// <summary>Picks are cheap; a few tries get past the situational lines most personas carry.</summary>
    private const int AmbientTries = 3;

    /// <summary>
    /// A line of the person's persona safe to say unprovoked: generated idle lines carry
    /// situations on paper (a rez plea, an afk, a question for the room) that an ambient
    /// mutter does not have. See <see cref="SpeechFacts.AmbientSafe"/>.
    /// </summary>
    public static string PickAmbientLine(SosariaCharacter character)
    {
        for (var tries = 0; tries < AmbientTries; tries++)
        {
            var line = character.Persona?.PickIdleLine(character.Routine?.CurrentSkill?.Name);

            if (SpeechFacts.AmbientSafe(line))
            {
                return line;
            }
        }

        return null;
    }

    /// <summary>The category a person busy with this skill talks from, or null for a skill with nothing to say.</summary>
    public static string WorkCategory(string skillKind) =>
        skillKind switch
        {
            SkillKinds.Mine => TalkCategory.MiningTalk,
            SkillKinds.Lumberjack => TalkCategory.LumberTalk,
            SkillKinds.Fish => TalkCategory.FishingTalk,
            SkillKinds.Smith => TalkCategory.SmithTalk,
            SkillKinds.Tailor => TalkCategory.TailorTalk,
            SkillKinds.Carpentry => TalkCategory.CarpenterTalk,
            SkillKinds.Tinker or SkillKinds.Alchemy or SkillKinds.Inscription or SkillKinds.Fletch or SkillKinds.Cook =>
                TalkCategory.CraftTalk,
            SkillKinds.Hunt or SkillKinds.Dungeon => TalkCategory.HuntTalk,
            SkillKinds.GoTo or SkillKinds.Travel => TalkCategory.Traveling,
            _ => null
        };
}
