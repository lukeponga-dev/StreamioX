using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StreamioX.App.Models;

namespace StreamioX.App.Services
{
    public class CatalogService : ICatalogService
    {
        private readonly StremioClient _stremioClient;

        public CatalogService(StremioClient stremioClient)
        {
            _stremioClient = stremioClient
                             ?? throw new ArgumentNullException(nameof(stremioClient));
        }

        public async Task<IEnumerable<Movie>> GetCatalogAsync(
            string addonBaseUrl,
            string type,
            string catalogId)
        {
            if (string.IsNullOrWhiteSpace(addonBaseUrl))
                throw new ArgumentException("Addon URL is required.");

            if (type != "movie" && type != "series")
                throw new ArgumentException("Unsupported media type.");

            if (string.IsNullOrWhiteSpace(catalogId))
                throw new ArgumentException("Catalog ID is required.");

            string url =
                $"{addonBaseUrl.TrimEnd('/')}/catalog/{type}/{catalogId}.json";

            var response =
                await _stremioClient.GetFromJsonAsync<CatalogResponse>(url);

            return response?.Metas ?? Array.Empty<Movie>();
        }
    }
}