using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Mobiles;

namespace SosariaAI.Deliberation;

/// <summary>
/// A caller's own typed questions for the Brain's System One provider. The Brain answers on
/// the world thread with the answers keyed by question name, or with null when the call
/// failed or was refused by a cap, plus the name of the provider that answered. <paramref name="Other"/> is the person or foe the call is
/// about; <paramref name="Use"/> is what the call is for, for the one budget and the usage line.
/// </summary>
internal sealed record JevCall(
    SosariaCharacter Character,
    Mobile Other,
    BrainEventKind Kind,
    JevKind Use,
    JevDecision Decision,
    Action<IReadOnlyDictionary<string, SystemOneAnswer>, string> Answer
);
