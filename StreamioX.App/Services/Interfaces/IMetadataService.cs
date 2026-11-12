// Purpose: Declares the IMetadataService service contract used to keep features independent of concrete implementations. Implements IMetadataService application behavior and coordinates the related Stremio, playback, or platform APIs.

using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;

namespace StreamioX.App.Services
{
    public interface IMetadataService
    {
        Task<Movie?> GetMetaAsync(string type, string id, CancellationToken cancellationToken = default);
    }
}
