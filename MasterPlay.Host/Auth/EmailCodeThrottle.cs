using System.Collections.Concurrent;

namespace MasterPlay.Host.Auth;

/// <summary>
/// Per-account resend cooldown and wrong-code counter for emailed 6-digit codes.
/// Keys are account IDs or normalized emails; they never leave the process.
/// </summary>
public sealed class EmailCodeThrottle(TimeProvider clock)
{
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    public const int MaxFailedAttempts = 5;

    private readonly ConcurrentDictionary<string, DateTimeOffset> lastSent = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> failures = new(StringComparer.Ordinal);

    public EmailCodeThrottle() : this(TimeProvider.System)
    {
    }

    /// <summary>Records a send if the cooldown has passed. Returns when the next send is allowed.</summary>
    public bool TryRecordSend(string key, out DateTimeOffset nextAllowedAt)
    {
        var now = clock.GetUtcNow();
        var allowed = true;
        var recorded = lastSent.AddOrUpdate(
            key,
            now,
            (_, previous) =>
            {
                if (previous + ResendCooldown > now)
                {
                    allowed = false;
                    return previous;
                }

                return now;
            });

        nextAllowedAt = recorded + ResendCooldown;
        if (allowed) failures.TryRemove(key, out _);
        return allowed;
    }

    public bool AttemptsExceeded(string key) =>
        failures.TryGetValue(key, out var count) && count >= MaxFailedAttempts;

    public void RecordFailure(string key) => failures.AddOrUpdate(key, 1, (_, count) => count + 1);

    public void RecordSuccess(string key) => failures.TryRemove(key, out _);
}
