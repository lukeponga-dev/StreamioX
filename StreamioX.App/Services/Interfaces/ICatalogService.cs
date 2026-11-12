// Purpose: Declares the ICatalogService service contract used to keep features independent of concrete implementations. Implements ICatalogService application behavior and coordinates the related Stremio, playback, or platform APIs.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;

namespace StreamioX.App.Services
{
    public interface ICatalogService
    {
        Task<IReadOnlyList<Movie>> GetCatalogAsync(
            string addonBaseUrl,
            string type,
            string catalogId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Movie>> GetFromInstalledAddonsAsync(
            string type = "movie",
            string? preferredCatalogId = null,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Movie>> SearchInstalledAddonsAsync(
            string query,
            CancellationToken cancellationToken = default);
    }
}
