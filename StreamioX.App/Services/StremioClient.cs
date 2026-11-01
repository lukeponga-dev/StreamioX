using System;
using System.Net.Http;
using System.Threading.Tasks;
using StreamioX.App.Models;

namespace StreamioX.App.Services
{
    public class StremioClient
    {
        private readonly HttpClient _httpClient;

        public StremioClient(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "StreamioX/1.0");
        }

        public async Task<T?> GetFromJsonAsync<T>(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException(
                    "A request URL is required.", nameof(url));

            System.Diagnostics.Debug.WriteLine(
                $"[StremioClient] GET {url}");

            using var response = await _httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            System.Diagnostics.Debug.WriteLine(
                $"[StremioClient] Response length: {json.Length}");

            object? result;

            if (typeof(T) == typeof(CatalogResponse))
            {
                result = System.Text.Json.JsonSerializer.Deserialize(
                    json,
                    StremioJsonContext.Default.CatalogResponse);
            }
            else
            {
                throw new NotSupportedException(
                    $"JSON type {typeof(T).Name} is not registered.");
            }

            return (T?)result;
        }
    }
}