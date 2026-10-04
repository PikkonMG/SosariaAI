using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;

namespace SosariaAI.Social;

/// <summary>
/// The one way a character says a talk line. <see cref="Say"/> is for a moment that always
/// speaks (a challenge, a plea); <see cref="Maybe"/> is for a moment that speaks now and then:
/// it keeps a rest per person, rolls the category's chance, and rolls far lower where no
/// person at a keyboard can hear. The line goes out through SpeakScripted. No model call.
/// </summary>
public static class Talk
{
    private static readonly Dictionary<Serial, long> RestUntil = new();

    /// <summary>A filled line of the category for the running era, or null when none fits.</summary>
    public static string Line(string category, in TalkSlots slots = default) =>
        Line(category, Utility.Random(int.MaxValue), slots);

    public static string Line(string category, int roll, in TalkSlots slots) =>
        TalkLibrary.Shared.Pick(category, roll, slots, EraBands.Current());

    /// <summary>
    /// Says a line of the category now. False when no line fits or the speaker cannot talk.
    /// When <paramref name="varyIfHeard"/>, a line identical to one just spoken near the
    /// speaker is re-rolled once: two mouths chanting the same template reads scripted.
    /// </summary>
    public static bool Say(SosariaCharacter speaker, string category, in TalkSlots slots = default, bool varyIfHeard = false)
    {
        if (!CanTalk(speaker))
        {
            return false;
        }

        var line = Line(category, slots);

        if (string.IsNullOrEmpty(line))
        {
            return false;
        }

        // CanTalk already proved the map is set.
        if (varyIfHeard && HeardLines.Shared.HeardNear(speaker.Map.MapID, speaker.X, speaker.Y, line, Core.Now))
        {
            var other = Line(category, slots);
            if (!string.IsNullOrEmpty(other))
            {
                line = other;
            }
        }

        speaker.SpeakScripted(line);
        NoteSpoke(speaker);
        return true;
    }

    /// <summary>
    /// Says a line of the category with <paramref name="percent"/> chance, when the speaker has
    /// rested since its last situational line. Unheard, the chance is divided by
    /// <see cref="TalkOdds.UnheardDivisor"/>: the log still reads alive, the shard stays quiet.
    /// </summary>
    public static bool Maybe(SosariaCharacter speaker, string category, int percent, in TalkSlots slots = default)
    {
        if (!CanTalk(speaker) || !Rested(speaker, Core.TickCount))
        {
            return false;
        }

        var chance = TalkOdds.Chance(percent, PresenceFocus.PlayerNearby(speaker));

        return Utility.Random(TalkOdds.PercentScale) < chance && Say(speaker, category, slots);
    }

    /// <summary>
    /// The gesture inside a "*...*" talk line, or null for a spoken line. A wrapped line is
    /// a real emote, not said words.
    /// </summary>
    public static string EmoteBody(string line) =>
        line is { Length: > 2 } && line[0] == '*' && line[^1] == '*'
            ? line.Trim('*', ' ')
            : null;

    /// <summary>Starts the speaker's rest: a scene line counts too.</summary>
    public static void NoteSpoke(SosariaCharacter speaker) =>
        RestUntil[speaker.Serial] = Core.TickCount + TalkOdds.SpeakerRestMs;

    /// <summary>True when the speaker's situational rest has passed.</summary>
    public static bool RestedNow(SosariaCharacter speaker) => Rested(speaker, Core.TickCount);

    private static bool Rested(SosariaCharacter speaker, long now) =>
        !RestUntil.TryGetValue(speaker.Serial, out var until) || now - until >= 0;

    private static bool CanTalk(SosariaCharacter speaker) =>
        speaker is { Deleted: false } && speaker.Map != null && speaker.Map != Map.Internal;
}
