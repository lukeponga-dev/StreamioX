using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;

namespace StreamioX.App.Services
{
    public interface IStreamService
    {
        Task<IReadOnlyList<StreamSource>> GetStreamsAsync(
            string addonBaseUrl,
            string type,
            string id,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Aggregate streams from all installed stream-providing addons.
        /// </summary>
        Task<IReadOnlyList<StreamSource>> GetFromInstalledAddonsAsync(
            string type,
            string id,
            CancellationToken cancellationToken = default);
    }
}
