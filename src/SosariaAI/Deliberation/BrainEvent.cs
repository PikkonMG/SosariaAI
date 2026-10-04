using System;
using Server;

namespace SosariaAI.Deliberation;

public sealed record BrainEvent(
    BrainEventKind Kind,
    Serial CharacterSerial,
    string CharacterName,
    string SpeakerName,
    Serial SpeakerSerial,
    bool SpeakerIsPlayer,
    string Text,
    string CurrentActivity,
    string Location,
    DateTime When,
    bool FromParty = false,
    string Identity = null
);
