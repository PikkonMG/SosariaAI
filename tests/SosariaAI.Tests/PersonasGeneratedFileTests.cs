using System;
using System.IO;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class PersonasGeneratedFileTests : IDisposable
{
    private const string OperatorEdit = "Operator wrote this one by hand.";
    private const int WritesPerMinute = 2;

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "sosariaai-personas-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void FileNameFor_IsSafeOnEveryFileSystem()
    {
        Assert.Equal("Felucca-connor_7.json", PersonasGeneratedFile.FileNameFor(PersonaDraftSamples.CopyId));
        Assert.Equal("Trammel-a_b_c.json", PersonasGeneratedFile.FileNameFor("Trammel:a/b\\c"));
    }

    [Fact]
    public void SaveIfMissing_CreatesTheFolder_ThenNeverOverwrites()
    {
        var draft = Stamped();

        Assert.True(PersonasGeneratedFile.SaveIfMissing(_directory, draft));
        Assert.True(PersonasGeneratedFile.Exists(_directory, PersonaDraftSamples.CopyId));

        draft.Background = OperatorEdit;
        Assert.False(PersonasGeneratedFile.SaveIfMissing(_directory, draft));
        Assert.NotEqual(OperatorEdit, PersonasGeneratedFile.Load(_directory)[PersonaDraftSamples.CopyId].Background);
    }

    [Fact]
    public void Load_ReadsEveryDraftById_AndSkipsBrokenFiles()
    {
        PersonasGeneratedFile.SaveIfMissing(_directory, Stamped());
        File.WriteAllText(Path.Combine(_directory, "broken.json"), "{ not json");

        var loaded = PersonasGeneratedFile.Load(_directory);

        Assert.Single(loaded);
        Assert.Equal(PersonaDraftRules.IdleCount, loaded[PersonaDraftSamples.CopyId].IdleLines.Count);
        Assert.Empty(PersonasGeneratedFile.Load(Path.Combine(_directory, "missing")));
    }

    [Fact]
    public void Find_VetsTheSavedDraftForTheBandOfThisBoot()
    {
        var draft = Stamped();
        draft.IdleLines[0] = "saw a paladin at the bank";
        PersonasGeneratedFile.SaveIfMissing(_directory, draft);
        PersonaWriter.Configure(_directory, WritesPerMinute, enabled: true);

        Assert.NotNull(PersonaWriter.Find(PersonaDraftSamples.CopyId, EraBand.ML));
        Assert.Null(PersonaWriter.Find(PersonaDraftSamples.CopyId, EraBand.T2A));
        Assert.Null(PersonaWriter.Find("Felucca:nobody#1", EraBand.ML));
    }

    [Fact]
    public void WriterOff_QueuesNothingAndReadsNoSavedPersona()
    {
        PersonasGeneratedFile.SaveIfMissing(_directory, Stamped());

        PersonaWriter.Configure(_directory, WritesPerMinute, enabled: false);
        Assert.False(PersonaWriter.WritesNew);
        Assert.Null(PersonaWriter.Find(PersonaDraftSamples.CopyId, EraBand.T2A));

        PersonaWriter.Configure(_directory, WritesPerMinute, enabled: true);
        Assert.True(PersonaWriter.WritesNew);
        Assert.NotNull(PersonaWriter.Find(PersonaDraftSamples.CopyId, EraBand.T2A));
    }

    [Fact]
    public void ZeroRate_StopsNewWritesButKeepsSavedPersonas()
    {
        PersonasGeneratedFile.SaveIfMissing(_directory, Stamped());
        PersonaWriter.Configure(_directory, writesPerMinute: 0, enabled: true);

        Assert.False(PersonaWriter.WritesNew);
        Assert.NotNull(PersonaWriter.Find(PersonaDraftSamples.CopyId, EraBand.T2A));
    }

    private static PersonaDraft Stamped()
    {
        var draft = PersonaDraftSamples.Valid();
        draft.CharacterId = PersonaDraftSamples.CopyId;
        draft.Era = EraBands.T2ATag;
        return draft;
    }
}
