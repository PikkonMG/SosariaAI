using System;
using System.Collections.Generic;
using System.IO;
using Server;
using SosariaAI.Memory;

namespace SosariaAI.Tests;

/// <summary>A fresh long-term memory file for one test, with the people the reader tests use. Removed afterwards.</summary>
public sealed class RecallTestStore : IDisposable
{
    public const string Despise = "Despise";
    public const string Britain = "Britain";
    public const string MapName = "Felucca";
    private const int OutingMinutes = 20;
    private const uint TamsinSerial = 0x7C01;

    public static readonly DateTime Now = new(2026, 10, 3, 18, 0, 0, DateTimeKind.Utc);
    public static readonly PersonRef Halvard = PersonRef.Bot("Felucca:halvard", "Halvard");
    public static readonly PersonRef Bryn = PersonRef.Bot("Felucca:bryn", "Bryn");
    public static readonly PersonRef Grim = PersonRef.Bot("Felucca:grim", "Grim");
    public static readonly PersonRef Tamsin = PersonRef.Player((Serial)TamsinSerial, "Tamsin");

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "sosaria-recall-" + Guid.NewGuid().ToString("N"));

    static RecallTestStore() => Timer.Init(0);

    public RecallTestStore()
    {
        Directory.CreateDirectory(_folder);
        Store.Open(Path.Combine(_folder, MemoryStore.FileName));
    }

    public MemoryStore Store { get; } = new();

    /// <summary>Keeps one adventure that ended at <paramref name="endedAt"/> and returns its id.</summary>
    public long Record(string kind, string place, DateTime endedAt, string summary, params (PersonRef Person, string Role)[] members)
    {
        var list = new List<AdventureMember>(members.Length);

        for (var i = 0; i < members.Length; i++)
        {
            list.Add(new AdventureMember(members[i].Person, members[i].Role));
        }

        return Store.Record(
            new Adventure
            {
                Kind = kind,
                Place = place,
                Map = MapName,
                StartedAt = endedAt.AddMinutes(-OutingMinutes),
                EndedAt = endedAt,
                Summary = summary,
                Members = list
            }
        );
    }

    public void Dispose()
    {
        Store.Close();

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
