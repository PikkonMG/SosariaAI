using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Skills;

namespace SosariaAI.Deliberation;

/// <summary>
/// Jev picks a character's next job at a big moment when the decision route is a System One
/// provider (every next job with jevScope "all"). The scorer ranks the jobs the rules allow;
/// Jev chooses among the best few from a small word-only state; the character stands until
/// the answer comes. An error, a slow or unsure answer, the hourly token budget, or the rate
/// limit gives the scorer's pick instead. Every Jev call, of every kind, spends one hourly
/// token budget and none of the paid call budget, and has its own in-flight limit with a
/// short queue behind it.
/// </summary>
public static partial class Brain
{
    private static readonly TimeSpan JevUsageCheck = TimeSpan.FromMinutes(1);
    private static readonly JevJobDesk _jobDesk = new();
    private static readonly Dictionary<Serial, DateTime> _lastBankAt = new();
    private static readonly HashSet<long> _jevRequests = new();
    private static JevUsage _jevUsage;
    private static JevRateLimiter _jevLimiter;
    private static int _jevInFlight;

    private static void ConfigureJev()
    {
        _jevUsage = new JevUsage(_settings.JevInputTokensPerHour);
        _jevLimiter = new JevRateLimiter();
    }

    /// <summary>The one Jev transport: every Jev ask shares its rate limiter and usage meter.</summary>
    private static JevWorker CreateJevWorker(ProviderDefinition provider, string key, TimeSpan timeout)
    {
        var paid = IsPaid(provider);
        var floor = JevDecisionRules.Floor(_settings.DecisionMinConfidence, provider.MinConfidence);

        return new JevWorker(
            provider.BaseUrl,
            key ?? string.Empty,
            provider.Model,
            timeout,
            result => Core.LoopContext.Post(() => Apply(result)),
            _settings.JevMaxConcurrentRequests,
            floor,
            limiter: paid ? _jevLimiter : new JevRateLimiter(),
            usage: paid ? _jevUsage : null
        );
    }

    private static void StartJevUsageLog()
    {
        foreach (var worker in _workers.Values)
        {
            if (worker is JevWorker)
            {
                Timer.StartTimer(JevUsageCheck, JevUsageCheck, LogFinishedJevHour);
                return;
            }
        }
    }

    private static void LogFinishedJevHour()
    {
        if (_jevUsage?.TakeFinished(DateTime.UtcNow) is { } hour)
        {
            console.Information(JevUsageHour.LogTemplate, hour.LogArgs);
        }
    }

    private static void LogJevUsageAtShutdown()
    {
        LogFinishedJevHour();

        if (_jevUsage?.Current(DateTime.UtcNow) is { Requests: > 0 } hour)
        {
            console.Information(JevUsageHour.LogTemplate, hour.LogArgs);
        }
    }

    /// <summary>True while this character's next-job question is out and inside the wait.</summary>
    public static bool WaitsForJobChoice(SosariaCharacter character, DateTime now) =>
        character != null && _jobDesk.Waiting(character.Serial, now, JevDecisionRules.AnswerWait);

    /// <summary>Something else took over the character, such as a flight; its open question is dropped.</summary>
    public static void ForgetJobChoice(SosariaCharacter character)
    {
        if (character != null)
        {
            _jobDesk.Forget(character.Serial);
        }
    }

