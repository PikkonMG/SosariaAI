using System;
using System.Collections.Generic;
using System.Globalization;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Deliberation;

/// <summary>One option of a pick: its key, what it is for, and when it is the wrong pick.</summary>
public readonly record struct JevPickOption(string Key, string What, string NotFor);

/// <summary>What came of one pick: the option Jev named, or null when the rules keep the pick, and why, for the log.</summary>
public readonly record struct JevPickVerdict(string Choice, double Confidence, string Source);

/// <summary>
/// One choice among the options the rules already allow, put to the Brain's System One
/// provider at a big moment: a small word-only state, one Choice question with a contrastive
/// rubric per option, one request. Code owns the flow: the caller checks its hard rules first,
/// offers only what they allow, and keeps its own rule pick for every doubt (no provider, a
/// refused call, no answer, an unknown option, an answer under the floor, or one that comes too
/// late, see <see cref="JevWait"/>). Every call is booked as a <see cref="JevKind.BigMoment"/>:
/// it spends the one hourly Jev token budget and routes by <c>route.decisionUses.bigMoment</c>.
/// </summary>
public static class JevPick
{
    public const string Question = "pick";

    public const string RulesSource = "rules";
    public const string NoAnswerSource = RulesSource + ": jev gave no answer";
    public const string NoPickSource = RulesSource + ": jev gave no pick";
    public const string SlowSource = RulesSource + ": jev too slow";

    private const string ConfidenceFormat = "0.00";

    /// <summary>The verdict of a question whose answer did not come inside the wait.</summary>
    public static readonly JevPickVerdict TooSlow = new(null, 0, SlowSource);

    /// <summary>Set by the Brain when a System One provider is routed. Returns true when it took the call.</summary>
    internal static Func<JevCall, bool> Asker { get; set; }

    /// <summary>Set by the Brain: the decision floor for the provider that answered.</summary>
    internal static Func<string, double> FloorOf { get; set; }

    /// <summary>True once the Brain has a provider to ask; until then the rules alone pick.</summary>
    public static bool IsAvailable => Asker != null;

    public static JevDecision Build(IReadOnlyDictionary<string, object> state, string instructions, IReadOnlyList<JevPickOption> options)
    {
        var criteria = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < options.Count; i++)
        {
            criteria[options[i].Key] = JevJobOptions.Rubric(options[i].What, options[i].NotFor);
        }

        return new JevDecision(
            state,
            new Dictionary<string, JevQuestion> { [Question] = new(SystemOneApi.ChoiceType, instructions, criteria) }
        );
    }

    /// <summary>
    /// Jev's pick when it names an offered option at or above <paramref name="floor"/>; else no
    /// pick, with the reason. Either way the source names the provider that was asked.
    /// </summary>
    public static JevPickVerdict Read(
        IReadOnlyDictionary<string, SystemOneAnswer> answers,
        IReadOnlyList<JevPickOption> options,
        double floor,
        string providerName = BrainProviders.JevName
    )
    {
        var source = string.IsNullOrWhiteSpace(providerName) ? BrainProviders.JevName : providerName;

        if (answers == null)
        {
            return new JevPickVerdict(null, 0, NoAnswerSource);
        }

        if (!answers.TryGetValue(Question, out var answer) || KeyOf(options, answer?.Choice) is not { } key)
        {
            return new JevPickVerdict(null, 0, NoPickSource);
        }

        return answer.Confidence < floor
            ? new JevPickVerdict(null, answer.Confidence, $"{RulesSource}: {source} unsure {Word(answer.Confidence)}")
            : new JevPickVerdict(key, answer.Confidence, $"{source} {Word(answer.Confidence)}");
    }

    /// <summary>
    /// Puts the pick to the provider. False when none is routed, fewer than
    /// <see cref="JevDecisionRules.MinOptions"/> options leave nothing to choose, or the call
    /// was refused (the budget, the queue); the caller's rule pick stands then. The verdict comes
    /// later on the world thread.
    /// </summary>
    public static bool TryAsk(
        SosariaCharacter character,
        Mobile other,
        BrainEventKind kind,
        JevDecision decision,
        IReadOnlyList<JevPickOption> options,
        Action<JevPickVerdict> onVerdict
    )
    {
        var asker = Asker;

        if (asker == null || character == null || decision == null || onVerdict == null ||
            options == null || options.Count < JevDecisionRules.MinOptions)
        {
            return false;
        }

        return asker(
            new JevCall(
                character,
                other,
                kind,
                JevKind.BigMoment,
                decision,
                (answers, providerName) => onVerdict(Read(answers, options, Floor(providerName), providerName))
            )
        );
    }

    private static double Floor(string providerName) =>
        FloorOf?.Invoke(providerName) ?? BrainConfiguration.DefaultDecisionMinConfidence;

    private static string KeyOf(IReadOnlyList<JevPickOption> options, string choice)
    {
        if (options == null || string.IsNullOrWhiteSpace(choice))
        {
            return null;
        }

        for (var i = 0; i < options.Count; i++)
        {
            if (options[i].Key.Equals(choice.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return options[i].Key;
            }
        }

        return null;
    }

    // The log reads the same on every server culture.
    private static string Word(double confidence) => confidence.ToString(ConfidenceFormat, CultureInfo.InvariantCulture);
}

/// <summary>
/// One pick out to Jev from a rule hook, waited for without holding a tick: the hook asks, reads
/// <see cref="Waiting"/> on its next ticks, and takes the verdict once it is back or overdue
/// (<see cref="JevPick.TooSlow"/>, and the rules pick). An answer to an older question, or one
/// kept past <see cref="AnswerKeep"/> unread, applies to nothing. Game loop only.
/// </summary>
public sealed class JevWait
{
    /// <summary>How long the hook waits for the answer before its rule picks.</summary>
    public static readonly TimeSpan AnswerWait = JevDecisionRules.AnswerWait;

    /// <summary>An answer not read within this long of the ask is stale: the moment it was for has passed.</summary>
    public static readonly TimeSpan AnswerKeep = TimeSpan.FromSeconds(30);

    private long _ticket;
    private DateTime _askedAt;
    private bool _open;
    private JevPickVerdict? _answer;

    /// <summary>
    /// Opens one question and hands <paramref name="send"/> the answer's way back. False when
    /// the question did not go out; nothing is open then.
    /// </summary>
    public bool Ask(DateTime now, Func<Action<JevPickVerdict>, bool> send)
    {
        var ticket = ++_ticket;
        _open = true;
        _askedAt = now;
        _answer = null;

        if (send(verdict => Answer(ticket, verdict)))
        {
            return true;
        }

        Close();
        return false;
    }

    /// <summary>True while a question is out, unanswered, and inside <see cref="AnswerWait"/>.</summary>
    public bool Waiting(DateTime now) => _open && _answer == null && now - _askedAt < AnswerWait;

    /// <summary>
    /// The verdict of the open question once it is back or overdue, and the question closes.
    /// False while it is still inside the wait, when none is open, or when it went stale.
    /// </summary>
    public bool TryTake(DateTime now, out JevPickVerdict verdict)
    {
        verdict = default;

        if (!_open || Waiting(now))
        {
            return false;
        }

        var fresh = now - _askedAt < AnswerKeep;
        verdict = _answer ?? JevPick.TooSlow;
        Close();
        return fresh;
    }

    /// <summary>Drops the open question; a late answer to it applies to nothing.</summary>
    public void Close()
    {
        _open = false;
        _answer = null;
    }

    private void Answer(long ticket, JevPickVerdict verdict)
    {
        if (_open && ticket == _ticket)
        {
            _answer = verdict;
        }
    }
}
