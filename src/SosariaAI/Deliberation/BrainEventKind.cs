namespace SosariaAI.Deliberation;

public enum BrainEventKind
{
    Spoken,
    Attacked,
    Died,
    GoalEnded,
    Decide,
    Plan,
    DungeonEnded,
    PlayerNoticed,

    /// <summary>
    /// Nobody spoke to the character. It says something of its own accord, out of what it
    /// wants, what it did today and who is around. This replaces reading a canned line.
    /// </summary>
    Musing,

    /// <summary>
    /// A line the game already decided (a trade offer) said in the character's own voice.
    /// The words may change; the numbers and goods may not.
    /// </summary>
    Reword,

    /// <summary>
    /// A copy that has no personal persona yet asks the chat model to write one, once.
    /// The answer is saved and never asked for again (see <see cref="PersonaWriter"/>).
    /// </summary>
    PersonaWrite
}
