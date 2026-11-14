// Purpose: Queries installed Stremio stream add-ons concurrently and publishes provider results as they arrive.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;

namespace StreamioX.App.Services
{
    public sealed class StreamService : IStreamService
    {
        private static readonly TimeSpan StreamResponseTtl = TimeSpan.FromSeconds(45);
        private static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(12);
        private static readonly ConcurrentDictionary<string, long> ProviderLatencyMs = new(StringComparer.OrdinalIgnoreCase);
        private readonly StremioClient _stremioClient;
        private readonly IAddonService _addonService;

        public StreamService(StremioClient stremioClient, IAddonService addonService)
        {
            _stremioClient = stremioClient ?? throw new ArgumentNullException(nameof(stremioClient));
            _addonService = addonService ?? throw new ArgumentNullException(nameof(addonService));
        }

        public async Task<IReadOnlyList<StreamSource>> GetStreamsAsync(
            string addonBaseUrl,
            string type,
            string id,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(addonBaseUrl) || string.IsNullOrWhiteSpace(id))
                return Array.Empty<StreamSource>();

            string url = $"{addonBaseUrl.TrimEnd('/')}/stream/{type}/{id}.json";
            var cacheKey = "addon-stream:" + url;

            return await MemoryTtlCache.GetOrCreateAsync(
                cacheKey,
                StreamResponseTtl,
                async () =>
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(ProviderTimeout);
                    var response = await _stremioClient
                        .GetFromJsonAsync<StreamResponse>(url, timeout.Token)
                        .ConfigureAwait(false);
                    return (IReadOnlyList<StreamSource>)(response?.Streams ?? Array.Empty<StreamSource>());
                },
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Starts independent add-on requests together and publishes each provider outcome immediately.</summary>
        public async Task<IReadOnlyList<StreamSource>> GetFromInstalledAddonsAsync(
            string type,
            string id,
            CancellationToken cancellationToken = default,
            Func<IReadOnlyList<StreamSource>, Task>? onProviderResult = null,
            Func<string, string, Task>? onProviderStatus = null)
        {
            await _addonService.InitializeAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var providers = _addonService.GetStreamProviders(type, id)
                .OrderBy(provider => ProviderLatencyMs.TryGetValue(provider.Id, out var latency) ? latency : 5000)
                .ToArray();
            if (providers.Length == 0)
                return Array.Empty<StreamSource>();

            var tasks = providers.Select(async addon =>
            {
                var providerName = string.IsNullOrWhiteSpace(addon.Name) ? addon.Id : addon.Name;
                var started = DateTimeOffset.UtcNow;
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(ProviderTimeout);

                try
                {
                    if (onProviderStatus != null)
                        await onProviderStatus(providerName, "Searching").ConfigureAwait(false);

                    var streams = await GetStreamsAsync(addon.BaseUrl, type, id, timeout.Token)
                        .ConfigureAwait(false);
                    ProviderLatencyMs[addon.Id] = Math.Max(1, (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds);

                    if (onProviderResult != null)
                        await onProviderResult(streams).ConfigureAwait(false);
                    if (onProviderStatus != null)
                        await onProviderStatus(providerName, "Complete").ConfigureAwait(false);

                    return streams;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    if (onProviderStatus != null)
                        await onProviderStatus(providerName, "Cancelled").ConfigureAwait(false);
                    throw;
                }
                catch (OperationCanceledException)
                {
                    if (onProviderStatus != null)
                        await onProviderStatus(providerName, "Timed out").ConfigureAwait(false);
                    return (IReadOnlyList<StreamSource>)Array.Empty<StreamSource>();
                }
                catch (Exception)
                {
                    if (onProviderStatus != null)
                        await onProviderStatus(providerName, "Failed").ConfigureAwait(false);
                    return (IReadOnlyList<StreamSource>)Array.Empty<StreamSource>();
                }
            }).ToArray();

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return results.SelectMany(result => result)
                .Where(stream =>
                {
                    var key = !string.IsNullOrWhiteSpace(stream.Url) ? stream.Url.Trim()
                        : !string.IsNullOrWhiteSpace(stream.InfoHash) ? stream.InfoHash.Trim()
                        : stream.Title?.Trim() ?? string.Empty;
                    return key.Length > 0 && seen.Add(key);
                })
                .ToArray();
        }
    }
}
