using MasterPlay.Core.Entities;
using MasterPlay.Host.Integrations;

namespace MasterPlay.Host.Tests;

public sealed class LegacyIntegrationCredentialsTests
{
    private static Func<string, string?> Env(Dictionary<string, string> values) =>
        name => values.TryGetValue(name, out var value) ? value : null;

    [Fact]
    public void Cloudinary_is_read_from_config_json()
    {
        var config = new Config { CloudinaryCloudName = " demo ", CloudinaryApiKey = "key", CloudinaryApiSecret = "secret" };

        var values = LegacyIntegrationCredentials.Read(IntegrationProviders.Cloudinary, Env([]), () => config);

        Assert.NotNull(values);
        Assert.Equal("demo", values["cloudName"]);
        Assert.Equal(["apiKey", "apiSecret", "cloudName"], values.Keys.Order());
    }

    [Fact]
    public void Partial_credential_set_is_never_imported()
    {
        var config = new Config { CloudinaryCloudName = "demo", CloudinaryApiKey = "key" };

        Assert.Null(LegacyIntegrationCredentials.Read(IntegrationProviders.Cloudinary, Env([]), () => config));
        Assert.Null(LegacyIntegrationCredentials.Read(
            IntegrationProviders.Igdb, Env(new() { ["IGDB_CLIENT_ID"] = "id" }), () => new Config()));
    }

    [Fact]
    public void Igdb_prefers_environment_over_config_json()
    {
        var env = Env(new() { ["IGDB_CLIENT_ID"] = "env-id", ["IGDB_CLIENT_SECRET"] = "env-secret" });
        var config = new Config { IgdbClientId = "cfg-id", IgdbClientSecret = "cfg-secret" };

        var values = LegacyIntegrationCredentials.Read(IntegrationProviders.Igdb, env, () => config);

        Assert.Equal("env-id", values!["clientId"]);
        Assert.Equal("cfg-id", LegacyIntegrationCredentials.Read(IntegrationProviders.Igdb, Env([]), () => config)!["clientId"]);
    }

    [Fact]
    public void Single_key_providers_read_their_environment_variable()
    {
        var env = Env(new() { ["RAWG_API_KEY"] = "r", ["GEMINI_API_KEY"] = "g" });

        Assert.Equal("r", LegacyIntegrationCredentials.Read(IntegrationProviders.Rawg, env, () => null)!["apiKey"]);
        Assert.Equal("g", LegacyIntegrationCredentials.Read(IntegrationProviders.Gemini, env, () => null)!["apiKey"]);
        Assert.Null(LegacyIntegrationCredentials.Read(IntegrationProviders.Steam, env, () => null));
    }
}
