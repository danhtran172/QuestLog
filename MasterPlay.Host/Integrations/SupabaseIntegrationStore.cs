using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MasterPlay.Host.Auth;
using MasterPlay.Infrastructure.Integrations;

namespace MasterPlay.Host.Integrations;

public static class IntegrationProviders
{
    public const string Rawg = "rawg";
    public const string Igdb = "igdb";
    public const string Steam = "steam";
    public const string Cloudinary = "cloudinary";
    public const string Gemini = "gemini";

    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        Rawg, Igdb, Steam, Cloudinary, Gemini,
    };

    public static bool IsKnown(string provider) => Known.Contains(provider);

    /// <summary>
    /// Public identifiers that are not secrets (the cloud name appears in every image
    /// URL; the Twitch client ID is sent in every IGDB request header). Only these may
    /// be echoed back to the Admin Console; every other field stays write-only.
    /// </summary>
    private static readonly Dictionary<string, string[]> IdentifierFields = new(StringComparer.Ordinal)
    {
        [Cloudinary] = ["cloudName"],
        [Igdb] = ["clientId"],
    };

    public static IReadOnlyList<string> Identifiers(string provider) =>
        IdentifierFields.TryGetValue(provider, out var fields) ? fields : [];
}

public sealed record IntegrationLastTestDto(
    string Status,
    DateTimeOffset? TestedAt,
    string? ErrorCode);

public sealed record IntegrationSummaryDto(
    string Provider,
    bool Configured,
    IReadOnlyList<string> RequiredFields,
    IReadOnlyList<string> ConfiguredFields,
    long Revision,
    IntegrationLastTestDto LastTest,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy)
{
    /// <summary>Non-secret identifier values only; see <see cref="IntegrationProviders.Identifiers"/>.</summary>
    public IReadOnlyDictionary<string, string> Identifiers { get; init; } = new Dictionary<string, string>();
}

public sealed record IntegrationCredentials(long Revision, IReadOnlyDictionary<string, string> Values);

public sealed class IntegrationStoreException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

