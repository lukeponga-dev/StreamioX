using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;

namespace StreamioX.App.Services
{
    /// <summary>
    /// Queries installed Stremio catalog add-ons.
    /// </summary>
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
            if (string.IsNullOrWhiteSpace(addonBaseUrl)
                || string.IsNullOrWhiteSpace(type)
                || string.IsNullOrWhiteSpace(catalogId))
            {
                return Array.Empty<Movie>();
            }

            var url = $"{addonBaseUrl.TrimEnd('/')}/catalog/{Uri.EscapeDataString(type)}/{Uri.EscapeDataString(catalogId)}.json";
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

            var results = new List<Movie>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var providerErrors = new List<string>();
            var providers = _addonService.GetCatalogProviders(type);

            foreach (var addon in providers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var matchingCatalogs = addon.Catalogs
                    .Where(c => string.Equals(c.Type, type, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var catalog = matchingCatalogs.FirstOrDefault(c =>
                    !string.IsNullOrWhiteSpace(preferredCatalogId)
                    && string.Equals(c.Id, preferredCatalogId, StringComparison.OrdinalIgnoreCase))
                    ?? matchingCatalogs.FirstOrDefault();

                if (catalog == null || string.IsNullOrWhiteSpace(catalog.Id))
                    continue;

                try
                {
                    var movies = await GetCatalogAsync(addon.BaseUrl, type, catalog.Id, cancellationToken)
                        .ConfigureAwait(false);

                    foreach (var movie in movies)
                    {
                        if (movie != null && !string.IsNullOrWhiteSpace(movie.Id) && seenIds.Add(movie.Id))
                            results.Add(movie);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Continue through other providers, but preserve the reason if all fail.
                    providerErrors.Add($"{addon.Name}: {ex.Message}");
                }
            }

            if (results.Count == 0)
            {
                if (providers.Count == 0)
                    throw new InvalidOperationException(
                        $"No installed add-on exposes a {type} catalog. Check the Add-ons page.");

                if (providerErrors.Count > 0)
                    throw new InvalidOperationException(
                        "Catalog providers returned no results. " + string.Join(" | ", providerErrors));

                throw new InvalidOperationException(
                    $"Installed catalog providers did not return any {type} items.");
            }

            return results;
        }
    }
}
