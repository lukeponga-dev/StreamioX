using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StreamioX.App.Services
{
    /// <summary>
    /// Small process-local cache for add-on responses and short-lived playback URLs.
    /// Expired entries are removed lazily; per-key gates prevent duplicate concurrent requests.
    /// </summary>
    internal static class MemoryTtlCache
    {
        private sealed record Entry(object Value, DateTimeOffset ExpiresAt);

        private static readonly ConcurrentDictionary<string, Entry> Entries = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

        public static async Task<T> GetOrCreateAsync<T>(
            string key,
            TimeSpan ttl,
            Func<Task<T>> factory)
        {
            if (Entries.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
                return (T)cached.Value;

            var gate = Gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (Entries.TryGetValue(key, out cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
                    return (T)cached.Value;

                var value = await factory().ConfigureAwait(false);
                if (value is not null)
                {
                    Entries[key] = new Entry(value, DateTimeOffset.UtcNow.Add(ttl));
                    if (Entries.Count > 256)
                    {
                        var now = DateTimeOffset.UtcNow;
                        foreach (var expired in Entries.Where(pair => pair.Value.ExpiresAt <= now).Select(pair => pair.Key).Take(64))
                            Entries.TryRemove(expired, out _);
                    }
                }

                return value!;
            }
            finally
            {
                gate.Release();
            }
        }

        public static void Remove(string key) => Entries.TryRemove(key, out _);
        public static void RemoveByPrefix(string prefix)
        {
            foreach (var key in Entries.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
                Entries.TryRemove(key, out _);
        }
    }
}