using Server;

namespace SosariaAI.Deliberation;

public sealed record BrainRequest(
    long RequestId,
    Serial CharacterSerial,
    Serial SpeakerSerial,
    string CharacterName,
    BrainEventKind Kind,
    string SystemMessage,
    string UserMessage,
    string ProviderName = null,
    bool HighPriority = false,
    bool FromParty = false,
    string CharacterId = null,
    int PlanRevision = 0,
    JevDecision Jev = null,
    JevAsk Ask = JevAsk.Plain,
    JevKind JevKind = JevKind.Decision
);
