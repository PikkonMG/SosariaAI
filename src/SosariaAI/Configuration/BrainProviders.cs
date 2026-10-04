using System;
using System.Collections.Generic;
using SosariaAI.Deliberation;

namespace SosariaAI.Configuration;

public static class BrainProviders
{
    public const string FrontierName = "frontier";
    public const string LocalName = "local";
    public const string ChatKind = "chat";
    public const string DecisionKind = "decision";
    public const string SystemOneApi = "systemone";
    public const string JevName = "jev";
    public const string VonName = "von";
    public const string LayaName = "laya";
    public const string DefaultJevBaseUrl = "https://api." + SystemOneFamilies.TypeSafeHost;
    public const string DefaultJevModel = "jev-latest";
    public const string DefaultJevKeyVariable = "SOSARIAAI_JEV_KEY";
    public const string DefaultVonBaseUrl = "http://127.0.0.1:8000";
    public const string DefaultVonModel = "von-1.2.0";
    public const string DefaultLayaBaseUrl = "http://127.0.0.1:8001";
    public const string DefaultLayaModel = "auto";
    public const string DefaultLocalBaseUrl = "http://127.0.0.1:11434/v1";
    public const string DefaultLocalModel = "llama3.1:8b";
    public const string DefaultLocalApiKey = "ollama";
    public const int DefaultLocalTimeoutSeconds = 30;
    public const string ReasoningOff = "none";

    /// <summary>TypeSafe's documented Jev price. Input tokens are the only billed part.</summary>
    public const double JevDollarsPerMillionInputTokens = 0.042;

    public const long TokensPerMillion = 1_000_000;

    /// <summary>
    /// A System One provider answers typed questions (the TypeSafe Jev API) instead of
    /// chat completions. Anything else, including an unset api field, is OpenAI-shaped.
    /// </summary>
    public static bool IsSystemOne(ProviderDefinition provider) =>
        provider != null && SystemOneApi.Equals(provider.Api, StringComparison.OrdinalIgnoreCase);

    public static bool IsLocalhost(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.IsLoopback;
    }

    public static string ResolveApiKey(ProviderDefinition provider, Func<string, string> getEnvironmentVariable)
    {
        if (provider == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(provider.ApiKeyEnvironmentVariable))
        {
            var fromEnvironment = getEnvironmentVariable?.Invoke(provider.ApiKeyEnvironmentVariable);

            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                return fromEnvironment.Trim();
            }
        }

        return string.IsNullOrWhiteSpace(provider.ApiKey) ? null : provider.ApiKey.Trim();
    }

    public static bool IsUsable(ProviderDefinition provider, string apiKey)
    {
        if (provider == null ||
            string.IsNullOrWhiteSpace(provider.BaseUrl) ||
            string.IsNullOrWhiteSpace(provider.Model))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(apiKey))
        {
            return true;
        }

        // A provider declared free is one the operator runs, so it needs no key.
        return provider.Paid == false || IsLocalhost(provider.BaseUrl);
    }

    public static bool CountsAsPaid(ProviderDefinition provider, bool countLocalAsPaid)
    {
        if (provider == null)
        {
            return false;
        }

        if (countLocalAsPaid)
        {
            return true;
        }

        if (provider.Paid.HasValue)
        {
            return provider.Paid.Value;
        }

        return !IsLocalhost(provider.BaseUrl);
    }

    public static string ProviderNameForKind(BrainConfiguration config, string kind, string decisionUse = null)
    {
        if (config?.Route == null || string.IsNullOrWhiteSpace(kind))
        {
            return null;
        }

        var name = kind.Equals(DecisionKind, StringComparison.OrdinalIgnoreCase)
            ? config.Route.DecisionUses != null && !string.IsNullOrWhiteSpace(decisionUse) &&
              config.Route.DecisionUses.TryGetValue(decisionUse, out var selected) &&
              !string.IsNullOrWhiteSpace(selected)
                ? selected
                : config.Route.Decision
            : config.Route.Chat;

        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }

    public static ProviderDefinition Find(BrainConfiguration config, string name)
    {
        if (config?.Providers == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return config.Providers.TryGetValue(name, out var found) ? found : null;
    }

    public static IReadOnlyList<string> UsedNames(BrainConfiguration config)
    {
        var names = new List<string>();
        AddUnique(names, config?.Route?.Chat);
        AddUnique(names, config?.Route?.Decision);
        if (config?.Route?.DecisionUses != null)
        {
            foreach (var name in config.Route.DecisionUses.Values)
            {
                AddUnique(names, name);
            }
        }
        return names;
    }

    private static void AddUnique(List<string> names, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        for (var i = 0; i < names.Count; i++)
        {
            if (names[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        names.Add(name);
    }
}
