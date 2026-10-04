using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Json;
using Server.Logging;
using SosariaAI.Logging;

namespace SosariaAI.Configuration;

/// <summary>
/// Loads an operator-edited JSON file without ever crashing the server. A hand edit
/// that breaks the JSON is logged in plain words with the file, the editor line and
/// the most likely cause, and the defaults are used instead.
/// </summary>
public static class ConfigFile
{
    public const string FileExtension = ".json";
    public const string JsonSearchPattern = "*" + FileExtension;

    private const string ServerConfigurationFolder = "Configuration";
    private const string PluginFolder = "sosariaai";

    /// <summary>The plugin's own runtime folder: every file it writes lives here.</summary>
    public static string RootDirectory => Path.Combine(Core.BaseDirectory, ServerConfigurationFolder, PluginFolder);

    /// <summary>A file or folder by name inside <see cref="RootDirectory"/>.</summary>
    public static string PathIn(string name) => Path.Combine(RootDirectory, name);

    // System.Text.Json reports a missing comma as an unexpected token after a value.
    private const string MissingCommaMarker = "Expected either ',', '}', or ']'";

    private const string MissingCommaHint = "Line {0} needs a comma at its end.";

    private const string QuotesHint =
        "Every text value must be inside double quotes, for example \"apiKey\": \"abc123\".";

    private const string DefaultsNote = "Using the defaults for this file.";

    // JsonException counts lines and positions from zero. Editors count from one.
    private const long EditorOffset = 1;

    private static readonly ILogger logger = SosariaLog.For(typeof(ConfigFile));

    public static void WriteMissing<T>(string path, Func<T> create)
    {
        if (!File.Exists(path))
        {
            JsonConfig.Serialize(path, create());
        }
    }

    public static T LoadOrDefault<T>(string path, JsonSerializerOptions options, Func<T> createDefault) where T : class
    {
        try
        {
            return JsonConfig.Deserialize<T>(path, options) ?? createDefault();
        }
        catch (JsonException e)
        {
            logger.Error(
                "{Path} is not valid JSON (line {Line}, position {Position}): {Reason}. {Hint} {Note}",
                path,
                EditorLine(e.LineNumber),
                EditorLine(e.BytePositionInLine),
                e.Message,
                HintFor(e),
                DefaultsNote
            );
            return createDefault();
        }
    }

    /// <summary>Picks the plain-words hint that matches the parse error.</summary>
    public static string HintFor(JsonException error)
    {
        if (error.Message.Contains(MissingCommaMarker, StringComparison.Ordinal))
        {
            var lineBeforeError = EditorLine(error.LineNumber) - EditorOffset;
            return string.Format(MissingCommaHint, lineBeforeError);
        }

        return QuotesHint;
    }

    private static long EditorLine(long? zeroBased) => (zeroBased ?? 0) + EditorOffset;
}