/// <summary>
/// The only Host component allowed to ask Supabase for integration plaintext.
/// Values exist only in this process long enough to call the provider; callers must
/// never log, persist, or return them.
/// </summary>
public sealed class SupabaseIntegrationStore(
    IHttpClientFactory httpClientFactory,
    SupabaseAuthOptions options,
    ILogger<SupabaseIntegrationStore> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<IntegrationSummaryDto>> ListAsync(CancellationToken cancellationToken)
    {
        var value = await CallAsync("admin_list_integrations", new { }, cancellationToken);
        var array = Unwrap(value);
        if (array.ValueKind != JsonValueKind.Array)
            throw new IntegrationStoreException(503, "INTEGRATION_STORE_INVALID", "Integration configuration returned an invalid response.");

        return array.Deserialize<List<IntegrationSummaryDto>>(JsonOptions) ?? [];
    }

    public async Task<IntegrationSummaryDto> ReplaceAsync(
        string provider,
        IReadOnlyDictionary<string, string> values,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        // A null actor marks a system import (legacy credentials seeded at startup).
        EnsureProvider(provider);
        var value = await CallAsync(
            "admin_replace_integration_credentials",
            new { p_provider = provider, p_values = values, p_actor = actorUserId },
            cancellationToken);
        var summary = DeserializeSummary(Unwrap(value));
        RuntimeIntegrationCredentials.Replace(provider, summary.Revision, values);
        return summary;
    }

    /// <summary>
    /// Adds the provider's public identifier values to a summary. Uses the in-process
    /// cache when it holds the summary's revision, otherwise reloads from Vault.
    /// </summary>
    public async Task<IntegrationSummaryDto> WithIdentifiersAsync(
        IntegrationSummaryDto summary,
        CancellationToken cancellationToken)
    {
        var fields = IntegrationProviders.Identifiers(summary.Provider);
        if (!summary.Configured || fields.Count == 0) return summary;

        if (RuntimeIntegrationCredentials.Revision(summary.Provider) < summary.Revision)
            await ReadCredentialsAsync(summary.Provider, cancellationToken);

        var identifiers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (RuntimeIntegrationCredentials.TryGet(summary.Provider, field, out var value))
                identifiers[field] = value!;
        }
        return summary with { Identifiers = identifiers };
    }

    public async Task<IntegrationCredentials> ReadCredentialsAsync(
        string provider,
        CancellationToken cancellationToken)
    {
        EnsureProvider(provider);
        var value = Unwrap(await CallAsync(
            "admin_read_integration_credentials",
            new { p_provider = provider },
            cancellationToken));
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            throw new IntegrationStoreException(422, "INTEGRATION_NOT_CONFIGURED", "Configure this integration before testing it.");

        var result = value.Deserialize<IntegrationCredentialsRow>(JsonOptions);
        if (result?.Revision is not > 0 || result.Values is null || result.Values.Count == 0)
            throw new IntegrationStoreException(422, "INTEGRATION_NOT_CONFIGURED", "Configure this integration before testing it.");
        var credentials = new IntegrationCredentials(result.Revision, result.Values);
        RuntimeIntegrationCredentials.Replace(provider, credentials.Revision, credentials.Values);
        return credentials;
    }

    public async Task<IntegrationSummaryDto> RecordTestAsync(
        string provider,
        bool passed,
        string? errorCode,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        EnsureProvider(provider);
        var value = await CallAsync(
            "admin_record_integration_test",
            new { p_provider = provider, p_passed = passed, p_error_code = errorCode, p_actor = actorUserId },
            cancellationToken);
        return DeserializeSummary(Unwrap(value));
    }

    private async Task<JsonElement> CallAsync(string function, object payload, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.Url.TrimEnd('/')}/rest/v1/rpc/{function}")
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("apikey", options.ServiceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ServiceRoleKey);

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return document.RootElement.Clone();
        }

        // Never record response bodies: a provider/Vault failure must not turn a
        // credential-bearing payload into a server log entry.
        logger.LogWarning("Integration store RPC {Function} failed with {StatusCode}", function, (int)response.StatusCode);

        // PostgREST returns 404 when an RPC is not present. In this path every
        // RPC is created by 202610030001_admin_integrations.sql, so retrying in
        // the UI can never recover it. Give operators the actionable fix while
        // still keeping the database response body out of logs and responses.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new IntegrationStoreException(
                503,
                "INTEGRATION_SCHEMA_OUTDATED",
                "The integration database is missing a function this app version needs. Apply the pending Supabase migration.");
        }

        throw new IntegrationStoreException(
            503,
            "INTEGRATION_STORE_UNAVAILABLE",
            "Integration configuration is temporarily unavailable.");
    }

    private void EnsureConfigured()
    {
        if (!options.IsConfigured)
            throw new IntegrationStoreException(503, "INTEGRATION_STORE_NOT_CONFIGURED", "Integration configuration is not available on this server.");
    }

    private static JsonElement Unwrap(JsonElement value)
    {
        // PostgREST returns a JSON scalar directly for current deployments, while
        // older deployments wrap scalar RPC results in a one-element row array.
        return value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 1
            ? value[0].Clone()
            : value;
    }

    private static IntegrationSummaryDto DeserializeSummary(JsonElement value) =>
        value.Deserialize<IntegrationSummaryDto>(JsonOptions)
        ?? throw new IntegrationStoreException(503, "INTEGRATION_STORE_INVALID", "Integration configuration returned an invalid response.");

    private static void EnsureProvider(string provider)
    {
        if (!IntegrationProviders.IsKnown(provider))
            throw new IntegrationStoreException(422, "INTEGRATION_PROVIDER_UNKNOWN", "That integration is not supported.");
    }

    private HttpClient HttpClient => httpClientFactory.CreateClient(nameof(SupabaseIntegrationStore));

    private sealed record IntegrationCredentialsRow(
        long Revision,
        IReadOnlyDictionary<string, string>? Values);
}

public sealed record IntegrationTestResult(bool Passed, string? ErrorCode);

