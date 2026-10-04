using System.Collections.Concurrent;

namespace MasterPlay.Infrastructure.Integrations;

/// <summary>
/// Server-process cache populated exclusively by the Host's Vault-backed store.
/// It intentionally has no persistence, serialization, or logging surface.
/// A process restart reloads the active configuration from Vault during Host startup.
/// </summary>
public static class RuntimeIntegrationCredentials
{
    private sealed record Entry(long Revision, IReadOnlyDictionary<string, string> Values);

    private static readonly ConcurrentDictionary<string, Entry> Entries = new(StringComparer.Ordinal);

    public static void Replace(string provider, long revision, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrWhiteSpace(provider) || revision <= 0) return;
        Entries.AddOrUpdate(
            provider,
            _ => new Entry(revision, new Dictionary<string, string>(values, StringComparer.Ordinal)),
            (_, current) => current.Revision > revision
                ? current
                : new Entry(revision, new Dictionary<string, string>(values, StringComparer.Ordinal)));
    }

    public static bool TryGet(string provider, string field, out string? value)
    {
        value = null;
        return Entries.TryGetValue(provider, out var entry)
            && entry.Values.TryGetValue(field, out var found)
            && !string.IsNullOrWhiteSpace(value = found);
    }

    public static long Revision(string provider) =>
        Entries.TryGetValue(provider, out var entry) ? entry.Revision : 0;
}
