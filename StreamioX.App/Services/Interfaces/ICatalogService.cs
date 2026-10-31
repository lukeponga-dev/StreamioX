// Services/Interfaces/ICatalogService.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using StreamioX.App.Models;

namespace StreamioX.App.Services
{
    /// <summary>
    /// Contract for fetching media catalogs and metadata items from Stremio add-ons.
    /// </summary>
    public interface ICatalogService
    {
        /// <summary>
        /// Fetches a list of media items from a specified Stremio add-on catalog[cite: 1].
        /// </summary>
        /// <param name="addonBaseUrl">Base URL of the Stremio add-on (e.g., https://cinemeta-stremio.strem.fun)[cite: 1].</param>
        /// <param name="type">Media type filter (e.g., "movie" or "series")[cite: 1].</param>
        /// <param name="catalogId">Catalog identifier (e.g., "top" or "popular")[cite: 1].</param>
        Task<IEnumerable<Movie>> GetCatalogAsync(string addonBaseUrl, string type, string catalogId);
    }
}