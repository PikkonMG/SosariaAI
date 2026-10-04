namespace SosariaAI.Deliberation;

/// <summary>What a request asks for. Every chat call is <see cref="Plain"/>.</summary>
public enum JevAsk
{
    /// <summary>Generated text from a chat provider, or a routine pick from Jev.</summary>
    Plain,

    /// <summary>The speech gate: would the character answer at all (a noul).</summary>
    Gate,

    /// <summary>What a player's unclear line wants (a choice).</summary>
    Intent,

    /// <summary>A caller's own typed questions, handed back as Jev answered them.</summary>
    Answers
}
