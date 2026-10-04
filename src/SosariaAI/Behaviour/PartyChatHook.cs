using Server;
using Server.Engines.PartySystem;
using SosariaAI.Deliberation;
using SosariaAI.Mobiles;

namespace SosariaAI.Behaviour;

/// <summary>
/// Party chat is packets, not spoken words. Wrap the engine handler so a person's party line
/// reaches the characters as speech aimed at them: a public line every character in the
/// party hears, a private line only the character it was sent to.
/// </summary>
public sealed class PartyChatHook : PartyCommands
{
    private readonly PartyCommands _inner;

    public PartyChatHook(PartyCommands inner) => _inner = inner;

    public static void Install()
    {
        if (Handler is null or PartyChatHook)
        {
            return;
        }

        Handler = new PartyChatHook(Handler);
    }

    public override void OnAdd(Mobile from) => _inner.OnAdd(from);

    public override void OnRemove(Mobile from, Mobile target) => _inner.OnRemove(from, target);

    public override void OnSetCanLoot(Mobile from, bool canLoot) => _inner.OnSetCanLoot(from, canLoot);

    public override void OnAccept(Mobile from, Mobile leader) => _inner.OnAccept(from, leader);

    public override void OnDecline(Mobile from, Mobile leader) => _inner.OnDecline(from, leader);

    public override void OnPrivateMessage(Mobile from, Mobile target, string text)
    {
        Hear(from, text, target);
        _inner.OnPrivateMessage(from, target, text);
    }

    public override void OnPublicMessage(Mobile from, string text)
    {
        Hear(from, text, target: null);
        _inner.OnPublicMessage(from, text);
    }

    private static void Hear(Mobile from, string text, Mobile target)
    {
        if (!People.IsHuman(from) || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var party = GameParty.Of(from);

        if (party == null)
        {
            return;
        }

        for (var i = 0; i < party.Members.Count; i++)
        {
            if (party.Members[i].Mobile is not SosariaCharacter character)
            {
                continue;
            }

            if (target != null && character != target)
            {
                continue;
            }

            Brain.HearParty(character, from, text);
        }

        GameParty.NoteSpokenAnswer(from, text);
    }
}
