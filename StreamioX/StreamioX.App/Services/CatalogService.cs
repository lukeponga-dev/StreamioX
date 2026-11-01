using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;

namespace StreamioX.App.Services
{
    public sealed class CatalogService : ICatalogService
    {
        private readonly StremioClient _stremioClient;
        private readonly IAddonService _addonService;

        public CatalogService(StremioClient stremioClient, IAddonService addonService)
        {
            _stremioClient = stremioClient ?? throw new ArgumentNullException(nameof(stremioClient));
            _addonService = addonService ?? throw new ArgumentNullException(nameof(addonService));
        }

        public async Task<IReadOnlyList<Movie>> GetCatalogAsync(
            string addonBaseUrl,
            string type,
            string catalogId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(addonBaseUrl))
                return Array.Empty<Movie>();

            string url = $"{addonBaseUrl.TrimEnd('/')}/catalog/{type}/{catalogId}.json";

            var response = await _stremioClient
                .GetFromJsonAsync<CatalogResponse>(url, cancellationToken)
                .ConfigureAwait(false);

            return response?.Metas ?? Array.Empty<Movie>();
        }

        public async Task<IReadOnlyList<Movie>> GetFromInstalledAddonsAsync(
            string type = "movie",
            string? preferredCatalogId = null,
            CancellationToken cancellationToken = default)
        {
            await _addonService.InitializeAsync(cancellationToken).ConfigureAwait(false);

            var providers = _addonService.GetCatalogProviders(type);
            var results = new List<Movie>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var addon in providers)
            {
                var catalogs = addon.Catalogs
                    .Where(c => string.IsNullOrEmpty(c.Type)
                                || string.Equals(c.Type, type, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (catalogs.Count == 0)
                {
                    // Fallback common ids
                    catalogs.Add(new CatalogDescriptor { Type = type, Id = preferredCatalogId ?? "top" });
                }

                foreach (var catalog in catalogs)
                {
                    var catalogId = catalog.Id ?? preferredCatalogId ?? "top";
                    try
                    {
                        var items = await GetCatalogAsync(addon.BaseUrl, type, catalogId, cancellationToken)
                            .ConfigureAwait(false);
                        foreach (var item in items)
                        {
                            if (item.Id != null && seen.Add(item.Id))
                                results.Add(item);
                        }
                    }
                    catch
                    {
                        // Skip failed addon, try next
                    }

                    // Prefer first successful catalog per addon for board-style feed
                    if (results.Count > 0 && preferredCatalogId == null)
                        break;
                }

                if (results.Count >= 50)
                    break;
            }

            return results;
        }
    }
}
