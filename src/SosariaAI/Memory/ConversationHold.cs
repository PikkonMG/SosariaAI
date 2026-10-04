using System;
using Server;

namespace SosariaAI.Memory;

/// <summary>
/// A player who talks to a character gets its attention. While the hold is active the character
/// stands still, faces the player and waits for the next line instead of walking off mid-sentence.
/// Waiting for a model reply does not use the attention window. A hard ceiling still ends a lost
/// request. After a reply, the normal window applies so the player can speak again.
/// </summary>
public sealed class ConversationHold
{
    public static readonly TimeSpan ReplyWaitCeiling = TimeSpan.FromSeconds(120);

    private DateTime _until;
    private bool _waitingForReply;

    public Serial Partner { get; private set; } = Serial.Zero;

    public bool IsActive(DateTime now) => _until != default && now < _until;

    public void Hold(Serial partner, DateTime now, TimeSpan window)
    {
        Partner = partner;
        _waitingForReply = false;
        _until = now + window;
    }

    public void WaitForReply(Serial partner, DateTime now, TimeSpan ceiling)
    {
        Partner = partner;
        _waitingForReply = true;
        _until = now + ceiling;
    }

    public void HeardReply(DateTime now, TimeSpan window)
    {
        if (Partner == Serial.Zero)
        {
            return;
        }

        _waitingForReply = false;
        _until = now + window;
    }

    public bool GiveUpIfDue(DateTime now)
    {
        if (!_waitingForReply || _until == default || now < _until)
        {
            return false;
        }

        Clear();
        return true;
    }

    public void Clear()
    {
        Partner = Serial.Zero;
        _waitingForReply = false;
        _until = default;
    }
}
