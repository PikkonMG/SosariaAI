using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Skill = SosariaAI.Skills.Skill;
using SosariaAI.Logging;

namespace SosariaAI.Deliberation;

/// <summary>
/// Deliberation layer. Policy runs on the game loop. HTTP runs on per-provider
/// <see cref="BrainWorker"/> instances. No timer calls the model. The needs brain
/// is free and always on. Memory lives on the character and is saved with the world.
/// </summary>
public static partial class Brain
{
    public const int HearRangeMultiplier = 2;

    /// <summary>
    /// How far ordinary speech carries in the game. A player this far away still sees the
    /// words, so this is the range that decides whether anyone would hear a character talk.
    /// The plugin's own <see cref="HearRange"/> is smaller and decides who replies.
    /// </summary>
    public const int SpeechCarryRange = 15;
    public const string CapReachedLog =
        "Brain cap reached ({Paid} paid calls this hour, {Day} today); {Name} used the needs brain";

    private static readonly ILogger logger = SosariaLog.For(typeof(Brain));
    private static readonly ILogger console = SosariaLog.Console(typeof(Brain));
    private static readonly CharacterCooldown _cooldown = new();
    private static readonly CharacterCooldown _noticeCooldown = new();
    private static readonly BotConversationTracker _botTalk = new();
    private static readonly AttentionGate _attention = new();
    private static readonly Dictionary<string, IBrainWorker> _workers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ProviderFailureGate _providerFailures = new();
    private static readonly ProviderSpeed _speed = new();
    private static readonly Dictionary<Serial, BrainEvent> _pendingPlayerLines = new();
    private static readonly Dictionary<long, BrainEvent> _pendingGates = new();
    private static readonly Dictionary<long, BrainEvent> _pendingIntents = new();
    private static readonly Dictionary<long, TradePhraseCall> _pendingPhrases = new();
    private static readonly Dictionary<long, JevCall> _pendingJevCalls = new();
    private static readonly HashSet<Serial> _chatInFlight = new();
    private static DateTime _lastPlanEnqueue;

    private static BrainConfiguration _settings;
    private static CallBudget _budget;
    private static long _nextRequestId;
    private static int _inFlight;
    private static DateTime _lastDropLog;
    private static DateTime _lastCapLog;
    private static int _dropsSinceLog;

    internal static Action<string, BrainEventKind> TestNoteEnqueue { get; set; }

    public static bool IsEnabled { get; private set; }

    public static int HearRange => _settings?.HearRange ?? BrainConfiguration.DefaultHearRange;

    public static int ReplyHearRange => ReplyRangeTiles(HearRange);

    public static int ReplyRangeTiles(int hearRange) => hearRange * HearRangeMultiplier;

    public static TimeSpan BotToBotCooldown =>
        TimeSpan.FromMinutes(_settings?.BotToBotCooldownMinutes ?? BrainConfiguration.DefaultBotToBotCooldownMinutes);

    public static TimeSpan BotToBotPairRest =>
        TimeSpan.FromMinutes(_settings?.BotToBotPairRestMinutes ?? BrainConfiguration.DefaultBotToBotPairRestMinutes);

    public static int BotToBotMaxExchanges =>
        _settings?.BotToBotMaxExchanges ?? BrainConfiguration.DefaultBotToBotMaxExchanges;

    public static TimeSpan ChatHold =>
        TimeSpan.FromSeconds(_settings?.AttentionWindowSeconds ?? BrainConfiguration.DefaultAttentionWindowSeconds);

    public static BotConversationTracker Conversations => _botTalk;

    public static PersonaCatalog Personas { get; private set; }

    public static PersonaPartCatalog PersonaParts { get; private set; }

    public static void Configure()
    {
        _settings = BrainFile.LoadOrCreate(BrainFile.DefaultPath);
        Personas = PersonasFile.LoadOrCreate(PersonasFile.DefaultDirectory);
        PersonaParts = PersonaPartsFile.LoadOrCreate(PersonaPartsFile.DefaultDirectory);
        PersonaWriter.Configure(
            PersonasGeneratedFile.DefaultDirectory,
            _settings.PersonaWritesPerMinute,
            _settings.PersonaWriter
        );
        _budget = new CallBudget(
            _settings.Budget?.MaxPaidCallsPerDay ?? CallBudget.DefaultMaxPaidCallsPerDay,
            _settings.Budget?.MaxPaidCallsPerHour ?? CallBudget.DefaultMaxPaidCallsPerHour
        );
        ConfigureJev();

        if (!_settings.Enabled)
        {
            IsEnabled = false;
            console.Information("Brain is off: enabled is false in brain.json");
            return;
        }

        IsEnabled = true;
        EventSink.Shutdown += Shutdown;
    }

    public static void Initialize()
    {
        if (_settings is not { Enabled: true })
        {
            return;
        }

        Func<string, string> env = Environment.GetEnvironmentVariable;

        foreach (var name in BrainProviders.UsedNames(_settings))
        {
            var provider = BrainProviders.Find(_settings, name);

            if (provider == null)
            {
                logger.Warning("Unknown brain provider {Name} in the route; that call kind is disabled", name);
                continue;
            }

            var key = BrainProviders.ResolveApiKey(provider, env);

            if (!BrainProviders.IsUsable(provider, key))
            {
                logger.Warning(
                    "Brain provider {Name} has no key and is not a local endpoint; that provider is disabled",
                    name
                );
                continue;
            }

            var timeout = TimeSpan.FromSeconds(Math.Max(1, provider.TimeoutSeconds));
            IBrainWorker worker = BrainProviders.IsSystemOne(provider)
                ? CreateJevWorker(provider, key, timeout)
                : new BrainWorker(
                    provider.BaseUrl,
                    key ?? string.Empty,
                    provider.Model,
                    _settings.Temperature,
                    _settings.MaxReplyCharacters,
                    timeout,
                    result => Core.LoopContext.Post(() => Apply(result)),
                    _settings.MaxConcurrentRequests,
                    provider.Thinking,
                    provider.ReasoningEffort
                );
            worker.Start();
            _workers[name] = worker;
            console.Information("Brain provider {Name} is on: model {Model} at {BaseUrl}", name, provider.Model, provider.BaseUrl);

            if (BrainProviders.IsSystemOne(provider))
            {
                LogTrust(name, provider);
            }
        }

        IsEnabled = _workers.Count > 0;

        if (!IsEnabled)
        {
            console.Information("Brain model is off: no usable provider");
            return;
        }

        TradeVoice.Phraser = PhraseTrade;
        TradeIntentJev.Asker = AskJev;
        CombatStanceJev.Asker = AskJev;
        JevPick.Asker = AskJev;
        JevPick.FloorOf = DecisionFloor;
        StartJevUsageLog();

        Timer.StartTimer(TimeSpan.FromSeconds(1), AskForMissingPlans);
    }

