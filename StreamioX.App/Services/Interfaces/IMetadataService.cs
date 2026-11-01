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
