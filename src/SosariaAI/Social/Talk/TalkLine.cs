using SosariaAI.Behaviour;

namespace SosariaAI.Social;

/// <summary>
/// One line of a talk file: its text, the slots it names, and the eras it belongs to. No eras
/// means every era.
/// </summary>
public sealed record TalkLine(string Text, TalkSlot Needs, string[] Eras)
{
    public static TalkLine Of(string text, string[] eras) => new(text, TalkSlots.Needs(text), eras);

    public bool Fits(EraBand band) => EraBands.Fits(Eras, band);

    /// <summary>True when every slot the line names is filled.</summary>
    public bool FilledBy(TalkSlot filled) => (Needs & ~filled) == TalkSlot.None;
}

/// <summary>A talk category with its note for the operator and its default lines.</summary>
public sealed record TalkTopic(string Name, string About, string[] Lines);
