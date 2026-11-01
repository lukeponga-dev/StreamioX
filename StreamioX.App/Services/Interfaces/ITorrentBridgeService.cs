using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;

namespace StreamioX.App.Services
{
    public interface ITorrentBridgeService
    {
        bool IsRunning { get; }

        Task<string> GetStreamUrlAsync(
            StreamSource stream,
            CancellationToken cancellationToken = default);

        Task StopAsync();
    }
}