    /// <summary>
    /// Puts the next-job pick to Jev when it is a big moment, or any pick with jevScope "all".
    /// True when the question went out and the character waits. False leaves the pick to the
    /// scorer; <paramref name="source"/> then says why for the log, or is null when Jev is not
    /// the decision route.
    /// </summary>
    public static bool AskJobChoice(
        SosariaCharacter character,
        ScoreResult scored,
        Situation situation,
        DateTime now,
        out string source
    )
    {
        source = null;

        if (character == null || scored == null || _settings == null || !IsEnabled)
        {
            return false;
        }

        if (_jobDesk.TakeExpired(character.Serial))
        {
            source = JevDecisionRules.SlowSource;
            return false;
        }

        var options = JevJobOptions.From(scored);
        var moment = JevDecisionRules.Moment(new MomentFacts(situation.DiedRecently, PlayerNear(character)));
        moment = _jobDesk.NewMoment(character.Serial, moment) ? moment : null;

        if (JevDecisionRules.JobKind(_settings.JevScope == BrainConfiguration.JevScopeAll, moment) is not { } kind)
        {
            source = JevDecisionRules.ScopeSource;
            return false;
        }

        if (!RunningProvider(BrainProviders.DecisionKind, out var providerName, out var provider, out var worker, kind) ||
            !BrainProviders.IsSystemOne(provider))
        {
            return false;
        }

        if (!Trusts(providerName, provider, SystemOneTask.NextJob))
        {
            source = JevDecisionRules.RulesKeepSource(providerName);
            return false;
        }

        var clock = DateTime.UtcNow;
        var paid = IsPaid(provider);
        source = JevDecisionRules.SkipReason(
            options.Count,
            JevDecisionRules.RulesHold(scored, situation.DenTripFirst),
            _jobDesk.CoolingDown(character.Serial, now, TimeSpan.FromSeconds(_settings.JevDecideCooldownSeconds)),
            !paid || JevBudgetRules.Allows(kind, _jevUsage.UsedShare(clock)),
            !paid || _jevLimiter.Allows(clock)
        );

        if (source != null)
        {
            return false;
        }

        if (ProviderPaused(providerName, now) || !JevMaySend(kind, provider, now))
        {
            source = JevDecisionRules.BusySource;
            return false;
        }

        var requestId = ++_nextRequestId;
        var request = new BrainRequest(
            requestId,
            character.Serial,
            Serial.Zero,
            character.Name,
            BrainEventKind.Decide,
            null,
            null,
            providerName,
            CharacterId: character.CharacterId,
            Jev: JevPrompt.BuildNextJob(
                SituationOf(character, situation, scored.Plan, jobEnded: true) with { Moment = moment },
                options,
                PlayerNear(character) && MayGreet(character, now)
            ),
            Ask: JevAsk.Answers,
            JevKind: kind
        );

        if (!Send(worker, request, paid: false, now, BrainProviders.DecisionKind))
        {
            source = JevDecisionRules.BusySource;
            return false;
        }

        _jobDesk.Open(character.Serial, new JevJobAsk(requestId, now, options, providerName));
        TestNoteEnqueue?.Invoke(BrainProviders.DecisionKind, BrainEventKind.Decide);
        return true;
    }

