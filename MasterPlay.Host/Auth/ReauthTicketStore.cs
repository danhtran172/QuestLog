using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace MasterPlay.Host.Auth;

/// <summary>
/// Short-lived proof that the signed-in user just entered an email code.
/// Only a hash of each ticket is kept. A ticket is bound to the account and to
/// the Supabase session that requested it, and is consumed by its first
/// successful use.
/// </summary>
public sealed class ReauthTicketStore(TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, Entry> tickets = new(StringComparer.Ordinal);

    public ReauthTicketStore() : this(TimeProvider.System)
    {
    }

    public (string Ticket, DateTimeOffset ExpiresAt) Issue(string userId, string sessionId)
    {
        PurgeExpired();
        var ticket = GoogleOAuthFlowStore.RandomToken();
        var expiresAt = clock.GetUtcNow() + Lifetime;
        tickets[Hash(ticket)] = new Entry(userId, sessionId, expiresAt);
        return (ticket, expiresAt);
    }

    /// <summary>Checks a ticket without consuming it.</summary>
    public bool IsValid(string? ticket, string userId, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(ticket) || !tickets.TryGetValue(Hash(ticket), out var entry)) return false;
        if (entry.ExpiresAt <= clock.GetUtcNow())
        {
            tickets.TryRemove(Hash(ticket), out _);
            return false;
        }

        return string.Equals(entry.UserId, userId, StringComparison.Ordinal) &&
               string.Equals(entry.SessionId, sessionId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Takes a valid ticket out of the store so no concurrent request can use it.
    /// Pass the result to <see cref="Release"/> when the protected operation fails
    /// for a reason the user can fix (for example a username conflict).
    /// </summary>
    public Reservation? Reserve(string? ticket, string userId, string sessionId)
    {
        if (!IsValid(ticket, userId, sessionId) || !tickets.TryRemove(Hash(ticket!), out var entry)) return null;
        return new Reservation(Hash(ticket!), entry);
    }

    public void Release(Reservation reservation)
    {
        if (reservation.Entry.ExpiresAt > clock.GetUtcNow())
        {
            tickets.TryAdd(reservation.Key, reservation.Entry);
        }
    }

    public sealed record Reservation(string Key, Entry Entry);

    private void PurgeExpired()
    {
        var now = clock.GetUtcNow();
        foreach (var pair in tickets.Where(pair => pair.Value.ExpiresAt <= now).ToList())
        {
            tickets.TryRemove(pair.Key, out _);
        }
    }

    private static string Hash(string ticket) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ticket)));

    public sealed record Entry(string UserId, string SessionId, DateTimeOffset ExpiresAt);
}
