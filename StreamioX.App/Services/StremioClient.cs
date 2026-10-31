// Services/StremioClient.cs
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace StreamioX.App.Services
{
    /// <summary>
    /// Handles low-level HTTP communication with Stremio add-on servers and manifest endpoints.
    /// </summary>
    public class StremioClient
    {
        private readonly HttpClient _httpClient;

        public StremioClient(HttpClient httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
        }

        /// <summary>
        /// Asynchronously fetches and deserializes JSON data from a specified Stremio add-on URL.
        /// </summary>
        public async Task<T> GetFromJsonAsync<T>(string url)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(jsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StremioClient] Error fetching {url}: {ex.Message}");
                return default;
            }
        }
    }
}