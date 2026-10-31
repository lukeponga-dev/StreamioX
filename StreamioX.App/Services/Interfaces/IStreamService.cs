using System.Collections.Generic;
using System.Threading.Tasks;
using StreamioX.App.Models;

namespace StreamioX.App.Services
{
    /// <summary>
    /// Contract for fetching playback stream links from Stremio add-ons[cite: 1, 2].
    /// </summary>
    public interface IStreamService
    {
        Task<IEnumerable<StreamSource>> GetStreamsAsync(string addonBaseUrl, string type, string id);
    }
}