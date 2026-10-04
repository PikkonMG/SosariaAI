using System.Collections.Generic;
using Server;

namespace SosariaAI.Deliberation;

public sealed record BrainResult(
    long RequestId,
    Serial CharacterSerial,
    Serial SpeakerSerial,
    string CharacterName,
    BrainEventKind Kind,
    string Say,
    int LatencyMilliseconds,
    string Error,
    string Choose = null,
    string Act = null,
    bool FromParty = false,
    string CharacterId = null,
    int PlanRevision = 0,
    string Raw = null,
    int InputTokens = 0,
    JevAsk Ask = JevAsk.Plain,
    double Gate = 0,
    IReadOnlyDictionary<string, SystemOneAnswer> Answers = null,
    string ProviderName = null
);
