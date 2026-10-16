using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;

namespace SosariaAI.Logging;

/// <summary>
/// The plugin's own activity log. A hundred simulated players write many lines a
/// second; the console cannot show them and the operator cannot read it. Every
/// per-character line goes here. Warnings and the minute summary also reach the console.
/// Each boot starts a fresh file, and the last run's file moves to <see cref="RunsFolder"/>,
/// named for the time it ended. Only the last boot was kept once: a six-hour run of 800
/// people was lost to two short boots the next morning. Lines wait in a buffer that a
/// background timer writes out every <see cref="FlushInterval"/>: a write per line held the
/// world thread on the disk. Shutdown and a crash switch the file to
/// <see cref="WriteThrough"/>.
/// </summary>
public static class ActivityFile
{
    public const string FileName = "activity.log";
    public const string RunsFolder = "activity-runs";

    /// <summary>Past runs kept; a six-hour run of 800 people writes about 60 MB.</summary>
    public const int KeptRuns = 10;

    private const string RunFilePrefix = "activity-";
    private const string RunFileExtension = ".log";
    private const string RunStampFormat = "yyyyMMdd-HHmmss";
    private const string TimeFormat = "HH:mm:ss";

    /// <summary>How often the buffered lines are written to the disk.</summary>
    public static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);

    private static readonly object Gate = new();
    private static StreamWriter _writer;
    private static Timer _flusher;

    public static string Path { get; private set; }

    public static void Open(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        lock (Gate)
        {
            Close();
            Directory.CreateDirectory(directory);
            Path = System.IO.Path.Combine(directory, FileName);

            if (File.Exists(Path))
            {
                KeepRun(System.IO.Path.Combine(directory, RunsFolder));
            }

            _writer = new StreamWriter(Path, append: false);
            _flusher = new Timer(_ => Flush(), null, FlushInterval, FlushInterval);
        }
    }

    /// <summary>
    /// Writes the buffer out and sends every later line straight to the disk, for the lines a
    /// shutdown or a crash still logs.
    /// </summary>
    public static void WriteThrough()
    {
        lock (Gate)
        {
            if (_writer != null)
            {
                _writer.AutoFlush = true;
            }
        }
    }

    /// <summary>The name a run's file is kept under: the time its last line was written.</summary>
    public static string RunFileName(DateTime endedAt) =>
        RunFilePrefix + endedAt.ToString(RunStampFormat, CultureInfo.InvariantCulture) + RunFileExtension;

    /// <summary>
    /// The kept run files past the newest <paramref name="kept"/>, oldest first. The time in a
    /// run file's name sorts as text, so the names alone order the runs.
    /// </summary>
    public static List<string> Expired(IEnumerable<string> runFileNames, int kept)
    {
        var names = new List<string>();

        foreach (var name in runFileNames)
        {
            if (name.StartsWith(RunFilePrefix, StringComparison.Ordinal) &&
                name.EndsWith(RunFileExtension, StringComparison.Ordinal))
            {
                names.Add(name);
            }
        }

        names.Sort(StringComparer.Ordinal);
        var expiredCount = Math.Max(0, names.Count - Math.Max(0, kept));
        return names.GetRange(0, expiredCount);
    }

    public static void Write(string level, string source, string message)
    {
        lock (Gate)
        {
            _writer?.WriteLine($"[{DateTime.Now.ToString(TimeFormat)} {level}] {message} <{source}>");
        }
    }

    private static void Flush()
    {
        lock (Gate)
        {
            _writer?.Flush();
        }
    }

    private static void Close()
    {
        lock (Gate)
        {
            _flusher?.Dispose();
            _flusher = null;
            _writer?.Dispose();
            _writer = null;
        }
    }

    private static void KeepRun(string runsDirectory)
    {
        Directory.CreateDirectory(runsDirectory);
        var kept = System.IO.Path.Combine(runsDirectory, RunFileName(File.GetLastWriteTime(Path)));
        File.Move(Path, kept, overwrite: true);

        var names = new List<string>();

        foreach (var file in Directory.GetFiles(runsDirectory))
        {
            names.Add(System.IO.Path.GetFileName(file));
        }

        foreach (var name in Expired(names, KeptRuns))
        {
            File.Delete(System.IO.Path.Combine(runsDirectory, name));
        }
    }
}
