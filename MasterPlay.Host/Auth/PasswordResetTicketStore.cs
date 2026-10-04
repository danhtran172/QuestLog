using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace MasterPlay.Host.Auth;

/// <summary>
/// Proof that a password-recovery code was verified, carried from the code step to
/// the new-password step. Only a hash of each ticket is kept; a ticket lives 10
/// minutes and is consumed when the new password is set.
/// </summary>
public sealed class PasswordResetTicketStore(TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, Entry> tickets = new(StringComparer.Ordinal);

    public PasswordResetTicketStore() : this(TimeProvider.System)
    {
    }

    public (string Ticket, DateTimeOffset ExpiresAt) Issue(string userId, string email)
    {
        PurgeExpired();
        var ticket = GoogleOAuthFlowStore.RandomToken();
        var expiresAt = clock.GetUtcNow() + Lifetime;
        tickets[Hash(ticket)] = new Entry(userId, email, expiresAt);
        return (ticket, expiresAt);
    }

    /// <summary>
    /// Takes a valid ticket out of the store so no concurrent request can use it.
    /// Pass the result to <see cref="Release"/> when the user can fix the failure
    /// (for example a password that is too short).
    /// </summary>
    public Reservation? Reserve(string? ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket) || !tickets.TryRemove(Hash(ticket), out var entry)) return null;
        return entry.ExpiresAt > clock.GetUtcNow() ? new Reservation(Hash(ticket), entry) : null;
    }

    public void Release(Reservation reservation)
    {
        if (reservation.Entry.ExpiresAt > clock.GetUtcNow())
        {
            tickets.TryAdd(reservation.Key, reservation.Entry);
        }
    }

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

    public sealed record Entry(string UserId, string Email, DateTimeOffset ExpiresAt);

    public sealed record Reservation(string Key, Entry Entry);
}
