using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;

namespace StreamioX.App.Services
{
    /// <summary>
    /// Concrete implementation of ICatalogService that queries Stremio add-on endpoints[cite: 1, 2].
    /// </summary>
    public class CatalogService : ICatalogService
    {
        private readonly StremioClient _stremioClient;
        private readonly IAddonService _addonService;

        public CatalogService(StremioClient stremioClient, IAddonService addonService)
        {
            _stremioClient = stremioClient ?? throw new ArgumentNullException(nameof(stremioClient));
        }

        public async Task<IEnumerable<Movie>> GetCatalogAsync(string addonBaseUrl, string type, string catalogId)
        {
            if (string.IsNullOrWhiteSpace(addonBaseUrl))
                return new Movie[0];

            // Standard Stremio Addon Catalog URL: {addonBaseUrl}/catalog/{type}/{catalogId}.json[cite: 1, 2]
            string url = $"{addonBaseUrl.TrimEnd('/')}/catalog/{type}/{catalogId}.json";

            var response = await _stremioClient.GetFromJsonAsync<CatalogResponse>(url);
            return response?.Metas ?? new Movie[0];
        }
    }
}
