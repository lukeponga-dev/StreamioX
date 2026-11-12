// Purpose: Implements StreamService application behavior and coordinates the related Stremio, playback, or platform APIs.

using System;
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
                    var response = await _stremioClient
                        .GetFromJsonAsync<StreamResponse>(url, cancellationToken)
                        .ConfigureAwait(false);
                    return (IReadOnlyList<StreamSource>)(response?.Streams ?? Array.Empty<StreamSource>());
                }).ConfigureAwait(false);
        }

        /// <summary>Queries each eligible stream provider and normalizes its returned sources.</summary>
        public async Task<IReadOnlyList<StreamSource>> GetFromInstalledAddonsAsync(
            string type,
            string id,
            CancellationToken cancellationToken = default,
            Func<IReadOnlyList<StreamSource>, Task>? onProviderResult = null)
        {
            await _addonService.InitializeAsync(cancellationToken).ConfigureAwait(false);
            var providers = _addonService.GetStreamProviders(type, id);
            if (providers.Count == 0)
                return Array.Empty<StreamSource>();

            // Start every provider before awaiting any of them. Each successful
            // provider publishes its results immediately, enabling the details
            // page to expose a playable source while slower add-ons continue.
            var tasks = providers.Select(async addon =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var streams = await GetStreamsAsync(addon.BaseUrl, type, id, cancellationToken)
                        .ConfigureAwait(false);
                    if (streams.Count > 0 && onProviderResult != null)
                        await onProviderResult(streams).ConfigureAwait(false);
                    return streams;
                }
                catch (OperationCanceledException) { throw; }
                catch
                {
                    // Isolate provider failures so one unavailable add-on doesn't
                    // prevent other providers from returning playable streams.
                    return (IReadOnlyList<StreamSource>)Array.Empty<StreamSource>();
                }
            }).ToArray();

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            return results.SelectMany(streams => streams).ToArray();
        }
    }
}
