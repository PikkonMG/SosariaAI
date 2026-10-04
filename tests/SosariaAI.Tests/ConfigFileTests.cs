using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Json;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class ConfigFileTests
{
    private const string MissingCommaJson = "{\n  \"a\": 1\n  \"b\": 2\n}\n";
    private const string TrailingCommaJson = "{\n  \"a\": 1,\n}\n";

    [Fact]
    public void WriteMissing_WritesOnlyWhenTheFileIsAbsent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sosariaai-missing-{Guid.NewGuid():N}.json");

        try
        {
            ConfigFile.WriteMissing(path, () => new Dictionary<string, int> { ["n"] = 1 });
            var first = File.ReadAllText(path);
            ConfigFile.WriteMissing(path, () => new Dictionary<string, int> { ["n"] = 2 });
            Assert.Equal(first, File.ReadAllText(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void LoadOrDefault_ReturnsDefault_WhenACommaIsMissing()
    {
        var path = WriteTemp(MissingCommaJson);

        try
        {
            var fallback = new Dictionary<string, int> { ["fallback"] = 1 };
            var loaded = ConfigFile.LoadOrDefault(path, JsonConfig.DefaultOptions, () => fallback);

            Assert.Same(fallback, loaded);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadOrDefault_AcceptsATrailingComma()
    {
        var path = WriteTemp(TrailingCommaJson);

        try
        {
            var loaded = ConfigFile.LoadOrDefault(path, JsonConfig.DefaultOptions, () => new Dictionary<string, int>());

            Assert.Equal(1, loaded["a"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void HintFor_NamesTheEditorLineThatNeedsAComma()
    {
        var error = Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<Dictionary<string, int>>(MissingCommaJson, JsonConfig.DefaultOptions)
        );

        Assert.Equal("Line 2 needs a comma at its end.", ConfigFile.HintFor(error));
    }

    [Fact]
    public void HintFor_FallsBackToTheQuotesHint()
    {
        var error = Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<Dictionary<string, string>>("{ \"a\": abc }", JsonConfig.DefaultOptions)
        );

        Assert.Contains("double quotes", ConfigFile.HintFor(error));
    }

    private static string WriteTemp(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"sosariaai-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
