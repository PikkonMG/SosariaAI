using System;
using System.IO;

namespace SosariaAI.Logging;

/// <summary>
/// The plugin's own activity log. A hundred simulated players write many lines a
/// second; the console cannot show them and the operator cannot read it. Every
/// per-character line goes here. Warnings and the minute summary also reach the console.
/// </summary>
public static class ActivityFile
{
    public const string FileName = "activity.log";
    public const string PreviousFileName = "activity.prev.log";
    private const string TimeFormat = "HH:mm:ss";

    private static readonly object Gate = new();
    private static StreamWriter _writer;

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
            var previous = System.IO.Path.Combine(directory, PreviousFileName);

            if (File.Exists(Path))
            {
                File.Move(Path, previous, overwrite: true);
            }

            _writer = new StreamWriter(Path, append: false) { AutoFlush = true };
        }
    }

    public static void Write(string level, string source, string message)
    {
        lock (Gate)
        {
            _writer?.WriteLine($"[{DateTime.Now.ToString(TimeFormat)} {level}] {message} <{source}>");
        }
    }

    public static void Close()
    {
        lock (Gate)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