/// <summary>Read-only credential validation calls. No upstream response body is kept.</summary>
public sealed class IntegrationConnectionTester(
    SupabaseIntegrationStore store,
    IHttpClientFactory httpClientFactory)
{
    public async Task<IntegrationTestResult> TestAsync(string provider, CancellationToken cancellationToken)
    {
        var credentials = await store.ReadCredentialsAsync(provider, cancellationToken);
        try
        {
            return provider switch
            {
                IntegrationProviders.Rawg => await TestRawgAsync(credentials, cancellationToken),
                IntegrationProviders.Igdb => await TestIgdbAsync(credentials, cancellationToken),
                IntegrationProviders.Steam => await TestSteamAsync(credentials, cancellationToken),
                IntegrationProviders.Cloudinary => await TestCloudinaryAsync(credentials, cancellationToken),
                IntegrationProviders.Gemini => await TestGeminiAsync(credentials, cancellationToken),
                _ => new IntegrationTestResult(false, "INTEGRATION_PROVIDER_UNKNOWN"),
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new IntegrationTestResult(false, "INTEGRATION_UPSTREAM_UNAVAILABLE");
        }
        catch (HttpRequestException)
        {
            return new IntegrationTestResult(false, "INTEGRATION_UPSTREAM_UNAVAILABLE");
        }
    }

    private async Task<IntegrationTestResult> TestRawgAsync(IntegrationCredentials credentials, CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(
            $"https://api.rawg.io/api/platforms?key={Uri.EscapeDataString(Value(credentials, "apiKey"))}&page_size=1",
            cancellationToken);
        return FromStatus(response.StatusCode);
    }

    private async Task<IntegrationTestResult> TestIgdbAsync(IntegrationCredentials credentials, CancellationToken cancellationToken)
    {
        var clientId = Value(credentials, "clientId");
        var clientSecret = Value(credentials, "clientSecret");
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://id.twitch.tv/oauth2/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "client_credentials",
            }),
        };
        using var tokenResponse = await Client.SendAsync(tokenRequest, cancellationToken);
        if (!tokenResponse.IsSuccessStatusCode) return FromStatus(tokenResponse.StatusCode);
        using var tokenDocument = await JsonDocument.ParseAsync(await tokenResponse.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!tokenDocument.RootElement.TryGetProperty("access_token", out var token) || string.IsNullOrWhiteSpace(token.GetString()))
            return new IntegrationTestResult(false, "INTEGRATION_TEST_FAILED");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/games")
        {
            Content = new StringContent("fields id; limit 1;", Encoding.UTF8, "text/plain"),
        };
        request.Headers.Add("Client-ID", clientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.GetString());
        using var response = await Client.SendAsync(request, cancellationToken);
        return FromStatus(response.StatusCode);
    }

    private async Task<IntegrationTestResult> TestSteamAsync(IntegrationCredentials credentials, CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(
            $"https://api.steampowered.com/ISteamWebAPIUtil/GetServerInfo/v1/?key={Uri.EscapeDataString(Value(credentials, "apiKey"))}",
            cancellationToken);
        return FromStatus(response.StatusCode);
    }

    private async Task<IntegrationTestResult> TestCloudinaryAsync(IntegrationCredentials credentials, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.cloudinary.com/v1_1/{Uri.EscapeDataString(Value(credentials, "cloudName"))}/resources/image?max_results=1");
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Value(credentials, "apiKey")}:{Value(credentials, "apiSecret")}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        using var response = await Client.SendAsync(request, cancellationToken);
        return FromStatus(response.StatusCode);
    }

    private async Task<IntegrationTestResult> TestGeminiAsync(IntegrationCredentials credentials, CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(
            $"https://generativelanguage.googleapis.com/v1beta/models?key={Uri.EscapeDataString(Value(credentials, "apiKey"))}",
            cancellationToken);
        return FromStatus(response.StatusCode);
    }

    private static string Value(IntegrationCredentials credentials, string name) =>
        credentials.Values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new IntegrationStoreException(422, "INTEGRATION_NOT_CONFIGURED", "Configure this integration before testing it.");

    private static IntegrationTestResult FromStatus(HttpStatusCode status) =>
        status is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices
            ? new IntegrationTestResult(true, null)
            : status == HttpStatusCode.TooManyRequests
                ? new IntegrationTestResult(false, "INTEGRATION_RATE_LIMITED")
                : status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? new IntegrationTestResult(false, "INTEGRATION_CREDENTIAL_INVALID")
                    : (int)status >= 500
                        ? new IntegrationTestResult(false, "INTEGRATION_UPSTREAM_UNAVAILABLE")
                        : new IntegrationTestResult(false, "INTEGRATION_TEST_FAILED");

    private HttpClient Client => httpClientFactory.CreateClient(nameof(IntegrationConnectionTester));
}
