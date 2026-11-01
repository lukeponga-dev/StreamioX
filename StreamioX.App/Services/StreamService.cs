// Services/StreamService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StreamioX.App.Models;

namespace StreamioX.App.Services
{
    public class StreamService : IStreamService
    {
        private readonly StremioClient _stremioClient;

        public StreamService(StremioClient stremioClient)
        {
            _stremioClient = stremioClient ?? throw new ArgumentNullException(nameof(stremioClient));
        }

        public async Task<IEnumerable<StreamSource>> GetStreamsAsync(string addonBaseUrl, string type, string id)
        {
            if (string.IsNullOrWhiteSpace(addonBaseUrl) || string.IsNullOrWhiteSpace(id))
                return new StreamSource[0];

            string url = $"{addonBaseUrl.TrimEnd('/')}/stream/{type}/{id}.json";
            var response = await _stremioClient.GetFromJsonAsync<StreamResponse>(url);
            return response?.Streams ?? new StreamSource[0];
        }
    }
}