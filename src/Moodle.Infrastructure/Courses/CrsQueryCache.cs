using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Moodle.UnitTests")]

namespace Moodle.Infrastructure.Courses;

/// <summary>
/// Small instance-local cache of unfiltered CRS data, never authorization decisions.
/// The owner supplies a committed outbox stamp and detached copies of mutable values.
/// Like its owning DbContext, this object must not be used concurrently.
/// </summary>
internal sealed class CrsQueryCache
{
    internal readonly record struct Stamp(long Count, long LastId);
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private const int Capacity = 64;

    public async Task<T> ReadAsync<T>(string key, bool bypass, Func<Task<Stamp>> revision,
        Func<Task<T>> load, Func<T, T> copy)
    {
        if (bypass) return await load();
        // Check even on hits: never rely on an asynchronously dispatched notification for freshness.
        // Count as well as MAX(Id) detects a lower sequence ID committing after a higher one.
        var stamp = await revision();
        if (entries.TryGetValue(key, out var entry) && entry.Stamp == stamp)
            return copy((T)entry.Value!);

        var value = await load(); // On failure leave any existing entry untouched; never return it as fallback.
        if (!entries.ContainsKey(key) && entries.Count >= Capacity)
            entries.Remove(entries.Keys.First());
        entries[key] = new(stamp, copy(value));
        return value;
    }

    private sealed record Entry(Stamp Stamp, object? Value);
}
