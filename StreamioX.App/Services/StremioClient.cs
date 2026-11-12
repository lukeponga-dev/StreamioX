// Purpose: Implements StremioClient application behavior and coordinates the related Stremio, playback, or platform APIs.

using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using StreamioX.App.Models;
using System.Threading;
using System.Threading.Tasks;

namespace StreamioX.App.Services
{
    /// <summary>
    /// Handles low-level HTTP communication with Stremio add-on servers.
    /// </summary>
    public sealed class StremioClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = StremioJsonContext.Default
        };

        private readonly HttpClient _httpClient;

        public StremioClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// Fetches and deserializes JSON from a Stremio add-on URL.
        /// </summary>
        public async Task<T?> GetFromJsonAsync<T>(string url, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url);

            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var typeInfo = StremioJsonContext.Default.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
                ?? throw new NotSupportedException($"No source-generated JSON metadata registered for {typeof(T).FullName}.");
            return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false);
        }
    }
}
