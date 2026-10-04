using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace MasterPlay.Host.Auth;

public enum GoogleOAuthClient
{
    Web,
    Desktop,
}

/// <summary>
/// Pending Google sign-in flows. The PKCE verifier never leaves this process;
/// the browser only sees <c>state</c>, and only the app that started the flow
/// knows <c>flowId</c>. Flows live in memory: a Host restart drops them and the
/// user simply starts again.
/// </summary>
public sealed class GoogleOAuthFlowStore(TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    public const int Capacity = 1000;

    private readonly ConcurrentDictionary<string, GoogleOAuthFlow> flowsById = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> flowIdsByState = new(StringComparer.Ordinal);

    public GoogleOAuthFlowStore() : this(TimeProvider.System)
    {
    }

    public GoogleOAuthFlow? Create(GoogleOAuthClient client)
    {
        PurgeExpired();
        if (flowsById.Count >= Capacity) return null;

        var now = clock.GetUtcNow();
        var verifier = RandomToken();
        var flow = new GoogleOAuthFlow(
            RandomToken(),
            RandomToken(),
            verifier,
            CodeChallenge(verifier),
            client,
            now,
            now + Lifetime);

        flowsById[flow.FlowId] = flow;
        flowIdsByState[flow.State] = flow.FlowId;
        return flow;
    }

    /// <summary>Consumes <paramref name="state"/>; a second callback with it finds nothing.</summary>
    public GoogleOAuthFlow? TakeByState(string? state)
    {
        if (string.IsNullOrEmpty(state) || !flowIdsByState.TryRemove(state, out var flowId)) return null;
        return Get(flowId);
    }

    public GoogleOAuthFlow? Get(string? flowId)
    {
        if (string.IsNullOrEmpty(flowId) || !flowsById.TryGetValue(flowId, out var flow)) return null;
        if (flow.ExpiresAt > clock.GetUtcNow()) return flow;

        Remove(flow);
        return null;
    }

    public void Remove(GoogleOAuthFlow flow)
    {
        flowsById.TryRemove(flow.FlowId, out _);
        flowIdsByState.TryRemove(flow.State, out _);
    }

    private void PurgeExpired()
    {
        var now = clock.GetUtcNow();
        foreach (var flow in flowsById.Values.Where(flow => flow.ExpiresAt <= now).ToList())
        {
            Remove(flow);
        }
    }

    public static string CodeChallenge(string verifier) =>
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    internal static string RandomToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    internal static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public sealed class GoogleOAuthFlow(
    string flowId,
    string state,
    string codeVerifier,
    string codeChallenge,
    GoogleOAuthClient client,
    DateTimeOffset createdAt,
    DateTimeOffset expiresAt)
{
    private readonly object gate = new();
    private string? authorizationCode;
    private bool cancelled;
    private bool exchangeStarted;

    public string FlowId { get; } = flowId;
    public string State { get; } = state;
    public string CodeVerifier { get; } = codeVerifier;
    public string CodeChallenge { get; } = codeChallenge;
    public GoogleOAuthClient Client { get; } = client;
    public DateTimeOffset CreatedAt { get; } = createdAt;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;

    public void Authorize(string code)
    {
        lock (gate) authorizationCode ??= code;
    }

    public void Cancel()
    {
        lock (gate) cancelled = true;
    }

    /// <summary>
    /// Hands the authorization code to exactly one caller. Concurrent polls see
    /// <see cref="GoogleFlowProgress.Pending"/> until that caller finishes.
    /// </summary>
    public (GoogleFlowProgress Progress, string? Code) TryBeginExchange()
    {
        lock (gate)
        {
            if (cancelled) return (GoogleFlowProgress.Cancelled, null);
            if (authorizationCode is null || exchangeStarted) return (GoogleFlowProgress.Pending, null);
            exchangeStarted = true;
            return (GoogleFlowProgress.Authorized, authorizationCode);
        }
    }
}

public enum GoogleFlowProgress
{
    Pending,
    Authorized,
    Cancelled,
}
