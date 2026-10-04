using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Server.Json;

namespace SosariaAI.Configuration;

/// <summary>
/// The personal personas the chat model wrote, one file per copy under
/// personas-generated/. A file is only written when it is missing, so an operator's edit
/// stays, and a copy with a file is never asked about again.
/// </summary>
public static class PersonasGeneratedFile
{
    public const string FolderName = "personas-generated";
    public const char FacetSeparatorStandIn = '-';
    public const char UnsafeStandIn = '_';

    private const char Dot = '.';

    public static string DefaultDirectory => ConfigFile.PathIn(FolderName);

    /// <summary>
    /// A file name that is safe on every file system: the facet colon becomes a dash and any
    /// other character outside letters, digits, dash and dot becomes an underscore.
    /// </summary>
    public static string FileNameFor(string characterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(characterId);
        var builder = new StringBuilder(characterId.Length + ConfigFile.FileExtension.Length);

        for (var i = 0; i < characterId.Length; i++)
        {
            var c = characterId[i];

            if (c == FacetIds.Separator)
            {
                builder.Append(FacetSeparatorStandIn);
            }
            else if (char.IsAsciiLetterOrDigit(c) || c == FacetSeparatorStandIn || c == Dot)
            {
                builder.Append(c);
            }
            else
            {
                builder.Append(UnsafeStandIn);
            }
        }

        return builder.Append(ConfigFile.FileExtension).ToString();
    }

    /// <summary>
    /// Every saved draft by character id, as written: each is vetted again when a character
    /// binds, against the era band of that boot.
    /// </summary>
    public static Dictionary<string, PersonaDraft> Load(string directory)
    {
        var drafts = new Dictionary<string, PersonaDraft>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(directory))
        {
            return drafts;
        }

        foreach (var path in Directory.GetFiles(directory, ConfigFile.JsonSearchPattern))
        {
            var draft = ConfigFile.LoadOrDefault<PersonaDraft>(path, null, () => null);

            if (!string.IsNullOrWhiteSpace(draft?.CharacterId))
            {
                drafts[draft.CharacterId] = draft;
            }
        }

        return drafts;
    }

    /// <summary>True when a file for this character is on disk.</summary>
    public static bool Exists(string directory, string characterId) =>
        File.Exists(Path.Combine(directory, FileNameFor(characterId)));

    /// <summary>Writes the draft when its file is missing. False when a file was already there.</summary>
    public static bool SaveIfMissing(string directory, PersonaDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, FileNameFor(draft.CharacterId));

        if (File.Exists(path))
        {
            return false;
        }

        JsonConfig.Serialize(path, draft);
        return true;
    }
}
