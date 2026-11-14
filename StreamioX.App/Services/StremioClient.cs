// Purpose: Implements StremioClient application behavior and coordinates the related Stremio, playback, or platform APIs.

using StreamioX.App.Models;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
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

        public async Task<T?> GetFromJsonAsync<T>(
            string url,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url);

            var typeInfo = StremioJsonContext.Default.GetTypeInfo(typeof(T))
                as JsonTypeInfo<T>
                ?? throw new NotSupportedException(
                    $"No source-generated JSON metadata registered for {typeof(T).FullName}.");

            var endpoint = GetSafeEndpoint(url);

            for (var attempt = 0; attempt < 2; attempt++)
            {
                HttpResponseMessage response;

                try
                {
                    response = await _httpClient.GetAsync(
                        url,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException) when (attempt == 0)
                {
                    await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                using (response)
                {
                    if (attempt == 0 && IsTransient(response.StatusCode))
                    {
                        var retryAfter = response.Headers.RetryAfter?.Delta
                            ?? (response.Headers.RetryAfter?.Date is DateTimeOffset retryDate
                                ? retryDate - DateTimeOffset.UtcNow
                                : TimeSpan.FromMilliseconds(300));

                        if (retryAfter >= TimeSpan.Zero &&
                            retryAfter <= TimeSpan.FromSeconds(1))
                        {
                            await Task.Delay(retryAfter, cancellationToken)
                                .ConfigureAwait(false);
                            continue;
                        }
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException(
                            $"Add-on request to {endpoint} returned " +
                            $"{(int)response.StatusCode} ({response.StatusCode}).");
                    }

                    var contentType = response.Content.Headers.ContentType?.MediaType
                        ?? "unknown";

                    if (contentType.Equals("text/html", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            $"Expected JSON from add-on endpoint {endpoint}, " +
                            $"but received HTML (Content-Type: {contentType}).");
                    }

                    await using var stream = await response.Content
                        .ReadAsStreamAsync(cancellationToken)
                        .ConfigureAwait(false);

                    try
                    {
                        return await JsonSerializer.DeserializeAsync(
                            stream,
                            typeInfo,
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (JsonException ex)
                    {
                        throw new InvalidDataException(
                            $"Invalid JSON from add-on endpoint {endpoint}. " +
                            $"Content-Type: {contentType}. " +
                            $"JSON error: {ex.Message}",
                            ex);
                    }
                }
            }

            throw new HttpRequestException(
                $"The add-on request to {endpoint} failed after one transient retry.");
        }

        private static string GetSafeEndpoint(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return "[invalid URL]";

            return $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}";
        }


        private static bool IsTransient(HttpStatusCode statusCode) =>
            statusCode == HttpStatusCode.RequestTimeout ||
            statusCode == HttpStatusCode.InternalServerError ||
            statusCode == HttpStatusCode.BadGateway ||
            statusCode == HttpStatusCode.ServiceUnavailable ||
            statusCode == HttpStatusCode.GatewayTimeout;
    }
}
