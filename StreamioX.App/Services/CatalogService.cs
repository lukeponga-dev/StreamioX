using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;

namespace StreamioX.App.Services
{
    /// <summary>Queries installed Stremio-compatible catalog add-ons.</summary>
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
            string addonBaseUrl, string type, string catalogId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(addonBaseUrl) ||
                string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(catalogId))
                return Array.Empty<Movie>();

            var url = $"{addonBaseUrl.TrimEnd('/')}/catalog/{Uri.EscapeDataString(type)}/{Uri.EscapeDataString(catalogId)}.json";
            var response = await _stremioClient.GetFromJsonAsync<CatalogResponse>(url, cancellationToken)
                .ConfigureAwait(false);
            return response?.Metas ?? Array.Empty<Movie>();
        }

        public async Task<IReadOnlyList<Movie>> GetFromInstalledAddonsAsync(
            string type = "movie", string? preferredCatalogId = null,
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
                    .Where(c => string.Equals(c.Type, type, StringComparison.OrdinalIgnoreCase)).ToList();
                var catalog = matchingCatalogs.FirstOrDefault(c =>
                    !string.IsNullOrWhiteSpace(preferredCatalogId) &&
                    string.Equals(c.Id, preferredCatalogId, StringComparison.OrdinalIgnoreCase))
                    ?? matchingCatalogs.FirstOrDefault();
                if (catalog == null || string.IsNullOrWhiteSpace(catalog.Id)) continue;

                try
                {
                    var items = await GetCatalogAsync(addon.BaseUrl, type, catalog.Id, cancellationToken)
                        .ConfigureAwait(false);
                    foreach (var item in items)
                        if (item != null && !string.IsNullOrWhiteSpace(item.Id) && seenIds.Add(item.Id))
                            results.Add(item);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { providerErrors.Add($"{addon.Name}: {ex.Message}"); }
            }

            if (results.Count == 0)
            {
                if (providers.Count == 0)
                    throw new InvalidOperationException($"No installed add-on exposes a {type} catalog. Check the Add-ons page.");
                if (providerErrors.Count > 0)
                    throw new InvalidOperationException("Catalog providers returned no results. " + string.Join(" | ", providerErrors));
                throw new InvalidOperationException($"Installed catalog providers did not return any {type} items.");
            }
            return results;
        }

        public async Task<IReadOnlyList<Movie>> SearchInstalledAddonsAsync(
            string query, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query)) return Array.Empty<Movie>();

            await _addonService.InitializeAsync(cancellationToken).ConfigureAwait(false);

            var results = new List<Movie>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var errors = new List<string>();
            var searchedCatalogs = 0;

            // Search is a catalog extra, not a universal endpoint. Only request catalogs
            // whose manifest explicitly advertises the "search" extra.
            foreach (var addon in _addonService.GetCatalogProviders())
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var catalog in addon.Catalogs)
                {
                    if (string.IsNullOrWhiteSpace(catalog.Type) || string.IsNullOrWhiteSpace(catalog.Id))
                        continue;

                    if (addon.Types.Count > 0 &&
                        !addon.Types.Any(t => string.Equals(t, catalog.Type, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var extras = catalog.Extra ?? new List<CatalogExtra>();
                    var searchExtra = extras.FirstOrDefault(extra =>
                        string.Equals(extra.Name, "search", StringComparison.OrdinalIgnoreCase));
                    if (searchExtra == null)
                        continue;

                    // A catalog may require additional extras (genre, skip, etc.). Do not
                    // send an incomplete request or invent values for those parameters.
                    if (extras.Any(extra => extra.IsRequired &&
                        !string.Equals(extra.Name, "search", StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var encodedQuery = Uri.EscapeDataString(query.Trim());
                    var url = $"{addon.BaseUrl.TrimEnd('/')}/catalog/{Uri.EscapeDataString(catalog.Type)}/{Uri.EscapeDataString(catalog.Id)}/search={encodedQuery}.json";
                    searchedCatalogs++;

                    try
                    {
                        var response = await _stremioClient.GetFromJsonAsync<CatalogResponse>(url, cancellationToken)
                            .ConfigureAwait(false);

                        foreach (var item in response?.Metas ?? Array.Empty<Movie>())
                        {
                            if (item == null || string.IsNullOrWhiteSpace(item.Id))
                                continue;

                            // IDs can overlap between movie and series namespaces.
                            var itemType = string.IsNullOrWhiteSpace(item.Type) ? catalog.Type : item.Type;
                            var key = $"{itemType}:{item.Id}";
                            if (seenIds.Add(key))
                            {
                                item.Type ??= catalog.Type;
                                results.Add(item);
                            }
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        errors.Add($"{addon.Name}/{catalog.Name ?? catalog.Id}: {ex.Message}");
                    }
                }
            }

            if (results.Count == 0 && searchedCatalogs == 0)
                throw new InvalidOperationException(
                    "None of your installed catalog add-ons advertises search support in its manifest. Install an add-on with a catalog search extra.");
            if (results.Count == 0 && errors.Count > 0)
                throw new InvalidOperationException(
                    "Search-capable add-ons were found, but their requests failed. " + string.Join(" | ", errors.Take(3)));

            return results;
        }
    }
}
