using System.Buffers;
using System.IO;
using Server;
using Server.Network;

namespace SosariaAI.Social;

/// <summary>
/// Guild chat never reaches a character: with the new guild system the engine sends a player's
/// guild line straight to the members' clients as packets, and a character has no client. This
/// wraps the engine's own unicode speech handler: the engine still handles every line exactly as
/// before, and a guild-typed line from a player is also passed to <see cref="GuildChat"/>. Nothing
/// in the engine is changed; the handler table is the engine's public registration point.
/// </summary>
public static unsafe class GuildChatHook
{
    public const int UnicodeSpeechPacket = 0xAD;

    /// <summary>The engine's own limits on a spoken line and its keyword list.</summary>
    public const int MaxSpeechLength = 128;

    public const int MaxKeywords = 50;

    private const int KeywordCountMask = 0xFFF0;
    private const int KeywordCountShift = 4;
    private const int LanguageLength = 4;

    private static delegate*<NetState, SpanReader, void> _engineSpeech;

    public static void Install()
    {
        var handler = IncomingPackets.GetHandler(UnicodeSpeechPacket);

        if (handler == null || _engineSpeech != null)
        {
            return;
        }

        _engineSpeech = handler.OnReceive;
        IncomingPackets.Register(
            new PacketHandler(
                UnicodeSpeechPacket,
                handler.GetLength(null),
                handler.InGameOnly,
                handler.OutOfGameOnly,
                &OnUnicodeSpeech
            )
            {
                ThrottleCallback = handler.ThrottleCallback
            }
        );
    }

    private static void OnUnicodeSpeech(NetState state, SpanReader reader)
    {
        // The copy is read on its own; the engine gets the untouched reader.
        var peek = reader;
        var guildLine = ReadGuildLine(ref peek);

        _engineSpeech(state, reader);

        if (guildLine != null && state?.Mobile is { Deleted: false } from)
        {
            GuildChat.HeardFromPlayer(from, guildLine);
        }
    }

    /// <summary>
    /// The text of a guild-typed unicode speech packet, or null for any other line or a
    /// malformed one. Reads the packet the way the engine's handler does.
    /// </summary>
    public static string ReadGuildLine(ref SpanReader reader)
    {
        if (reader.Remaining < 1)
        {
            return null;
        }

        var type = (MessageType)reader.ReadByte();

        if ((type & ~MessageType.Encoded) != MessageType.Guild)
        {
            return null;
        }

        try
        {
            reader.ReadInt16(); // hue
            reader.ReadInt16(); // font
            reader.ReadAscii(LanguageLength);
            string text;

            if ((type & MessageType.Encoded) != 0)
            {
                int value = reader.ReadInt16();
                var count = (value & KeywordCountMask) >> KeywordCountShift;

                if (count is < 0 or > MaxKeywords)
                {
                    return null;
                }

                // Twelve-bit keyword ids: the first of each pair takes a byte, the second two.
                for (var i = 0; i < count; i++)
                {
                    if ((i & 1) == 0)
                    {
                        reader.ReadByte();
                    }
                    else
                    {
                        reader.ReadInt16();
                    }
                }

                text = reader.ReadUTF8Safe();
            }
            else
            {
                text = reader.ReadBigUniSafe();
            }

            text = text.Trim();
            return text.Length is > 0 and <= MaxSpeechLength ? text : null;
        }
        catch (EndOfStreamException)
        {
            return null;
        }
    }
}
