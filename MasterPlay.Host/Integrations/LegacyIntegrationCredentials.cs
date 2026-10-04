using MasterPlay.Core.Entities;
using MasterPlay.Infrastructure.Configuration;

namespace MasterPlay.Host.Integrations;

/// <summary>
/// Credentials configured before Admin Integrations existed: process environment
/// variables and the machine's config.json. They are read only to seed an empty
/// Vault entry once; values are never logged or returned.
/// </summary>
public static class LegacyIntegrationCredentials
{
    public static IReadOnlyDictionary<string, string>? Read(string provider) =>
        Read(provider, Environment.GetEnvironmentVariable, LoadConfig);

    public static IReadOnlyDictionary<string, string>? Read(
        string provider,
        Func<string, string?> environment,
        Func<Config?> loadConfig)
    {
        return provider switch
        {
            IntegrationProviders.Rawg => Complete(("apiKey", environment("RAWG_API_KEY"))),
            IntegrationProviders.Gemini => Complete(("apiKey", environment("GEMINI_API_KEY"))),
            IntegrationProviders.Steam => Complete(("apiKey", environment("STEAM_API_KEY"))),
            IntegrationProviders.Igdb =>
                Complete(("clientId", environment("IGDB_CLIENT_ID")), ("clientSecret", environment("IGDB_CLIENT_SECRET")))
                ?? FromConfig(loadConfig, config => Complete(
                    ("clientId", config.IgdbClientId),
                    ("clientSecret", config.IgdbClientSecret))),
            IntegrationProviders.Cloudinary => FromConfig(loadConfig, config => Complete(
                ("cloudName", config.CloudinaryCloudName),
                ("apiKey", config.CloudinaryApiKey),
                ("apiSecret", config.CloudinaryApiSecret))),
            _ => null,
        };
    }

    /// <summary>A partial set is never imported: the store requires every field.</summary>
    private static IReadOnlyDictionary<string, string>? Complete(params (string Field, string? Value)[] fields) =>
        fields.All(field => !string.IsNullOrWhiteSpace(field.Value))
            ? fields.ToDictionary(field => field.Field, field => field.Value!.Trim(), StringComparer.Ordinal)
            : null;

    private static IReadOnlyDictionary<string, string>? FromConfig(
        Func<Config?> loadConfig,
        Func<Config, IReadOnlyDictionary<string, string>?> read)
    {
        var config = loadConfig();
        return config is null ? null : read(config);
    }

    private static Config? LoadConfig()
    {
        try
        {
            return JsonService.LoadConfig();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
