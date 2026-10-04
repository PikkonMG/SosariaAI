using System;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Mobiles;

namespace SosariaAI.Deliberation;

/// <summary>
/// A request to say a trade line in the character's own voice. The Brain answers on the world
/// thread with the reworded line, or with null when it will not or could not; the plain line is
/// then said as written.
/// </summary>
public sealed record TradePhraseCall(SosariaCharacter Speaker, Mobile Listener, string Plain, Action<string> Answer);

/// <summary>
/// How a trader's line reaches the air. The words come from the haggle, never from a model:
/// near a person at a keyboard, and not more than once per <see cref="PhraseCooldown"/>, the
/// Brain may say the line its own way, and a rewording that changes a number is thrown away.
/// Every line waits a typing pause first; an instant answer is the loudest tell there is.
/// </summary>
public static class TradeVoice
{
    public const int PhraseRange = TradeRanges.TalkRange;
    public const int PhraseCooldownSeconds = 20;

    public static readonly TimeSpan PhraseCooldown = TimeSpan.FromSeconds(PhraseCooldownSeconds);

    private static readonly CharacterCooldown _phrased = new();

    /// <summary>
    /// Set by the Brain when a chat provider can reword a line. Returns true when it took the
    /// call and will answer through <see cref="TradePhraseCall.Answer"/>.
    /// </summary>
    public static Func<TradePhraseCall, bool> Phraser { get; set; }

    public static bool MayPhrase(bool listenerIsHuman, bool inRange, bool cooling, bool phraserReady) =>
        listenerIsHuman && inRange && !cooling && phraserReady;

    /// <summary>Says a trade line to <paramref name="listener"/> after a typing pause.</summary>
    public static void Say(SosariaCharacter speaker, Mobile listener, string line)
    {
        if (speaker == null || string.IsNullOrEmpty(line))
        {
            return;
        }

        var now = Core.Now;
        var phraser = Phraser;
        var mayPhrase = MayPhrase(
            People.IsHuman(listener),
            listener != null && speaker.InRange(listener, PhraseRange),
            _phrased.IsCooling(speaker.Serial, now, PhraseCooldown),
            phraser != null
        );

        if (mayPhrase && phraser(new TradePhraseCall(speaker, listener, line, phrased => SpeakNow(speaker, Chosen(line, phrased)))))
        {
            _phrased.Mark(speaker.Serial, now);
            return;
        }

        Timer.StartTimer(ReplyDelay.For(line), () => SpeakNow(speaker, line));
    }

    /// <summary>
    /// The reworded line when it names the same numbers and adds no promise the plain line did
    /// not make (an invite or a trip no code keeps, <see cref="PromiseLines"/>); the plain one
    /// otherwise. The reworded line is said as written, past the speech gate.
    /// </summary>
    public static string Chosen(string plain, string phrased) =>
        TradeLines.SameNumbers(plain, phrased) && (PromiseLines.IsPromise(plain) || !PromiseLines.IsPromise(phrased))
            ? phrased
            : plain;

    public static void SpeakNow(SosariaCharacter speaker, string line)
    {
        if (speaker is { Deleted: false, Alive: true })
        {
            speaker.SpeakScripted(line);
        }
    }
}
