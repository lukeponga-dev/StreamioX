// Purpose: Provides a short-lived in-memory cache with shared, cancellation-independent in-flight requests.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StreamioX.App.Services
{
    /// <summary>
    /// Caches provider responses and coalesces concurrent requests for the same key.
    /// A caller may cancel its wait without cancelling the shared fetch for other callers.
    /// Shared fetches have their own bounded lifetime.
    /// </summary>
    internal static class MemoryTtlCache
    {
        private sealed record Entry(object Value, DateTimeOffset ExpiresAt);

        private static readonly ConcurrentDictionary<string, Entry> Entries = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, Lazy<Task<object>>> Inflight = new(StringComparer.Ordinal);
        private static readonly TimeSpan SharedFetchTimeout = TimeSpan.FromSeconds(20);

        public static async Task<T> GetOrCreateAsync<T>(
            string key,
            TimeSpan ttl,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("A cache key is required.", nameof(key));
            ArgumentNullException.ThrowIfNull(factory);
            cancellationToken.ThrowIfCancellationRequested();

            if (Entries.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
                return (T)cached.Value;

            var lazyFetch = Inflight.GetOrAdd(
                key,
                _ => new Lazy<Task<object>>(
                    () => CreateAndCacheAsync(key, ttl, factory),
                    LazyThreadSafetyMode.ExecutionAndPublication));

            // WaitAsync cancels only this caller's wait. The shared fetch uses its own token.
            var value = await lazyFetch.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
            return (T)value;
        }

        private static async Task<object> CreateAndCacheAsync<T>(
            string key,
            TimeSpan ttl,
            Func<CancellationToken, Task<T>> factory)
        {
            try
            {
                using var sharedLifetime = new CancellationTokenSource(SharedFetchTimeout);
                var value = await factory(sharedLifetime.Token).ConfigureAwait(false);
                if (value is not null)
                {
                    Entries[key] = new Entry(value, DateTimeOffset.UtcNow.Add(ttl));
                    if (Entries.Count > 256)
                    {
                        var now = DateTimeOffset.UtcNow;
                        foreach (var expired in Entries
                            .Where(pair => pair.Value.ExpiresAt <= now)
                            .Select(pair => pair.Key)
                            .Take(64))
                        {
                            Entries.TryRemove(expired, out _);
                        }
                    }
                }

                return value!;
            }
            finally
            {
                Inflight.TryRemove(key, out _);
            }
        }

        public static void Remove(string key) => Entries.TryRemove(key, out _);

        public static void RemoveByPrefix(string prefix)
        {
            foreach (var key in Entries.Keys
                .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
                .ToArray())
            {
                Entries.TryRemove(key, out _);
            }
        }
    }
}
