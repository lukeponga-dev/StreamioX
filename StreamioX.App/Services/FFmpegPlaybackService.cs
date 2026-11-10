using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using FFmpegInteropX;
using Windows.Foundation.Collections;
using Windows.Media.Playback;

namespace StreamioX.App.Services
{
    /// <summary>
    /// Validates direct network streams before opening them with FFmpegInteropX.
    /// The FFmpegMediaSource reference must remain alive for the playback session.
    /// </summary>
    public sealed class FFmpegPlaybackService
    {
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) StreamioX/1.0";
        private static readonly HttpClient Http = new HttpClient(
            new HttpClientHandler { AllowAutoRedirect = true })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        private FFmpegMediaSource? _mediaSource;

        public async Task<MediaPlaybackItem> CreateMediaPlaybackItemAsync(
            string streamUrl,
            Action<string>? reportStage = null,
            System.Collections.Generic.IReadOnlyDictionary<string, string>? requestHeaders = null)
        {
            if (string.IsNullOrWhiteSpace(streamUrl))
                throw new ArgumentException("A stream URL is required.", nameof(streamUrl));

            if (!Uri.TryCreate(streamUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException("FFmpeg requires an absolute HTTP or HTTPS stream URL.", nameof(streamUrl));

            Stop();

            // Probe with a one-byte range request. This catches expired links,
            // login pages, blocked hosts and broken redirects before FFmpeg opens them.
            reportStage?.Invoke("Checking direct stream HTTP response");
            var resolvedUri = await ResolveAndValidateStreamAsync(uri, requestHeaders).ConfigureAwait(true);
            System.Diagnostics.Debug.WriteLine($"[Playback] Probe completed. Original host: {uri.Host}; final host: {resolvedUri.Host}. FFmpeg will receive the ORIGINAL URL.");

            reportStage?.Invoke("Configuring FFmpeg network input");
            // FFmpegInteropX.UWP 2.1 ships with native D3D11 hardware decoding
            // enabled for supported codecs on Xbox. This package version does not
            // expose a UseDeviceHardwareAcceleration switch; adding one would not compile.
            var config = new MediaSourceConfig
            {
                FFmpegOptions = new PropertySet
                {
                    { "reconnect", 1 },
                    { "reconnect_streamed", 1 },
                    { "reconnect_on_network_error", 1 },
                    { "rw_timeout", "15000000" },
                    { "user_agent", UserAgent },
                    { "headers", BuildFfmpegHeaders(requestHeaders) }
                }
            };
            config.Audio.DownmixAudioStreamsToStereo = true;
            config.General.ReadAheadBufferEnabled = true;
            config.General.ReadAheadBufferDuration = TimeSpan.FromSeconds(15);
            config.General.ReadAheadBufferSize = 32 * 1024 * 1024;

            reportStage?.Invoke("Opening original stream URL with FFmpegInteropX");
            System.Diagnostics.Debug.WriteLine("[Playback] Creating FFmpegMediaSource from original URI.");
            System.Diagnostics.Debug.WriteLine($"[Playback] FFmpeg input scheme: {uri.Scheme}; host: {uri.Host}; port: {uri.Port}; path length: {uri.AbsolutePath.Length}.");
            System.Diagnostics.Debug.WriteLine($"[Playback] Forwarding {requestHeaders?.Count ?? 0} request header(s) to FFmpeg; values are intentionally not logged.");
            try
            {
                // The probe is diagnostic only. Do not substitute its redirect target:
                // signed URLs and session-bound providers may require the original URL.
                _mediaSource = await FFmpegMediaSource.CreateFromUriAsync(
                    uri.AbsoluteUri, config);
                System.Diagnostics.Debug.WriteLine(_mediaSource != null
                    ? "[Playback] FFmpegMediaSource created successfully."
                    : "[Playback] FFmpegMediaSource returned null.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"FFmpegInteropX could not open the validated HTTP stream. " +
                    $"HTTP endpoint: {resolvedUri.Host}; HRESULT: 0x{ex.HResult:X8}. " +
                    "The server may require authentication headers/cookies, or the returned media format may not be supported.",
                    ex);
            }

            if (_mediaSource == null)
                throw new InvalidOperationException("FFmpegInteropX returned no media source after opening the HTTP stream.");

            reportStage?.Invoke("Creating MediaPlaybackItem");
            var playbackItem = _mediaSource.CreateMediaPlaybackItem();
            System.Diagnostics.Debug.WriteLine(playbackItem != null
                ? "[Playback] MediaPlaybackItem created successfully."
                : "[Playback] MediaPlaybackItem returned null.");

            if (playbackItem == null)
                throw new InvalidOperationException("FFmpegInteropX returned no MediaPlaybackItem.");

            reportStage?.Invoke("MediaPlaybackItem created");
            return playbackItem;
        }

        private static async Task<Uri> ResolveAndValidateStreamAsync(
            Uri uri,
            System.Collections.Generic.IReadOnlyDictionary<string, string>? requestHeaders)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Range = new RangeHeaderValue(0, 0);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Accept.ParseAdd("*/*");
            ApplyRequestHeaders(request, requestHeaders);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var response = await Http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token).ConfigureAwait(true);

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "unknown";
            var finalUri = response.RequestMessage?.RequestUri ?? uri;

            System.Diagnostics.Debug.WriteLine($"[Playback] HTTP probe: {(int)response.StatusCode} {response.ReasonPhrase ?? string.Empty}");
            System.Diagnostics.Debug.WriteLine($"[Playback] Original URI host: {uri.Host}; final URI host: {finalUri.Host}; redirected: {!Uri.Compare(uri, finalUri, UriComponents.AbsoluteUri, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase).Equals(0)}");
            System.Diagnostics.Debug.WriteLine($"[Playback] Content-Type: {contentType}; Content-Length: {response.Content.Headers.ContentLength?.ToString() ?? "unknown"}");
            foreach (var header in response.Headers)
            {
                if (header.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase))
                    continue;
                System.Diagnostics.Debug.WriteLine($"[Playback] Response header: {header.Key}");
            }
            foreach (var header in response.Content.Headers)
            {
                System.Diagnostics.Debug.WriteLine($"[Playback] Content header: {header.Key}");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Direct stream returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}). " +
                    $"Content-Type: {contentType}; final host: {finalUri.Host}. " +
                    "The URL may be expired, blocked, or require request headers.");
            }

            if (contentType.Equals("text/html", StringComparison.OrdinalIgnoreCase) ||
                contentType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The stream URL returned an HTML page instead of media (HTTP {(int)response.StatusCode}, " +
                    $"host {finalUri.Host}). This usually means a login, anti-bot page, expired link, or redirect.");
            }

            return finalUri;
        }

        private static void ApplyRequestHeaders(HttpRequestMessage request,
            System.Collections.Generic.IReadOnlyDictionary<string, string>? headers)
        {
            if (headers == null) return;
            foreach (var header in headers)
            {
                if (string.IsNullOrWhiteSpace(header.Key) || string.IsNullOrWhiteSpace(header.Value)) continue;
                if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals("Range", StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase)) continue;
                if (header.Key.IndexOfAny(new[] { '\r', '\n', ':' }) >= 0 ||
                    header.Value.IndexOfAny(new[] { '\r', '\n' }) >= 0) continue;
                request.Headers.Remove(header.Key);
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        private static string BuildFfmpegHeaders(
            System.Collections.Generic.IReadOnlyDictionary<string, string>? headers)
        {
            if (headers == null || headers.Count == 0) return string.Empty;
            var builder = new System.Text.StringBuilder();
            foreach (var header in headers)
            {
                if (string.IsNullOrWhiteSpace(header.Key) || string.IsNullOrWhiteSpace(header.Value)) continue;
                if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals("Range", StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase)) continue;
                if (header.Key.IndexOfAny(new[] { '\r', '\n', ':' }) >= 0 ||
                    header.Value.IndexOfAny(new[] { '\r', '\n' }) >= 0) continue;
                builder.Append(header.Key).Append(": ").Append(header.Value).Append("\r\n");
            }
            return builder.ToString();
        }

        public void Stop()
        {
            // PlayerPage must clear MediaPlayer.Source before releasing this reference.
            _mediaSource = null;
        }
    }
}
