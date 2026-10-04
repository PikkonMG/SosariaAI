using System;
using System.Collections.Generic;
using System.IO;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Logging;

namespace SosariaAI.Social;

/// <summary>
/// Every talk category's lines, sorted by era once so a pick is a short walk. At boot each
/// category is read from Configuration/sosariaai/talk/&lt;category&gt;.txt; a missing file is
/// written from <see cref="TalkDefaults"/> first, and an existing one is never overwritten, so a
/// changed default never reaches a file already there. A row no speaker of its category can keep
/// (<see cref="TalkDefaults.Keeps"/>) is dropped as it loads. Until then (and in tests) the
/// defaults serve.
/// </summary>
public sealed class TalkLibrary
{
    public const string FolderName = "talk";

    private static readonly ILogger logger = SosariaLog.For(typeof(TalkLibrary));
    private static readonly int BandCount = Enum.GetValues<EraBand>().Length;

    private readonly Dictionary<string, TalkLine[][]> _byBand = new(StringComparer.Ordinal);

    public TalkLibrary(IReadOnlyDictionary<string, List<TalkLine>> categories)
    {
        foreach (var (name, lines) in categories)
        {
            var bands = new TalkLine[BandCount][];

            foreach (var band in Enum.GetValues<EraBand>())
            {
                bands[(int)band] = lines.FindAll(line => line.Fits(band)).ToArray();
            }

            _byBand[name] = bands;
        }
    }

    /// <summary>The library players hear. Swapped once at boot for the operator's files.</summary>
    public static TalkLibrary Shared { get; private set; } = FromDefaults();

    public static string DefaultDirectory() => ConfigFile.PathIn(FolderName);

    /// <summary>Called by ModernUO at boot.</summary>
    public static void Configure() => Shared = LoadOrCreate(DefaultDirectory());

    public static TalkLibrary FromDefaults()
    {
        var categories = new Dictionary<string, List<TalkLine>>(StringComparer.Ordinal);

        foreach (var topic in TalkDefaults.All)
        {
            categories[topic.Name] = TalkFile.Parse(topic.Lines).Lines;
        }

        return new TalkLibrary(categories);
    }

    /// <summary>
    /// Reads each category's file from <paramref name="directory"/>, writing a missing one from
    /// the defaults first. A file that cannot be read keeps its defaults; the reason is logged.
    /// </summary>
    public static TalkLibrary LoadOrCreate(string directory)
    {
        var categories = new Dictionary<string, List<TalkLine>>(StringComparer.Ordinal);
        var written = 0;
        var total = 0;

        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.Warning("Talk folder {Directory} could not be made ({Reason}); using the built-in lines", directory, e.Message);
            return FromDefaults();
        }

        foreach (var topic in TalkDefaults.All)
        {
            var path = Path.Combine(directory, topic.Name + TalkFile.Extension);
            var parsed = TalkFile.Parse(topic.Lines);

            try
            {
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, TalkFile.Render(topic));
                    written++;
                }

                parsed = TalkFile.Parse(File.ReadAllLines(path));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                logger.Warning("Talk file {Path} could not be read ({Reason}); using its built-in lines", path, e.Message);
            }

            foreach (var problem in parsed.Problems)
            {
                logger.Warning("Talk file {Path}, {Problem}", path, problem);
            }

            DropUnkept(topic.Name, parsed.Lines, path);
            categories[topic.Name] = parsed.Lines;
            total += parsed.Lines.Count;
        }

        logger.Information(
            "Talk: {Lines} lines in {Categories} categories from {Directory} ({Written} files written from defaults)",
            total,
            categories.Count,
            directory,
            written
        );

        return new TalkLibrary(categories);
    }

    /// <summary>
    /// Takes out the rows of <paramref name="category"/> its speaker cannot keep: a promise where
    /// nothing stands behind it, or a retired default an older boot wrote into the file. Each one is
    /// logged, so the operator sees why a row of theirs is never said.
    /// </summary>
    private static void DropUnkept(string category, List<TalkLine> lines, string path)
    {
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (!TalkDefaults.Keeps(category, lines[i].Text))
            {
                logger.Warning(
                    "Talk file {Path}: \"{Line}\" promises a trip, group or meeting nothing keeps there; it is never said",
                    path,
                    lines[i].Text
                );
                lines.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// One line of <paramref name="category"/> for this era, filled from <paramref name="slots"/>, or
    /// null when no line fits. A line naming an empty slot is passed over. The roll picks among
    /// the lines that fit, so the same roll gives the same line.
    /// </summary>
    public string Pick(string category, int roll, in TalkSlots slots, EraBand band)
    {
        if (category == null || !_byBand.TryGetValue(category, out var bands))
        {
            return null;
        }

        var lines = bands[(int)band];
        var filled = slots.Filled;
        var fitting = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].FilledBy(filled))
            {
                fitting++;
            }
        }

        if (fitting == 0)
        {
            return null;
        }

        var wanted = Math.Abs(roll % fitting);

        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].FilledBy(filled) && wanted-- == 0)
            {
                return slots.Fill(lines[i].Text);
            }
        }

        return null;
    }
}
