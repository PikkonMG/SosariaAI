using System.Text;

namespace SosariaAI.Deliberation;

/// <summary>
/// A 1999 player did not type em dashes. Strip them before speech.
/// </summary>
public static class SpeechSanitizer
{
    public const char EmDash = '\u2014';
    public const char EnDash = '\u2013';
    public const char Hyphen = '-';

    public static string StripDashes(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        if (text.IndexOf(EmDash) < 0 && text.IndexOf(EnDash) < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            builder.Append(c is EmDash or EnDash ? Hyphen : c);
        }

        return builder.ToString();
    }
}