    private static void AskForMissingPlans()
    {
        if (!IsEnabled)
        {
            return;
        }

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is SosariaCharacter { Deleted: false } character &&
                character.Map != null &&
                character.Map != Map.Internal)
            {
                RequestPlan(character, PlanTrigger.NoPlan);
            }
        }
    }

    public static void Shutdown()
    {
        foreach (var worker in _workers.Values)
        {
            worker.Dispose();
        }

        _workers.Clear();
        _providerFailures.Clear();
        LogJevUsageAtShutdown();
        EventSink.Shutdown -= Shutdown;
        MemoryStore.Shared.Flush();
    }

    public static void Watch(Routine routine)
    {
        if (routine == null)
        {
            return;
        }

        routine.SkillEnded += OnSkillEnded;
        routine.SkillStarted += OnSkillStarted;
    }

    public static void Consider(BrainEvent evt)
    {
        if (evt == null || _settings == null)
        {
            return;
        }

        if (evt.Kind == BrainEventKind.GoalEnded)
        {
            Remember(evt.CharacterSerial, evt.Text);
            return;
        }

        if (DecisionEvents.Qualifies(evt.Kind, evt.SpeakerIsPlayer))
        {
            TryDecision(evt);
            return;
        }

        if (!IsEnabled || !DecisionEvents.IsChat(evt.Kind))
        {
            return;
        }

        TryChat(evt);
    }

    public static void Apply(BrainResult result)
    {
        if (result == null)
        {
            return;
        }

        NoteDone(result.RequestId);

        if (result.Kind == BrainEventKind.Spoken && result.Ask == JevAsk.Plain)
        {
            _chatInFlight.Remove(result.CharacterSerial);
        }

        ApplyResult(result);
        ReleasePendingLines(Core.Now);
    }

    private static void ApplyResult(BrainResult result)
    {
        _speed.Record(result.ProviderName, result.LatencyMilliseconds);

        if (!string.IsNullOrEmpty(result.Error))
        {
            logger.Warning(
                "Brain request {Id} for {Name} failed after {Latency} ms: {Error}",
                result.RequestId,
                result.CharacterName,
                result.LatencyMilliseconds,
                result.Error
            );

            if (result.Ask == JevAsk.Plain)
            {
                FinishSpokenWait(result);
            }

            NoteProviderFailure(result.ProviderName);

            if (AnswerWaitingCall(result))
            {
                return;
            }

            if (result.Kind == BrainEventKind.Plan &&
                World.FindMobile(result.CharacterSerial) is SosariaCharacter failedPlan)
            {
                ApplyPlan(failedPlan, result);
                return;
            }

            if (DecisionEvents.Qualifies(result.Kind, speakerIsPlayer: true) &&
                World.FindMobile(result.CharacterSerial) is SosariaCharacter failed)
            {
                ApplyNeeds(failed);
            }

            return;
        }

        _providerFailures.Succeeded(result.ProviderName, Core.Now);

        if (AnswerWaitingCall(result))
        {
            return;
        }

        var character = World.FindMobile(result.CharacterSerial) as SosariaCharacter;

        if (character == null || character.Deleted || !character.Alive)
        {
            LogSpokenOutcome(result, character, speaker: null, discarded: true, distance: -1);
            return;
        }

        FinishSpokenWait(result, character);

        if (result.Kind == BrainEventKind.Plan)
        {
            ApplyPlan(character, result);
            return;
        }

        if (DecisionEvents.Qualifies(result.Kind, speakerIsPlayer: true))
        {
            LogSpokenOutcome(result, character, speaker: null, discarded: false, distance: 0);
            ApplyDecide(character, result);
            return;
        }

        if (result.Kind == BrainEventKind.Musing)
        {
            LogSpokenOutcome(result, character, speaker: null, discarded: false, distance: 0);

            if (string.IsNullOrEmpty(result.Say) ||
                SpokenRepeat.IsNearRepeat(result.Say, character.RecentSpeech()))
            {
                return;
            }

            if (SosariaSettings.LogActivity)
            {
                logger.Information(
                    "{Name} mused ({Event}): {Say}",
                    character.Name,
                    character.LastMusingEvent,
                    result.Say
                );
            }

            SpeakLater(character.Serial, result.Say);
            return;
        }

        var speaker = World.FindMobile(result.SpeakerSerial);

        if (speaker == null || speaker.Deleted)
        {
            LogSpokenOutcome(result, character, speaker: null, discarded: true, distance: -1);
            return;
        }

        var distance = NavMetric.Chebyshev(character.Location, speaker.Location);

        // Party chat carries any distance. Spoken words must still be in earshot.
        if (!result.FromParty && !character.InRange(speaker, ReplyHearRange))
        {
            LogSpokenOutcome(result, character, speaker, discarded: true, distance);
            return;
        }

        Remember(result.CharacterSerial, MemoryLineForReply(result, speaker.Name));

        if (result.Kind == BrainEventKind.Spoken)
        {
            _attention.Open(result.CharacterSerial, result.SpeakerSerial, Core.Now, AttentionWindow);
        }

        LogSpokenOutcome(result, character, speaker, discarded: false, distance);

        if (string.IsNullOrEmpty(result.Say))
        {
            return;
        }

        var say = result.Say;
        var characterSerial = result.CharacterSerial;
        var speakerSerial = result.SpeakerSerial;

        if (result.FromParty)
        {
            Timer.StartTimer(ReplyDelay.For(say), () => ChatReply(characterSerial, say));
            return;
        }

        Timer.StartTimer(ReplyDelay.For(say), () => SpeakReply(characterSerial, speakerSerial, say));
    }

    private static bool ProviderPaused(string providerName, DateTime now) =>
        _providerFailures.IsPaused(providerName, now);

    private static void NoteProviderFailure(string providerName)
    {
        if (_providerFailures.Failed(providerName, Core.Now, _settings.FailuresBeforePause,
                _settings.PauseAfterFailuresSeconds))
        {
            logger.Warning(
                "Brain provider {Name} paused for {Seconds} seconds after {Count} consecutive failures",
                providerName,
                _settings.PauseAfterFailuresSeconds,
                _settings.FailuresBeforePause
            );
        }
    }

    /// <summary>
    /// A result someone is waiting on rather than a character's own chat or plan: the speech
    /// gate, a player-line intent, a trade line to reword, or a caller's own typed questions (a
    /// haggle read, a fight stance). A failure answers the caller with nothing, so it falls back
    /// to its own rule or written line.
    /// </summary>
    private static bool AnswerWaitingCall(BrainResult result)
    {
        if (PersonaWriter.Receive(result))
        {
            return true;
        }

        var failed = !string.IsNullOrEmpty(result.Error);

        switch (result.Ask)
        {
            case JevAsk.Gate:
                {
                    ApplyGate(result);
                    return true;
                }
            case JevAsk.Intent:
                {
                    ApplyIntent(result);
                    return true;
                }
            case JevAsk.Answers:
                {
                    if (TryApplyJobChoice(result))
                    {
                        return true;
                    }

                    if (_pendingJevCalls.Remove(result.RequestId, out var ask))
                    {
                        ask.Answer(failed ? null : result.Answers, result.ProviderName);
                    }

                    return true;
                }
        }

        if (!_pendingPhrases.Remove(result.RequestId, out var phrase))
        {
            return false;
        }

        phrase.Answer(failed || string.IsNullOrWhiteSpace(result.Say) ? null : result.Say);
        return true;
    }

    /// <summary>
    /// A trader's line said in its own voice near a player: one chat call against the same
    /// budget and per-character cooldown as any chat. False leaves the plain line as written.
    /// </summary>
    private static bool PhraseTrade(TradePhraseCall call)
    {
        if (call?.Speaker == null || string.IsNullOrWhiteSpace(call.Plain) || _settings == null)
        {
            return false;
        }

        var now = Core.Now;

        if (_cooldown.IsCooling(call.Speaker.Serial, now, TimeSpan.FromSeconds(_settings.PerCharacterCooldownSeconds)))
        {
            return false;
        }

        var requestId = TryEnqueue(Create(BrainEventKind.Reword, call.Speaker, call.Listener, call.Plain), BrainProviders.ChatKind, now);

        if (requestId <= 0)
        {
            return false;
        }

        _cooldown.Mark(call.Speaker.Serial, now);
        _pendingPhrases[requestId] = call;
        return true;
    }

    /// <summary>True when a chat provider that writes text is running this boot.</summary>
    internal static bool HasChatWriter =>
        IsEnabled && RunningProvider(BrainProviders.ChatKind, out _, out var provider, out _) &&
        !BrainProviders.IsSystemOne(provider);

    /// <summary>A persona write may go out now: the chat provider is free, not paused, and within the paid caps.</summary>
    internal static bool PersonaWriteReady(DateTime now) =>
        ProviderReady(BrainProviders.ChatKind, now) && _inFlight < _settings.MaxConcurrentRequests;

    /// <summary>
    /// Sends the one request for a copy's own persona to the chat provider, on the normal
    /// channel so player chat goes first. Returns the request id, or 0 when it could not go out.
    /// </summary>
    internal static long RequestPersonaWrite(SosariaCharacter character, string systemMessage, string userMessage)
    {
        var now = Core.Now;

        if (character == null ||
            !TryResolveProvider(BrainProviders.ChatKind, now, character.Name, JevKind.Decision, SystemOneTask.NextJob, out var providerName, out var provider, out var worker, out var paid) ||
            BrainProviders.IsSystemOne(provider))
        {
            return 0;
        }

        var request = new BrainRequest(
            ++_nextRequestId,
            character.Serial,
            Serial.Zero,
            character.Name,
            BrainEventKind.PersonaWrite,
            systemMessage,
            userMessage,
            providerName,
            CharacterId: character.CharacterId
        );

        if (!Send(worker, request, paid, now, BrainProviders.ChatKind))
        {
            return 0;
        }

        TestNoteEnqueue?.Invoke(BrainProviders.ChatKind, BrainEventKind.PersonaWrite);
        return request.RequestId;
    }

    /// <summary>A caller's own typed questions put to the decision route, when that route is a System One provider.</summary>
    private static bool AskJev(JevCall call)
    {
        if (call?.Character == null || call.Decision == null || _settings == null)
        {
            return false;
        }

        var now = Core.Now;

        if (!TryResolveProvider(BrainProviders.DecisionKind, now, call.Character.Name, call.Use,
                SystemOneFamilies.TaskOf(call.Use, JevAsk.Answers), out var providerName, out var provider, out var worker, out var paid) ||
            !BrainProviders.IsSystemOne(provider))
        {
            return false;
        }

        var requestId = ++_nextRequestId;
        var request = new BrainRequest(
            requestId,
            call.Character.Serial,
            call.Other?.Serial ?? Serial.Zero,
            call.Character.Name,
            call.Kind,
            null,
            null,
            providerName,
            HighPriority: true,
            CharacterId: call.Character.CharacterId,
            Jev: call.Decision,
            Ask: JevAsk.Answers,
            JevKind: call.Use
        );

        if (!Send(worker, request, paid, now, BrainProviders.DecisionKind))
        {
            return false;
        }

        _pendingJevCalls[requestId] = call;
        return true;
    }

    private static void ChatReply(Serial characterSerial, string say)
    {
        if (World.FindMobile(characterSerial) is SosariaCharacter { Deleted: false, Alive: true } character)
        {
            GameParty.Chat(character, say);
        }
    }

    private static void FinishSpokenWait(BrainResult result, SosariaCharacter character = null)
    {
        if (result.Kind != BrainEventKind.Spoken)
        {
            return;
        }

        character ??= World.FindMobile(result.CharacterSerial) as SosariaCharacter;
        character?.Conversation.HeardReply(Core.Now, AttentionWindow);
    }

    private static void LogSpokenOutcome(
        BrainResult result,
        SosariaCharacter character,
        Mobile speaker,
        bool discarded,
        int distance
    )
    {
        if (!SosariaSettings.LogActivity)
        {
            return;
        }

        var name = character?.Name ?? result.CharacterName;
        var say = result.Say ?? string.Empty;

        if (!discarded)
        {
            logger.Information(
                "{Name} {Kind} latency {Latency} ms{Cost} reply: {Say}",
                name,
                result.Kind,
                result.LatencyMilliseconds,
                result.InputTokens > 0 ? $" ({result.InputTokens} tok)" : string.Empty,
                say
            );
            return;
        }

        if (character == null || character.Deleted || !character.Alive)
        {
            logger.Information(
                "{Name} answered too late: the character is gone, so nobody heard \"{Say}\" ({Latency} ms)",
                name,
                say,
                result.LatencyMilliseconds
            );
            return;
        }

        if (speaker == null || speaker.Deleted)
        {
            logger.Information(
                "{Name} answered too late: the other person is gone, so nobody heard \"{Say}\" ({Latency} ms)",
                name,
                say,
                result.LatencyMilliseconds
            );
            return;
        }

        logger.Information(
            "{Name} answered too late: {Speaker} is {Distance} tiles away, so nobody heard \"{Say}\" ({Latency} ms)",
            name,
            speaker.Name,
            distance,
            say,
            result.LatencyMilliseconds
        );
    }

    public static BrainEvent Spoken(SosariaCharacter character, Mobile speaker, string text) =>
        Create(BrainEventKind.Spoken, character, speaker, text);

    public static void HearParty(SosariaCharacter character, Mobile speaker, string text)
    {
        if (character == null || speaker == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _attention.Open(character.Serial, speaker.Serial, Core.Now, ChatHold);
        Consider(Spoken(character, speaker, text) with { FromParty = true });
    }

    public static BrainEvent Attacked(SosariaCharacter character, Mobile attacker) =>
        Create(BrainEventKind.Attacked, character, attacker, text: ChoicesText(character));

    public static void RequestDecide(SosariaCharacter character)
    {
        if (character == null)
        {
            return;
        }

        RequestPlan(character, PlanTrigger.NoPlan);
    }

    public static bool RequestPlan(SosariaCharacter character, PlanTrigger trigger)
    {
        if (character == null || _settings == null)
        {
            return false;
        }

        var now = Core.Now;
        var talking = character.Conversation.IsActive(now);
        var playerNearby = talking || PlayerInEarshot(character);
        var cooldown = TimeSpan.FromMinutes(
            _settings.DecideCooldownMinutes
        );
        var providerName = BrainProviders.ProviderNameForKind(_settings, PlanningPath.CallKind);
        var paused = ProviderPaused(providerName, now);
        var budgetOk = true;
        var provider = BrainProviders.Find(_settings, providerName);

        if (provider != null)
        {
            var paid = IsPaid(provider);
            budgetOk = !paid || (_budget?.CanSpend(now) ?? false);
        }

        if (!character.Planning.ShouldAsk(
                trigger,
                now,
                cooldown,
                talking,
                _lastPlanEnqueue,
                paused || !IsEnabled,
                budgetOk,
                playerNearby
            ))
        {
            return false;
        }

        var evt = Create(BrainEventKind.Plan, character, speaker: null, text: trigger.ToString());

        if (TryEnqueue(evt, PlanningPath.CallKind, now) <= 0)
        {
            return false;
        }

        _lastPlanEnqueue = now;
        character.Planning.MarkEnqueued(_nextRequestId, now);

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} asked for a plan ({Trigger})", character.Name, trigger);
        }

        return true;
    }

    private static void SchedulePlanRetry(SosariaCharacter character, TimeSpan delay)
    {
        if (character == null || delay <= TimeSpan.Zero)
        {
            return;
        }

        var serial = character.Serial;
        Timer.StartTimer(delay, () =>
        {
            if (World.FindMobile(serial) is not SosariaCharacter living || living.Deleted)
            {
                return;
            }

            if (living.Map == null || living.Map == Map.Internal || !living.Alive)
            {
                return;
            }

            if (PlanControl.OwnsOrdinaryWork(living.Planning.Plan, Core.Now))
            {
                return;
            }

            RequestPlan(living, PlanTrigger.Retry);
        });
    }

    /// <summary>
    /// Asks the model for something the character would say about a new event. Returns false when
    /// no request went out, so the caller can fall back to a written line instead of
    /// leaving the character silent.
    /// </summary>
    public static bool RequestMusing(SosariaCharacter character, string eventText)
    {
        if (!IsEnabled || character == null || character.Deleted || !character.Alive ||
            character.Map == null || character.Map == Map.Internal ||
            string.IsNullOrWhiteSpace(eventText))
        {
            return false;
        }

        var now = Core.Now;

        if (ProviderPaused(BrainProviders.ProviderNameForKind(_settings, BrainProviders.ChatKind), now) ||
            IsTalkingToPlayer(character.Serial, now) ||
            character.Conversation.IsActive(now) ||
            _cooldown.IsCooling(character.Serial, now, TimeSpan.FromSeconds(_settings.PerCharacterCooldownSeconds)))
        {
            return false;
        }

        // Paid speech is for a player. Other Sosaria people use written lines.
        if (!PlayerInEarshot(character))
        {
            return false;
        }

        return StartChat(Create(BrainEventKind.Musing, character, speaker: null, text: eventText), now);
    }

    private static bool PlayerInEarshot(SosariaCharacter character)
    {
        if (character?.Map == null || character.Map == Map.Internal)
        {
            return false;
        }

        foreach (var mobile in character.Map.GetMobilesInRange(character.Location, SpeechCarryRange))
        {
            if (mobile.Alive && People.IsHuman(mobile) && character.CanSee(mobile))
            {
                return true;
            }
        }

        return false;
    }

    public static void NoticeNearbyPlayer(SosariaCharacter character)
    {
        if (!IsEnabled || character == null || character.Deleted || !character.Alive ||
            character.Map == null || character.Map == Map.Internal)
        {
            return;
        }

        if (character.Conversation.IsActive(Core.Now))
        {
            return;
        }

        var now = Core.Now;
        var notice = TimeSpan.FromMinutes(
            _settings?.NearbyPlayerNoticeMinutes ?? BrainConfiguration.DefaultNearbyPlayerNoticeMinutes
        );

        if (_noticeCooldown.IsCooling(character.Serial, now, notice))
        {
            return;
        }

        var player = FindIdlePlayer(character);

        if (player == null)
        {
            return;
        }

        _noticeCooldown.Mark(character.Serial, now);
        Consider(Create(BrainEventKind.PlayerNoticed, character, player, ChoicesText(character)));
    }

    private static void TryDecision(BrainEvent evt)
    {
        var character = World.FindMobile(evt.CharacterSerial) as SosariaCharacter;

        if (character == null)
        {
            return;
        }

        var now = Core.Now;

        if (character.Conversation.IsActive(now))
        {
            if (SosariaSettings.LogActivity)
            {
                logger.Information(
                    "{Name} skipped a {Kind} decision because they are talking to a player",
                    character.Name,
                    evt.Kind
                );
            }

            return;
        }

        if (TryEnqueue(evt, BrainProviders.DecisionKind, now) <= 0)
        {
            ApplyNeeds(character);
        }
    }

    private static void TryChat(BrainEvent evt)
    {
        var now = Core.Now;
        ReleasePendingLines(now);

        // Only a player's words come through here; one character hearing another goes
        // through HearCharacter with its own caps. Words inside a haggle belong to the trade.
        if (!evt.SpeakerIsPlayer || InHaggle(evt) ||
            (evt.Kind == BrainEventKind.Spoken && !PassesAttentionGate(evt, now)))
        {
            return;
        }

        AnswerPlayer(evt, now);
    }

    // A player is never dropped. A line spoken while the character still answers the last one
    // waits its turn.
    private static void AnswerPlayer(BrainEvent evt, DateTime now)
    {
        HoldForPlayer(evt.CharacterSerial, evt.SpeakerSerial, now);

        if (_chatInFlight.Contains(evt.CharacterSerial) || !StartChat(evt, now))
        {
            _pendingPlayerLines[evt.CharacterSerial] = evt;
        }
    }

    /// <summary>
    /// A player spoke near the character. True when the model path took the line: the line is
    /// aimed at the character (its name or an open attention window) and a chat provider can
    /// answer now. An unclear line is first read by Jev so the right handler runs. False leaves
    /// the line to the free speech floor: room chatter, a bare name call, an insult, or no
    /// model to spend. Trade talk that opened a haggle never reaches here.
    /// </summary>
    public static bool TakesPlayerLine(SosariaCharacter character, Mobile speaker, string text, SpeechIntentKind intent)
    {
        if (character == null || speaker == null || _settings == null || intent == SpeechIntentKind.Insult)
        {
            return false;
        }

        var now = Core.Now;

        if (!ProviderReady(BrainProviders.ChatKind, now))
        {
            return false;
        }

        ReleasePendingLines(now);
        var evt = Spoken(character, speaker, text);

        // A bare name opens the attention window and turns the character; the floor answers it.
        if (!PassesAttentionGate(evt, now))
        {
            return false;
        }

        if (intent == SpeechIntentKind.Other && TryIntent(evt, now))
        {
            return true;
        }

        AnswerPlayer(evt, now);
        return true;
    }

    /// <summary>
    /// One character heard another. With a player in earshot and a free (local) chat model,
    /// the listener's answer is generated, behind the speech gate and the character-to-character
    /// caps. False means no model is spent and the caller answers with a written line.
    /// </summary>
    public static bool HearCharacter(SosariaCharacter listener, SosariaCharacter speaker, string line)
    {
        if (listener == null || speaker == null || string.IsNullOrWhiteSpace(line) || _settings == null)
        {
            return false;
        }

        var now = Core.Now;

        if (!BanterReady(now) || !PlayerInEarshot(listener) || IsTalkingToPlayer(listener.Serial, now) ||
            _cooldown.IsCooling(listener.Serial, now, BotToBotCooldown) ||
            !_botTalk.CanGreet(listener.Serial, speaker.Serial, now, TimeSpan.Zero, BotToBotPairRest, BotToBotMaxExchanges))
        {
            return false;
        }

        var evt = Spoken(listener, speaker, line);
        _attention.Open(listener.Serial, speaker.Serial, now, AttentionWindow);

        // The line passes a cheap semantic gate before it costs a generated reply.
        if (_settings.SpeechGate && TryGate(evt, now))
        {
            return true;
        }

        if (!StartChat(evt, now))
        {
            return false;
        }

        _botTalk.Record(listener.Serial, speaker.Serial, now, BotToBotPairRest);
        listener.Planning.Diag.NoteChat();
        return true;
    }

    private static bool InHaggle(BrainEvent evt) =>
        World.FindMobile(evt.CharacterSerial) is SosariaCharacter character &&
        TradeSessions.IsTrading(character, World.FindMobile(evt.SpeakerSerial));

    private static bool TryIntent(BrainEvent evt, DateTime now)
    {
        var requestId = TryEnqueue(evt, BrainProviders.DecisionKind, now, JevAsk.Intent);

        if (requestId <= 0)
        {
            return false;
        }

        _pendingIntents[requestId] = evt;
        HoldForPlayer(evt.CharacterSerial, evt.SpeakerSerial, now);
        return true;
    }

    /// <summary>
    /// Jev read the player's line. A group call goes to the party board, a trade line stays with
    /// the haggle the trade handler opened for it, an insult gets the floor's brush-off, and
    /// anything else, a trade line that opened no haggle too, gets a generated reply. A failed read falls open to a reply.
    /// </summary>
    private static void ApplyIntent(BrainResult result)
    {
        if (!_pendingIntents.Remove(result.RequestId, out var evt))
        {
            return;
        }

        var intent = string.IsNullOrEmpty(result.Error) ? JevPrompt.ParseIntent(result.Choose) : SpeechIntentKind.Other;

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} read {Speaker}'s words as {Intent} ({Tokens} tok)",
                evt.CharacterName,
                evt.SpeakerName,
                intent,
                result.InputTokens
            );
        }

        if (World.FindMobile(evt.CharacterSerial) is not SosariaCharacter { Deleted: false, Alive: true } character ||
            World.FindMobile(evt.SpeakerSerial) is not { Deleted: false } speaker)
        {
            return;
        }

        switch (intent)
        {
            case SpeechIntentKind.Party when LfgBoard.HearPlayer(character, speaker, evt.Text):
            case SpeechIntentKind.Trade when TradeSessions.IsTrading(character, speaker):
                {
                    character.Conversation.HeardReply(Core.Now, AttentionWindow);
                    return;
                }
            case SpeechIntentKind.Insult:
                {
                    SpeechResponder.Answer(character, speaker, evt.Text, intent, named: true);
                    return;
                }
        }

        AnswerPlayer(evt, Core.Now);
    }

    private static bool TryGate(BrainEvent evt, DateTime now)
    {
        var requestId = TryEnqueue(evt, BrainProviders.DecisionKind, now, JevAsk.Gate);

        if (requestId <= 0)
        {
            return false;
        }

        _pendingGates[requestId] = evt;
        _cooldown.Mark(evt.CharacterSerial, now);
        return true;
    }

    /// <summary>
    /// The gate answered: at or over the threshold the line earns a generated reply;
    /// under it the character lets the words pass, the way a person ignores chatter
    /// that was not meant for them. A failed gate falls open so a Jev outage never
    /// mutes the shard.
    /// </summary>
    private static void ApplyGate(BrainResult result)
    {
        if (!_pendingGates.Remove(result.RequestId, out var evt))
        {
            return;
        }

        var answer = result.Error != null || result.Gate >= _settings.SpeechGateThreshold;

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                answer
                    ? "{Name} will answer {Speaker} (gate {Gate:0.00}, {Tokens} tok)"
                    : "{Name} let {Speaker}'s words pass (gate {Gate:0.00}, {Tokens} tok)",
                result.CharacterName,
                evt.SpeakerName,
                result.Gate,
                result.InputTokens
            );
        }

        if (!answer ||
            World.FindMobile(evt.CharacterSerial) is not SosariaCharacter { Deleted: false, Alive: true })
        {
            return;
        }

        if (StartChat(evt, Core.Now))
        {
            _botTalk.Record(evt.CharacterSerial, evt.SpeakerSerial, Core.Now, BotToBotPairRest);

            if (World.FindMobile(evt.CharacterSerial) is SosariaCharacter chatter)
            {
                chatter.Planning.Diag.NoteChat();
            }
        }
    }

    private static bool StartChat(BrainEvent evt, DateTime now)
    {
        if (TryEnqueue(evt, BrainProviders.ChatKind, now) <= 0)
        {
            return false;
        }

        _cooldown.Mark(evt.CharacterSerial, now);

        if (evt.SpeakerIsPlayer)
        {
            _chatInFlight.Add(evt.CharacterSerial);

            if (World.FindMobile(evt.CharacterSerial) is SosariaCharacter character)
            {
                character.Conversation.WaitForReply(evt.SpeakerSerial, now, ConversationHold.ReplyWaitCeiling);
                character.Planning.Diag.NoteChat();
            }
        }

        return true;
    }

    // A waiting player line goes out as soon as the character is free. It expires with the
    // attention window, so a line from a player who left is not answered to an empty street.
    private static void ReleasePendingLines(DateTime now)
    {
        if (_pendingPlayerLines.Count == 0)
        {
            return;
        }

        List<Serial> finished = null;

        foreach (var (serial, evt) in _pendingPlayerLines)
        {
            var expired = now - evt.When > AttentionWindow;

            if (expired || !_chatInFlight.Contains(serial) && StartChat(evt, now))
            {
                (finished ??= []).Add(serial);
            }
        }

        if (finished == null)
        {
            return;
        }

        for (var i = 0; i < finished.Count; i++)
        {
            _pendingPlayerLines.Remove(finished[i]);
        }
    }

    private static void HoldForPlayer(Serial characterSerial, Serial playerSerial, DateTime now)
    {
        if (World.FindMobile(characterSerial) is SosariaCharacter character)
        {
            character.Conversation.Hold(playerSerial, now, AttentionWindow);
        }
    }

    private static bool IsTalkingToPlayer(Serial characterSerial, DateTime now) =>
        World.FindMobile(characterSerial) is SosariaCharacter character && character.Conversation.IsActive(now);

    /// <summary>A provider on this call kind is running and could take a call now: not paused, and within budget when paid.</summary>
    private static bool ProviderReady(string callKind, DateTime now)
    {
        if (!IsEnabled || !RunningProvider(callKind, out var providerName, out var provider, out _) ||
            ProviderPaused(providerName, now) ||
            BrainProviders.IsSystemOne(provider))
        {
            return false;
        }

        return !IsPaid(provider) ||
               (_budget?.CanSpend(now) ?? false);
    }

    /// <summary>The provider routed for this call kind, when its worker is running.</summary>
    private static bool RunningProvider(string callKind, out string providerName, out ProviderDefinition provider, out IBrainWorker worker,
        JevKind? decisionUse = null)
    {
        worker = null;
        providerName = BrainProviders.ProviderNameForKind(_settings, callKind,
            decisionUse is { } use ? SystemOneRoute.Key(use) : null);
        provider = BrainProviders.Find(_settings, providerName);
        return provider != null && _workers.TryGetValue(providerName, out worker);
    }

    /// <summary>Character-to-character chat may only use a free local model, never a paid one.</summary>
    private static bool BanterReady(DateTime now)
    {
        if (!ProviderReady(BrainProviders.ChatKind, now))
        {
            return false;
        }

        var provider = BrainProviders.Find(_settings, BrainProviders.ProviderNameForKind(_settings, BrainProviders.ChatKind));
        return !IsPaid(provider);
    }

    /// <summary>Returns the request id on success, 0 when the event could not go out.</summary>
    private static long TryEnqueue(BrainEvent evt, string callKind, DateTime now, JevAsk ask = JevAsk.Plain)
    {
        // A Jev call here is a big moment (an event decision, a player's words) or the speech gate.
        var jevKind = ask == JevAsk.Gate ? JevKind.SpeechGate : JevKind.BigMoment;

        if (!TryResolveProvider(callKind, now, evt.CharacterName, jevKind, SystemOneFamilies.TaskOf(jevKind, ask),
                out var providerName, out var provider, out var worker, out var paid, ChatMayHelp(callKind, evt.Kind, ask)))
        {
            return 0;
        }

        var systemOne = BrainProviders.IsSystemOne(provider);

        // A gate and an intent are typed answers, not chat text; only a System One provider gives them.
        if (ask != JevAsk.Plain && !systemOne)
        {
            return 0;
        }

        var persona = ResolvePersona(evt.CharacterSerial);
        var memory = MemoryFor(evt.CharacterSerial);
        var living = World.FindMobile(evt.CharacterSerial) as SosariaCharacter;
        BrainRequest request;
        var requestId = ++_nextRequestId;

        if (systemOne)
        {
            JevDecision jev;

            if (ask == JevAsk.Gate)
            {
                jev = JevPrompt.BuildGate(persona, evt, LifeOf(living, evt));
            }
            else if (ask == JevAsk.Intent)
            {
                jev = JevPrompt.BuildIntent(persona, evt);
            }
            else
            {
                // Jev answers typed questions only: decision events with routines to pick
                // between. Speech, musing, and plan text stay with generative providers.
                if (!PromptBuilder.UsesDecideShape(evt.Kind) ||
                    !JevPrompt.HasOptions(living?.Definition?.Choices))
                {
                    return 0;
                }

                jev = JevPrompt.Build(SituationOf(living, null, living.CurrentPlan, jobEnded: false), evt, living.Definition.Choices);
            }

            request = new BrainRequest(
                requestId,
                evt.CharacterSerial,
                evt.SpeakerSerial,
                evt.CharacterName,
                evt.Kind,
                null,
                null,
                providerName,
                evt.SpeakerIsPlayer && evt.Kind == BrainEventKind.Spoken,
                evt.FromParty,
                living?.CharacterId,
                living?.Planning.Plan?.Revision ?? 0,
                jev,
                ask,
                jevKind
            );
        }
        else
        {
            var messages = PromptBuilder.Build(
                persona,
                evt,
                memory,
                _settings.MaxReplyCharacters,
                LifeOf(living, evt),
                evt.Kind == BrainEventKind.Plan ? FactsOf(living, evt) : null
            );
            request = new BrainRequest(
                requestId,
                evt.CharacterSerial,
                evt.SpeakerSerial,
                evt.CharacterName,
                evt.Kind,
                messages[0].Content,
                messages[1].Content,
                providerName,
                evt.SpeakerIsPlayer && evt.Kind is BrainEventKind.Spoken or BrainEventKind.Reword,
                evt.FromParty,
                living?.CharacterId,
                living?.Planning.Plan?.Revision ?? 0
            );
        }

        if (!Send(worker, request, paid, now, callKind))
        {
            return 0;
        }

        TestNoteEnqueue?.Invoke(callKind, evt.Kind);
        return requestId;
    }

    /// <summary>
    /// The running provider on this call kind, when a call may go out now: not paused, and for a
    /// chat provider within the paid caps and its in-flight limit. A System One provider spends
    /// only the one Jev token budget, by <paramref name="jevKind"/>, and has its own in-flight limit.
    /// </summary>
    private static bool TryResolveProvider(
        string callKind,
        DateTime now,
        string characterName,
        JevKind jevKind,
        SystemOneTask task,
        out string providerName,
        out ProviderDefinition provider,
        out IBrainWorker worker,
        out bool paid,
        bool chatMayHelp = false
    )
    {
        paid = false;

        if (!RunningProvider(callKind, out providerName, out provider, out worker, jevKind) ||
            !IsEnabled || ProviderPaused(providerName, now))
        {
            if (providerName != null && provider == null)
            {
                logger.Warning("Unknown brain provider {Name} in the route; that call kind is disabled", providerName);
            }

            return false;
        }

        if (BrainProviders.IsSystemOne(provider))
        {
            if (Trusts(providerName, provider, task))
            {
                return JevMaySend(jevKind, provider, now);
            }

            // The model leaves this to the rules. A fast chat provider may decide it instead.
            return chatMayHelp && task == SystemOneTask.NextJob &&
                   ChatHelps(now, characterName, jevKind, out providerName, out provider, out worker, out paid);
        }

        paid = IsPaid(provider);

        if (paid && !_budget.CanSpend(now))
        {
            LogCap(now, characterName);
            return false;
        }

        if (_inFlight >= _settings.MaxConcurrentRequests)
        {
            RecordDrop(now);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Chat help covers only a decision event: never a chat call (the chat route asking itself
    /// for help would recurse without end), never a typed ask, and never a plan, which the rules
    /// make whenever the decision route is a System One provider.
    /// </summary>
    public static bool ChatMayHelp(string callKind, BrainEventKind kind, JevAsk ask) =>
        string.Equals(callKind, BrainProviders.DecisionKind, StringComparison.OrdinalIgnoreCase) &&
        ask == JevAsk.Plain && kind != BrainEventKind.Plan;

    /// <summary>
    /// The chat provider takes an event decision the System One model is not trusted with, when
    /// help is on, the chat route is a running chat provider inside its budget, and its replies
    /// come back inside <see cref="BrainConfiguration.ChatHelpMaxSeconds"/> on average.
    /// </summary>
    private static bool ChatHelps(
        DateTime now,
        string characterName,
        JevKind jevKind,
        out string providerName,
        out ProviderDefinition provider,
        out IBrainWorker worker,
        out bool paid
    ) =>
        TryResolveProvider(BrainProviders.ChatKind, now, characterName, jevKind, SystemOneTask.NextJob,
            out providerName, out provider, out worker, out paid) &&
        !BrainProviders.IsSystemOne(provider) &&
        _settings.ChatHelpMaxSeconds > 0 &&
        _speed.IsFastEnough(providerName, _settings.ChatHelpMaxSeconds * (double)CombatBrain.MillisecondsPerSecond);

    /// <summary>True when the System One model behind this provider is trusted with the task; else the rules answer.</summary>
    private static bool Trusts(string providerName, ProviderDefinition provider, SystemOneTask task) =>
        SystemOneFamilies.Handles(SystemOneFamilies.Detect(providerName, provider), task);

    private static void LogTrust(string name, ProviderDefinition provider)
    {
        var family = SystemOneFamilies.Detect(name, provider);
        var rules = SystemOneFamilies.RulesWords(family);

        var helper = BrainProviders.ProviderNameForKind(_settings, BrainProviders.ChatKind);
        var helps = rules.Length > 0 && _settings.ChatHelpMaxSeconds > 0 &&
                    BrainProviders.Find(_settings, helper) is { } chat && !BrainProviders.IsSystemOne(chat);

        if (helps)
        {
            console.Information(
                "Brain provider {Name} leaves next jobs to the rules; {Helper} decides big moments while it answers within {Seconds} s",
                name,
                helper,
                _settings.ChatHelpMaxSeconds
            );
        }

        if (rules.Length == 0)
        {
            console.Information("Brain provider {Name} runs {Family}: it may decide {Tasks}", name, family, SystemOneFamilies.HandledWords(family));
            return;
        }

        console.Information(
            "Brain provider {Name} runs {Family}: it may decide {Tasks}; the rules keep {Rules}",
            name,
            family,
            SystemOneFamilies.HandledWords(family),
            rules
        );
    }

    private static bool Send(IBrainWorker worker, BrainRequest request, bool paid, DateTime now, string callKind)
    {
        NoteSent(worker, request.RequestId);

        if (!worker.TryEnqueue(request))
        {
            NoteDone(request.RequestId);
            logger.Warning("Brain dropped a {Kind} request because the worker queue was full", callKind);
            return false;
        }

        if (paid)
        {
            _budget.Record(now);
        }

        return true;
    }

    private static void ApplyNeeds(SosariaCharacter character) => character.ScoreAndCommit();

    private static void LogCap(DateTime now, string name)
    {
        var window = TimeSpan.FromHours(1);

        if (_lastCapLog != default && now - _lastCapLog < window)
        {
            return;
        }

        _lastCapLog = now;
        logger.Information(CapReachedLog, _budget.PaidCallsThisHour, _budget.PaidCallsToday, name);
    }

    private static void OnSkillStarted(SosariaCharacter character, Skill skill)
    {
        if (character == null || skill == null)
        {
            return;
        }

        if (skill.Name is SkillKinds.Hunt or SkillKinds.Dungeon)
        {
            character.BeginHuntTrip(Core.Now);
        }
        else if (skill.Name == SkillKinds.Rest)
        {
            character.LastRestAt = Core.Now;
        }
        else if (skill.Name is SkillKinds.IdleWander or SkillKinds.Tavern or SkillKinds.Visit
                 or SkillKinds.Sightsee or SkillKinds.Loiter)
        {
            character.LastTownAt = Core.Now;
        }
    }

    private static void OnSkillEnded(SosariaCharacter character, Skill skill, SkillStatus status)
    {
        if (character == null || skill == null)
        {
            return;
        }

        if (skill.Name is SkillKinds.Hunt or SkillKinds.Dungeon)
        {
            LfgBoard.TripEnded(character, skill);
        }

        if (skill.Name == SkillKinds.Dungeon)
        {
            // A trip refused at the door is not a dungeon ending: no "finished a
            // dungeon" event and no paid model call for it.
            if (skill is DungeonTripSkill { ReachedInside: true })
            {
                Consider(Create(BrainEventKind.DungeonEnded, character, speaker: null, text: ChoicesText(character)));
            }

            return;
        }

        if (skill.Name is SkillKinds.BankDeposit or SkillKinds.VendorSell)
        {
            character.TickAmbition();
            NoteBankVisit(character);
        }

        if (skill.Name == SkillKinds.Sightsee)
        {
            character.MarkPlace(character.LocationDescription);
        }

        if (skill.Name is not SkillKinds.Lumberjack and not SkillKinds.Mine and not SkillKinds.Fish
            and not SkillKinds.BankDeposit)
        {
            return;
        }

        var detail = GoalMemory(skill, status);
        var evt = Create(BrainEventKind.GoalEnded, character, speaker: null, text: detail);
        Consider(evt);
    }

    private static BrainEvent Create(BrainEventKind kind, SosariaCharacter character, Mobile speaker, string text)
    {
        return new BrainEvent(
            kind,
            character.Serial,
            character.Name,
            speaker?.Name,
            speaker?.Serial ?? Serial.Zero,
            People.IsHuman(speaker),
            text,
            character.CurrentActivity,
            character.LocationDescription,
            Core.Now,
            Identity: IdentityOf(character)
        );
    }

    private static string IdentityOf(SosariaCharacter character)
    {
        var profile = character.PersonProfile;
        return IdentityLine.For(
            profile.Describe(),
            profile.Traits,
            profile.Wealth,
            JobRules.TryParse(character.ActiveGoalTarget, out var job) ? JobRules.NameOf(job) : null,
            character.Guild?.Abbreviation,
            PkRules.IsRed(character.Kills)
        );
    }

    public static NeedsSnapshot SnapshotOf(SosariaCharacter character) => Snapshot(character);

    private static NeedsSnapshot Snapshot(SosariaCharacter character)
    {
        var now = Core.Now;
        var party = Party.FindByMember(character.CharacterId);
        var maxWeight = character.MaxWeight;
        return new NeedsSnapshot
        {
            HitsFraction = character.HitsMax <= 0 ? 1 : (double)character.Hits / character.HitsMax,
            GoldBanked = character.BankBox?.GetAmount(typeof(Gold)) ?? 0,
            GoldCarried = character.Backpack?.GetAmount(typeof(Gold)) ?? 0,
            PackFillFraction = maxWeight <= 0 ? 0 : (double)character.TotalWeight / maxWeight,
            Power = CharacterPower.For(character),
            HealthyPower = CharacterPower.Healthy(character),
            TimeSinceHunt = NeedsBrain.ElapsedSince(character.LastHuntAt, now),
            TimeSinceRest = NeedsBrain.ElapsedSince(character.LastRestAt, now),
            TimeSinceTown = NeedsBrain.ElapsedSince(character.LastTownAt, now),
            PartyForming = party is { IsGathering: true } || party is { TripActive: true },
            Drives = character.Persona?.ResolvedDrives() ?? PersonaDrives.Neutral,
            DayPart = character.DayPartAt(now).ToString(),
            AmbitionWantsWork = AmbitionRules.PrefersWork(character.CurrentAmbition()),
            AmbitionWantsHunt = AmbitionRules.PrefersHunt(character.CurrentAmbition()),
            AmbitionWantsTravel = AmbitionRules.PrefersTravel(character.CurrentAmbition()),
            AlliesPower = Party.AlliesPower(character),
            BlockedRoutine = character.FailedRoutineId,
            BlockedCount = character.FailedRoutineCount,
            CanShop = (character.BankBox?.GetAmount(typeof(Gold)) ?? 0) +
                      (character.Backpack?.GetAmount(typeof(Gold)) ?? 0) >
                      SosariaSettings.GearGoldReserve
        };
    }

    private static LifePrompt LifeOf(SosariaCharacter character, BrainEvent evt)
    {
        if (character == null)
        {
            return null;
        }

        character.EnsureLife();
        var ambition = character.CurrentAmbition();
        var now = Core.Now;
        var selfId = Recall.IdOf(character);
        var speaker = evt == null ? null : World.FindMobile(evt.SpeakerSerial);
        var speakerId = speaker == character ? null : Recall.IdOf(speaker);
        var bond = MemoryStore.Shared.BondOf(selfId, speakerId);
        var tone = bond == null
            ? Recall.PlainTone
            : $"{Recall.Tone(bond.Score)} ({bond.LastReason})";

        return new LifePrompt
        {
            Ambition = AmbitionRules.Describe(ambition),
            AmbitionMood = AmbitionRules.MoodHint(ambition),
            DayPart = character.DayPartAt(now).ToString(),
            Opinion = tone,
            RecentSpeech = character.RecentSpeech(),
            ActionPhrase = ActionDescriptions.Phrase(
                character.Routine?.CurrentSkill?.Name,
                character.ActiveActionId
            ),
            PackCount = WorkGoodCount(character),
            Gold = character.Backpack?.GetAmount(typeof(Gold)) ?? 0,
            Hits = character.Hits,
            HitsMax = character.HitsMax,
            PartyState = GameParty.MemberNames(character) ?? "none",
            DistanceFromHome = HomeLeash.DistanceFromHome(character.Location, character.HomeSpot),
            Days = Recall.Days(MemoryStore.Shared, selfId, now, PlanFacts.MaxMemory),
            SharedAdventures = Recall.SharedFacts(MemoryStore.Shared, selfId, speakerId, now, PlanFacts.MaxShared)
        };
    }

    private static int WorkGoodCount(SosariaCharacter character)
    {
        var pack = character.Backpack;

        if (pack == null)
        {
            return 0;
        }

        var count = 0;

        foreach (var item in pack.Items)
        {
            if (HarvestPack.IsHarvest(item))
            {
                count += item.Amount;
            }
        }

        return count;
    }

    private static int Seed(SosariaCharacter character) =>
        character.JobSeed(Core.Now);

    private static Mobile FindIdlePlayer(SosariaCharacter character)
    {
        foreach (var mobile in character.Map.GetMobilesInRange(character.Location, HearRange))
        {
            if (People.IsHuman(mobile) && mobile is { Deleted: false, Alive: true, Combatant: null } &&
                People.Perceives(character, mobile))
            {
                return mobile;
            }
        }

        return null;
    }

    private static void SpeakReply(Serial characterSerial, Serial speakerSerial, string say)
    {
        var character = World.FindMobile(characterSerial) as SosariaCharacter;

        if (character == null || character.Deleted || !character.Alive)
        {
            return;
        }

        var speaker = World.FindMobile(speakerSerial);

        // A speaker who hid while the answer was typed is no longer there to answer.
        if (speaker == null || speaker.Deleted || !People.Perceives(character, speaker))
        {
            return;
        }

        if (!character.InRange(speaker, ReplyHearRange))
        {
            if (SosariaSettings.LogActivity)
            {
                var distance = NavMetric.Chebyshev(character.Location, speaker.Location);
                logger.Information(
                    "{Name} answered too late: {Speaker} is {Distance} tiles away, so nobody heard \"{Say}\"",
                    character.Name,
                    speaker.Name,
                    distance,
                    say
                );
            }

            return;
        }

        character.Direction = character.GetDirectionTo(speaker);
        character.SpeakAloud(say);
        character.Conversation.Hold(speakerSerial, Core.Now, AttentionWindow);

        if (speaker is SosariaCharacter other)
        {
            other.Conversation.Hold(character.Serial, Core.Now, AttentionWindow);
        }
    }

    private static TimeSpan AttentionWindow => TimeSpan.FromSeconds(_settings.AttentionWindowSeconds);

    private static bool PassesAttentionGate(BrainEvent evt, DateTime now)
    {
        if (AttentionGate.IsOnlyName(evt.Text, evt.CharacterName))
        {
            _attention.Open(evt.CharacterSerial, evt.SpeakerSerial, now, AttentionWindow);
            FaceSpeaker(evt.CharacterSerial, evt.SpeakerSerial);

            if (evt.SpeakerIsPlayer)
            {
                HoldForPlayer(evt.CharacterSerial, evt.SpeakerSerial, now);
            }

            return false;
        }

        return _attention.ShouldListen(evt.CharacterSerial, evt.CharacterName, evt.SpeakerSerial, evt.Text, now, AttentionWindow);
    }

    private static void FaceSpeaker(Serial characterSerial, Serial speakerSerial)
    {
        if (World.FindMobile(characterSerial) is SosariaCharacter character && World.FindMobile(speakerSerial) is { Deleted: false } speaker)
        {
            character.Direction = character.GetDirectionTo(speaker);
        }
    }

    private static Persona ResolvePersona(Serial serial)
    {
        if (World.FindMobile(serial) is SosariaCharacter character)
        {
            return character.Persona ?? Personas?.Neutral ?? Persona.CreateNeutral();
        }

        return Personas?.Neutral ?? Persona.CreateNeutral();
    }

    private static IReadOnlyList<string> MemoryFor(Serial serial) =>
        World.FindMobile(serial) is SosariaCharacter character
            ? character.Memory.Working.Thoughts()
            : [];

    private static void Remember(Serial serial, string line)
    {
        if (World.FindMobile(serial) is SosariaCharacter character)
        {
            character.Remember(line);
        }
    }

    private static string MemoryLineForReply(BrainResult result, string speakerName)
    {
        var who = string.IsNullOrEmpty(speakerName) ? "someone" : speakerName;

        if (result.Kind == BrainEventKind.Attacked)
        {
            return string.IsNullOrEmpty(result.Say)
                ? $"{who} attacked me."
                : $"{who} attacked me. I said: {result.Say}";
        }

        return string.IsNullOrEmpty(result.Say)
            ? $"{who} spoke to me."
            : $"{who} asked me something. I said: {result.Say}";
    }

    private static string GoalMemory(Skill skill, SkillStatus status)
    {
        if (skill is LumberjackSkill lumberjack)
        {
            return status == SkillStatus.Done
                ? $"I chopped wood and now carry {lumberjack.ResourceCount} logs."
                : "I stopped chopping wood.";
        }

        if (skill is MineSkill mine)
        {
            return status == SkillStatus.Done
                ? $"I mined ore and now carry {mine.ResourceCount} pieces."
                : "I stopped mining.";
        }

        if (skill is FishSkill fish)
        {
            return status == SkillStatus.Done
                ? $"I caught fish and now carry {fish.ResourceCount}."
                : "I stopped fishing.";
        }

        if (skill is BankDepositSkill bank)
        {
            return status == SkillStatus.Done
                ? $"I banked {bank.ItemsDeposited} items."
                : "I could not finish at the bank.";
        }

        if (skill is VendorSellSkill sell)
        {
            return status == SkillStatus.Done && sell.ItemsSold > 0
                ? $"I sold goods to {sell.VendorName ?? "a vendor"} for {sell.GoldTaken} gold."
                : "I could not sell my goods.";
        }

        return $"I finished {skill.Name}.";
    }

    private static void ApplyPlan(SosariaCharacter character, BrainResult result)
    {
        var timedOut = !string.IsNullOrEmpty(result.Error);
        var catalog = NavWorld.DestinationsFor(character.HomeFacet);
        var candidates = ActionCatalog.From(character.Definition, catalog, character.HuntHomeNow());
        var skills = GoalPlanRules.SkillKindsFrom(candidates);
        var facts = FactsOf(character, null);
        var offWorld = character.Map == null || character.Map == Map.Internal;
        var context = new PlanAcceptContext(
            character.CharacterId,
            character.Planning.Plan?.Revision ?? 0,
            !character.Deleted,
            offWorld,
            Core.Now,
            skills,
            facts.Destinations,
            facts.People,
            facts.Items,
            Core.Expansion
        );
        var accepted = character.Planning.Deliver(
            result.Raw,
            result.RequestId,
            character.CharacterId,
            context,
            timedOut
        );
        character.SaveModelPlan();

        if (!accepted.Accepted)
        {
            if (timedOut)
            {
                SchedulePlanRetry(character, PlanRequestRules.Backoff(character.Planning.ConsecutiveFailures));
            }

            if (!character.IsGhost && !offWorld)
            {
                ApplyNeeds(character);
            }

            return;
        }

        if (!string.IsNullOrEmpty(result.Say) &&
            DecisionEvents.MaySpeakPlan(result.Say) &&
            !SpokenRepeat.IsNearRepeat(result.Say, character.RecentSpeech()))
        {
            SpeakLater(character.Serial, result.Say);
        }

        if (character.IsGhost || offWorld)
        {
            return;
        }

        character.ScoreAndCommit();
    }

    private static PlanFacts FactsOf(SosariaCharacter character, BrainEvent evt)
    {
        if (character == null)
        {
            return new PlanFacts();
        }

        var catalog = NavWorld.DestinationsFor(character.HomeFacet);
        var candidates = ActionCatalog.From(character.Definition, catalog, character.HuntHomeNow());
        var skills = GoalPlanRules.SkillKindsFrom(candidates);
        var destinations = new List<string>();

        if (catalog?.All != null)
        {
            for (var i = 0; i < catalog.All.Count && destinations.Count < PlanFacts.MaxList; i++)
            {
                if (!string.IsNullOrWhiteSpace(catalog.All[i].Name))
                {
                    destinations.Add(catalog.All[i].Name);
                }
            }
        }

        var people = new List<string>();
        var selfId = Recall.IdOf(character);
        var friend = MemoryChoiceRules.FriendName(MemoryStore.Shared, selfId);
        var foe = MemoryChoiceRules.EnemyName(MemoryStore.Shared, selfId);

        if (!string.IsNullOrWhiteSpace(friend))
        {
            people.Add(friend);
        }

        if (!string.IsNullOrWhiteSpace(foe))
        {
            people.Add(foe);
        }

        var party = GameParty.MemberNames(character);

        if (!string.IsNullOrWhiteSpace(party))
        {
            people.Add(party);
        }

        var items = new List<string>();

        if (character.Build?.Kit != null)
        {
            for (var i = 0; i < character.Build.Kit.Count && items.Count < PlanFacts.MaxList; i++)
            {
                items.Add(character.Build.Kit[i]);
            }
        }

        items.Add("armor");
        items.Add("hatchet");
        items.Add("pickaxe");

        var world = character.CaptureFacts();
        var plan = character.Planning.Plan;
        return new PlanFacts
        {
            Role = (character.Build?.Role ?? CharacterRole.Worker).ToString(),
            Drives = FormatDrives(character.Persona?.ResolvedDrives() ?? PersonaDrives.Neutral),
            SupportedActions = PlanFacts.Bound(skills, PlanFacts.MaxActions),
            BankGold = world.BankGold,
            Goods = world.Goods,
            Tools = world.Tools,
            ArmorEquipped = world.ArmorEquipped,
            InDanger = character.MustRunFrom(HuntSkill.SightThreat(character)),
            IsGhost = character.IsGhost,
            InCombat = character.Combatant != null,
            Place = character.LocationDescription,
            Facet = character.HomeFacet,
            Expansion = Core.Expansion.ToString(),
            Destinations = PlanFacts.Bound(destinations, PlanFacts.MaxList),
            People = PlanFacts.Bound(people, PlanFacts.MaxList),
            Items = PlanFacts.Bound(items, PlanFacts.MaxList),
            Memories = PlanFacts.MemoriesFrom(
                Recall.Latest(MemoryStore.Shared, selfId, Core.Now, PlanFacts.MaxMemory),
                character.Memory.Working.Thoughts()
            ),
            RecentFailures = PlanFacts.Bound(
                string.IsNullOrWhiteSpace(character.FailedRoutineId) ? [] : [character.FailedRoutineId],
                PlanFacts.MaxFailures
            ),
            Promises = string.IsNullOrWhiteSpace(character.InviteAskPlayer)
                ? []
                : ["invite:" + character.InviteAskPlayer],
            CurrentPlan = plan == null ? string.Empty : plan.Goal + " (" + plan.State + ")",
            CurrentStep = plan?.CurrentSkill,
            Trigger = evt?.Text ?? character.Planning.Diag.LastTrigger.ToString()
        };
    }

    private static string FormatDrives(PersonaDrives drives) =>
        "greed " + drives.Greed + ", caution " + drives.Caution + ", valor " + drives.Valor;

    private static void ApplyDecide(SosariaCharacter character, BrainResult result)
    {
        var choices = character.Definition?.Choices;
        var snap = Snapshot(character);
        var catalog = NavWorld.DestinationsFor(character.HomeFacet);
        var power = PowerForRoutine(DecideChoice.CanonicalChoose(result.Choose, choices), snap);
        var fromModel = DecideChoice.ResolveWithinPower(result.Choose, choices, power, catalog);

        if (fromModel == null && !string.IsNullOrWhiteSpace(result.Choose) && SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} was told to {Routine} but is not strong enough (power {Power})",
                character.Name,
                result.Choose,
                power
            );
        }

        var previous = character.ActiveActionId;
        var currentSkill = ActionId.SkillKindOf(previous);
        var mayReplace = DecisionEvents.MayReplaceJob(result.Kind, currentSkill);
        var chosen = fromModel
                     ?? NeedsBrain.Pick(choices, snap, Seed(character), Utility.Random, catalog);

        if (mayReplace)
        {
            character.ApplyChosenRoutine(chosen, fromModel: fromModel != null,
                DecideSource(fromModel != null, result.ProviderName));
        }

        if (!string.IsNullOrWhiteSpace(character.ActiveActionId) &&
            !character.ActiveActionId.Equals(previous, StringComparison.OrdinalIgnoreCase))
        {
            Remember(character.Serial, ActionDescriptions.Started(
                ActionId.SkillKindOf(character.ActiveActionId),
                character.ActiveActionId
            ));
        }

        var jobBeforeAct = character.ActiveActionId;
        ApplyAct(character, ImmediateActs.Normalize(result.Act), mayReplace);
        var choiceApplied = mayReplace && fromModel != null &&
                            DecisionEvents.ActKeptChoice(jobBeforeAct, character.ActiveActionId);

        if (DecisionEvents.MaySpeakChoice(result.Choose, choiceApplied, result.Say) &&
            !string.IsNullOrEmpty(result.Say) &&
            !SpokenRepeat.IsNearRepeat(result.Say, character.RecentSpeech()))
        {
            SpeakLater(character.Serial, result.Say);
        }
    }

    private static void SpeakLater(Serial serial, string say)
    {
        Timer.StartTimer(ReplyDelay.For(say), () =>
        {
            if (World.FindMobile(serial) is SosariaCharacter living && living.Alive &&
                !SpokenRepeat.IsNearRepeat(say, living.RecentSpeech()))
            {
                living.SpeakAloud(say);
            }
        });
    }

    /// <summary>
    /// Carries out a decide answer's act, one of <see cref="ImmediateActs.AllowList"/>: greet
    /// says a greeting; go_hunt and go_town switch to the hunt or town routine when the job may
    /// be replaced and the character is strong enough for it.
    /// </summary>
    private static void ApplyAct(SosariaCharacter character, string act, bool mayReplaceJob)
    {
        if (act == ImmediateActs.Greet)
        {
            var now = Core.Now;
            var atBank = MeetingRules.InBankQuiet(character.Location, CharactersFile.DefaultBankSpot);

            if (!_botTalk.SpeakerMayGreet(character.Serial, now, MeetingRules.SpeakerQuiet))
            {
                return;
            }

            if (atBank && !_botTalk.AreaMayGreet(MeetingRules.BankArea, now, MeetingRules.BankGap))
            {
                return;
            }

            var who = NearbyName(character);
            var line = GreetingLines.Pick(
                0,
                who,
                character.Persona?.PickGreeting(who),
                unchecked((int)character.Serial.Value)
            );

            if (Meeting.ShouldSpeak(line, character.RecentSpeech()))
            {
                _botTalk.RecordSpeakerGreet(character.Serial, now);

                if (atBank)
                {
                    _botTalk.RecordAreaGreet(MeetingRules.BankArea, now);
                }

                character.SpeakAloud(line);
            }

            return;
        }

        if (act is not (ImmediateActs.GoHunt or ImmediateActs.GoTown) || !mayReplaceJob)
        {
            return;
        }

        var choices = character.Definition?.Choices;
        var routine = DecideChoice.CanonicalChoose(act, choices);
        var snap = Snapshot(character);
        var catalog = NavWorld.DestinationsFor(character.HomeFacet);
        var power = PowerForRoutine(routine, snap);

        if (!string.IsNullOrWhiteSpace(routine) &&
            DecideChoice.ResolveWithinPower(routine, choices, power, catalog) != null)
        {
            character.ApplyChosenRoutine(routine, fromModel: true);
        }
    }

    private static int PowerForRoutine(string routineId, NeedsSnapshot snap)
    {
        var family = NeedsBrain.FamilyOf(routineId);
        return DecideChoice.EffectivePower(
            snap.Power,
            snap.AlliesPower,
            family is RoutineFamily.Hunt or RoutineFamily.Dungeon or RoutineFamily.Party
        );
    }

    internal static string NearbyName(SosariaCharacter character)
    {
        if (character.Map == null || character.Map == Map.Internal)
        {
            return null;
        }

        foreach (var mobile in character.Map.GetMobilesInRange(character.Location, HearRange))
        {
            if (mobile != character && mobile is { Deleted: false, Alive: true } && People.Perceives(character, mobile))
            {
                return mobile.Name;
            }
        }

        return null;
    }

    private static string ChoicesText(SosariaCharacter character)
    {
        var builder = new System.Text.StringBuilder();
        var snap = Snapshot(character);
        builder.Append("Hits ");
        builder.Append((int)(snap.HitsFraction * 100));
        builder.Append(" percent. Gold in bank ");
        builder.Append(snap.GoldBanked);
        builder.Append(", carried ");
        builder.Append(snap.GoldCarried);
        builder.Append(". Pack ");
        builder.Append((int)(snap.PackFillFraction * 100));
        builder.Append(" percent. Time ");
        builder.Append(Core.Now.ToString("HH:mm"));
        builder.Append('.');

        if (snap.PartyForming)
        {
            builder.Append(" Your party is forming.");
        }

        builder.AppendLine();
        var choices = character.Definition?.Choices;

        if (choices == null)
        {
            return builder.ToString();
        }

        for (var i = 0; i < choices.Count; i++)
        {
            builder.Append("- ");
            builder.Append(choices[i].Routine);
            builder.Append(": ");
            builder.Append(choices[i].Description);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static void RecordDrop(DateTime now)
    {
        _dropsSinceLog++;
        var window = TimeSpan.FromMinutes(1);

        if (_lastDropLog == default || now - _lastDropLog >= window)
        {
            logger.Information(
                "Brain dropped {Count} events because {Max} requests are already in flight",
                _dropsSinceLog,
                _settings.MaxConcurrentRequests
            );
            _dropsSinceLog = 0;
            _lastDropLog = now;
        }
    }
}
