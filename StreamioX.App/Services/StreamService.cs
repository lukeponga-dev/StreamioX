using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;

namespace StreamioX.App.Services
{
    public sealed class StreamService : IStreamService
    {
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

            var response = await _stremioClient
                .GetFromJsonAsync<StreamResponse>(url, cancellationToken)
                .ConfigureAwait(false);

            return response?.Streams ?? Array.Empty<StreamSource>();
        }

        public async Task<IReadOnlyList<StreamSource>> GetFromInstalledAddonsAsync(
            string type,
            string id,
            CancellationToken cancellationToken = default)
        {
            await _addonService.InitializeAsync(cancellationToken).ConfigureAwait(false);

            var providers = _addonService.GetStreamProviders(type, id);
            var results = new List<StreamSource>();

            foreach (var addon in providers)
            {
                try
                {
                    var streams = await GetStreamsAsync(addon.BaseUrl, type, id, cancellationToken)
                        .ConfigureAwait(false);
                    results.AddRange(streams);
                }
                catch
                {
                    // Skip failed provider
                }
            }

            return results;
        }
    }
}
