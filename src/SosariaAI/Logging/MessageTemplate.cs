using System.Text;

namespace SosariaAI.Logging;

/// <summary>
/// Renders a "{Name} did {Thing}" template with its positional arguments, the way the
/// console logger does, for the activity file. Pure.
/// </summary>
public static class MessageTemplate
{
    private const char HoleOpen = '{';
    private const char HoleClose = '}';
    private const char FormatSeparator = ':';
    private const string NullText = "null";

    public static string Render(string template, object[] args)
    {
        if (string.IsNullOrEmpty(template) || args == null || args.Length == 0)
        {
            return template ?? string.Empty;
        }

        var text = new StringBuilder(template.Length + args.Length * 8);
        var next = 0;
        var i = 0;

        while (i < template.Length)
        {
            var c = template[i];

            if (c == HoleOpen && i + 1 < template.Length && template[i + 1] == HoleOpen)
            {
                text.Append(HoleOpen);
                i += 2;
                continue;
            }

            if (c == HoleClose && i + 1 < template.Length && template[i + 1] == HoleClose)
            {
                text.Append(HoleClose);
                i += 2;
                continue;
            }

            if (c != HoleOpen)
            {
                text.Append(c);
                i++;
                continue;
            }

            var close = template.IndexOf(HoleClose, i + 1);

            if (close < 0)
            {
                text.Append(template, i, template.Length - i);
                break;
            }

            var hole = template.Substring(i + 1, close - i - 1);
            var separator = hole.IndexOf(FormatSeparator);
            var format = separator >= 0 ? hole[(separator + 1)..] : null;
            text.Append(next < args.Length ? Format(args[next++], format) : HoleOpen + hole + HoleClose);
            i = close + 1;
        }

        return text.ToString();
    }

    private static string Format(object value, string format)
    {
        if (value == null)
        {
            return NullText;
        }

        if (!string.IsNullOrEmpty(format) && value is System.IFormattable formattable)
        {
            return formattable.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
        }

        return value.ToString();
    }
}