    /// <summary>A real player, not a character, stands within <see cref="JevDecisionRules.PlayerNearTiles"/>.</summary>
    private static bool PlayerNear(SosariaCharacter character)
    {
        if (character.Map == null || character.Map == Map.Internal)
        {
            return false;
        }

        foreach (var mobile in character.Map.GetMobilesInRange(character.Location, JevDecisionRules.PlayerNearTiles))
        {
            if (People.IsHuman(mobile) && mobile.Alive && character.CanSee(mobile))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The one gate every Jev call passes: the hourly budget for its kind, and room in Jev's own
    /// in-flight limit and short queue. Refused calls fall back to the rules.
    /// </summary>
    private static bool JevMaySend(JevKind kind, ProviderDefinition provider, DateTime now)
    {
        if (IsPaid(provider) && !JevBudgetRules.Allows(kind, _jevUsage.UsedShare(DateTime.UtcNow)))
        {
            return false;
        }

        if (JevDecisionRules.HasRoom(_jevInFlight, _settings.JevMaxConcurrentRequests))
        {
            return true;
        }

        RecordDrop(now);
        return false;
    }

    /// <summary>True when calls to this provider count against the budgets: brain.json's countLocalAsPaid, else its own paid flag.</summary>
    private static bool IsPaid(ProviderDefinition provider) =>
        BrainProviders.CountsAsPaid(provider, _settings.Budget?.CountLocalAsPaid == true);

    /// <summary>Counts a request out on the right in-flight limit: Jev's own, or the chat providers'.</summary>
    private static void NoteSent(IBrainWorker worker, long requestId)
    {
        if (worker is JevWorker)
        {
            _jevRequests.Add(requestId);
            _jevInFlight++;
        }
        else
        {
            _inFlight++;
        }
    }

    /// <summary>A request came back, or never went out: its in-flight slot is free again.</summary>
    private static void NoteDone(long requestId)
    {
        if (_jevRequests.Remove(requestId))
        {
            _jevInFlight = Math.Max(0, _jevInFlight - 1);
        }
        else if (_inFlight > 0)
        {
            _inFlight--;
        }
    }

    /// <summary>
    /// A next-job answer: run Jev's pick, or the scorer's when the answer failed or fell below
    /// the floor. False when the result is not an open next-job question.
    /// </summary>
    private static bool TryApplyJobChoice(BrainResult result)
    {
        if (!_jobDesk.TryTake(result.CharacterSerial, result.RequestId, out var ask))
        {
            return false;
        }

        if (World.FindMobile(result.CharacterSerial) is not SosariaCharacter { Deleted: false, Alive: true } character ||
            character.IsGhost || character.Map == null || character.Map == Map.Internal ||
            !character.AwaitsNextSkill)
        {
            return true;
        }

        var verdict = JevDecisionRules.Read(result.Answers, ask.Options, DecisionFloor(ask.ProviderName), result.Error, ask.ProviderName);
        character.CommitJobChoice(verdict.ActionId, verdict.Source);

        if (verdict.Greet)
        {
            ApplyAct(character, ImmediateActs.Greet, mayReplaceJob: false);
        }

        return true;
    }

    /// <summary>The floor a decision answer from this provider must reach: brain.json's, or the provider's own when higher.</summary>
    private static double DecisionFloor(string providerName) =>
        JevDecisionRules.Floor(_settings.DecisionMinConfidence, BrainProviders.Find(_settings, providerName)?.MinConfidence);

    /// <summary>Only when someone is near and the character may greet again does the greet noul ride along.</summary>
    private static bool MayGreet(SosariaCharacter character, DateTime now) =>
        NearbyName(character) != null && _botTalk.SpeakerMayGreet(character.Serial, now, MeetingRules.SpeakerQuiet);

    private static void NoteBankVisit(SosariaCharacter character) => _lastBankAt[character.Serial] = Core.Now;

    /// <summary>
    /// The log source of an event decision's routine: "jev" when the decision route is a System
    /// One provider, "model" for a chat model, and null for the scorer's plain fallback.
    /// </summary>
    private static string DecideSource(bool fromModel, string providerName)
    {
        if (!fromModel)
        {
            return null;
        }

        var provider = BrainProviders.Find(_settings, providerName);
        return BrainProviders.IsSystemOne(provider) ? providerName : GoalLoop.ModelSource;
    }

    /// <summary>
    /// The few facts a Jev decision reads, taken from the character now. The situation and plan
    /// come from the tick's own ranking when there is one.
    /// </summary>
    private static JevSituation SituationOf(
        SosariaCharacter character,
        Situation situation,
        GoalPlan plan,
        bool jobEnded
    )
    {
        situation ??= character.BuildSituation();
        var now = Core.Now;
        var needs = situation.Needs ?? new NeedsSnapshot();
        var ambition = character.CurrentAmbition();
        var doing = ActionDescriptions.Phrase(character.Routine?.CurrentSkill?.Name, character.ActiveActionId);
        var red = PkRules.IsRed(character.Kills);

        return new JevSituation
        {
            Identity = IdentityLine.Brief(character.PersonProfile.Describe(), red),
            Drives = needs.Drives,
            Want = AmbitionRules.Describe(ambition),
            Mood = AmbitionRules.MoodHint(ambition),
            DayPart = needs.DayPart,
            Place = character.LocationDescription,
            InTown = situation.InTownRegion,
            DistanceFromHome = situation.DistanceFromHome,
            Hits = character.Hits,
            HitsMax = character.HitsMax,
            Mana = character.Mana,
            ManaMax = character.ManaMax,
            Gold = situation.Gold,
            PackFill = needs.PackFillFraction,
            GoodsToSell = situation.HasSellGoods,
            SinceHunt = needs.TimeSinceHunt,
            SinceRest = needs.TimeSinceRest,
            SinceBank = _lastBankAt.TryGetValue(character.Serial, out var banked)
                ? NeedsBrain.ElapsedSince(banked, now)
                : TimeSpan.MaxValue,
            ThreatInSight = situation.MustFlee,
            RecentRuns = situation.RecentRuns,
            DiedRecently = situation.DiedRecently,
            Party = GameParty.MemberNames(character),
            PartyForming = needs.PartyForming,
            Doing = jobEnded ? $"just finished: {doing}" : doing,
            Plan = plan is { IsComplete: false } ? plan.CurrentWhy : null,
            Red = red,
            Armed = !red || SpareKit.Armed(character),
            GangAtDen = red ? RedGang.MatesInDen(character) : 0,
            BlueBand = red ? PartyRoads.BandFor(character, now) : null
        };
    }
}
