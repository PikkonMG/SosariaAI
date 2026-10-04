using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server.Json;

namespace SosariaAI.Configuration;

public static class BrainFile
{
    public const string FileName = "brain.json";

    private static readonly JsonSerializerOptions FileOptions = CreateFileOptions();

    public static string DefaultPath => ConfigFile.PathIn(FileName);

    public static BrainConfiguration LoadOrCreate(string path)
    {
        BrainConfiguration loaded;

        if (File.Exists(path))
        {
            loaded = ConfigFile.LoadOrDefault(path, FileOptions, CreateDefault);
        }
        else
        {
            loaded = CreateDefault();
            JsonConfig.Serialize(path, loaded, FileOptions);
        }

        loaded.Normalize();
        return loaded;
    }

    /// <summary>The shipped providers; every other key takes its declared default.</summary>
    public static BrainConfiguration CreateDefault() =>
        new()
        {
            Providers = new(StringComparer.OrdinalIgnoreCase)
            {
                [BrainProviders.FrontierName] = new ProviderDefinition
                {
                    BaseUrl = BrainConfiguration.DefaultBaseUrl,
                    Model = BrainConfiguration.DefaultModel,
                    ApiKeyEnvironmentVariable = BrainConfiguration.DefaultApiKeyEnvironmentVariable,
                    ApiKey = string.Empty,
                    TimeoutSeconds = BrainConfiguration.DefaultTimeoutSeconds,
                    Paid = true
                },
                [BrainProviders.LocalName] = new ProviderDefinition
                {
                    BaseUrl = BrainProviders.DefaultLocalBaseUrl,
                    Model = BrainProviders.DefaultLocalModel,
                    ApiKey = BrainProviders.DefaultLocalApiKey,
                    TimeoutSeconds = BrainProviders.DefaultLocalTimeoutSeconds,
                    Paid = false
                },
                [BrainProviders.JevName] = new ProviderDefinition
                {
                    Api = BrainProviders.SystemOneApi,
                    BaseUrl = BrainProviders.DefaultJevBaseUrl,
                    Model = BrainProviders.DefaultJevModel,
                    ApiKeyEnvironmentVariable = BrainProviders.DefaultJevKeyVariable,
                    ApiKey = string.Empty,
                    TimeoutSeconds = BrainConfiguration.DefaultTimeoutSeconds,
                    Paid = true
                },
                [BrainProviders.VonName] = new ProviderDefinition
                {
                    Api = BrainProviders.SystemOneApi,
                    BaseUrl = BrainProviders.DefaultVonBaseUrl,
                    Model = BrainProviders.DefaultVonModel,
                    TimeoutSeconds = BrainConfiguration.DefaultTimeoutSeconds,
                    Paid = false
                },
                [BrainProviders.LayaName] = new ProviderDefinition
                {
                    Api = BrainProviders.SystemOneApi,
                    BaseUrl = BrainProviders.DefaultLayaBaseUrl,
                    Model = BrainProviders.DefaultLayaModel,
                    TimeoutSeconds = BrainConfiguration.DefaultTimeoutSeconds,
                    Paid = false
                }
            }
        };

    private static JsonSerializerOptions CreateFileOptions()
    {
        var options = new JsonSerializerOptions(JsonConfig.DefaultOptions)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        return options;
    }
}
