using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace MasterPlay.Host.Auth;

public sealed class DesktopGuestSession
{
    public const string HeaderName = "X-QuestLog-Desktop-Token";
    private const string EnvironmentVariableName = "QUESTLOG_DESKTOP_SESSION_TOKEN";
    private readonly string? token;

    public DesktopGuestSession()
    {
        var configuredToken = Environment.GetEnvironmentVariable(EnvironmentVariableName)?.Trim();
        token = configuredToken is { Length: >= 32 } ? configuredToken : null;
    }

    public bool IsEnabled => token is not null;

    public bool AuthorizeExchange(HttpContext context) =>
        IsLoopback(context.Connection.RemoteIpAddress) &&
        context.Request.Headers.TryGetValue(HeaderName, out var suppliedToken) &&
        TokenEquals(suppliedToken.ToString());

    public bool AuthorizeBearer(string? suppliedToken, IPAddress? remoteAddress) =>
        IsLoopback(remoteAddress) && TokenEquals(suppliedToken);

    public GuestSessionDto CreateSession() => new(
        token ?? throw new InvalidOperationException("Desktop guest mode is not enabled."),
        DateTimeOffset.UtcNow.AddHours(24));

    private bool TokenEquals(string? suppliedToken)
    {
        if (token is null || string.IsNullOrWhiteSpace(suppliedToken)) return false;

        var expectedBytes = Encoding.UTF8.GetBytes(token);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedToken);
        return expectedBytes.Length == suppliedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }

    private static bool IsLoopback(IPAddress? address) =>
        address is not null && IPAddress.IsLoopback(address);
}
