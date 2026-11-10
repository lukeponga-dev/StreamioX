namespace StreamioX.App.Services
{
    using FFmpegInteropX;
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using Windows.Foundation.Collections;
    using Windows.Media.Playback;

    /// <summary>
    /// Validates direct network streams before opening them with FFmpegInteropX.
    /// The FFmpegMediaSource reference must remain alive for the playback session
    /// </summary>
    public sealed class FFmpegPlaybackService
    {
        /// <summary>
        /// Defines the UserAgent
        /// </summary>
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) StreamioX/1.0";

        /// <summary>
        /// Defines the Http
        /// </summary>
        private static readonly HttpClient Http = new HttpClient(
            new HttpClientHandler { AllowAutoRedirect = true })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        // The torrent bridge is an in-process loopback server. Do not route
        // requests to it through a system proxy, and do not follow redirects
        // away from the local adapter.
        private static readonly HttpClient LocalHttp = new HttpClient(
            new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        /// <summary>
        /// Defines the _mediaSource
        /// </summary>
        private FFmpegMediaSource? _mediaSource;

        /// <summary>
        /// The CreateMediaPlaybackItemAsync
        /// </summary>
        /// <param name="streamUrl">The streamUrl<see cref="string"/></param>
        /// <param name="reportStage">The reportStage<see cref="Action{string}?"/></param>
        /// <param name="requestHeaders">The requestHeaders<see cref="System.Collections.Generic.IReadOnlyDictionary{string, string}?"/></param>
        /// <returns>The <see cref="Task{MediaPlaybackItem}"/></returns>
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
            PlaybackDiagnostics.Write($"[Playback] Probe completed. Original host: {uri.Host}; final host: {resolvedUri.Host}. FFmpeg will receive the ORIGINAL URL.");

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
                    { "user_agent", GetFfmpegUserAgent(requestHeaders) },
                    { "headers", BuildFfmpegHeaders(requestHeaders) }
                }
            };
            config.Audio.DownmixAudioStreamsToStereo = true;
            config.General.ReadAheadBufferEnabled = true;
            config.General.ReadAheadBufferDuration = TimeSpan.FromSeconds(15);
            config.General.ReadAheadBufferSize = 32 * 1024 * 1024;

            reportStage?.Invoke("Opening original stream URL with FFmpegInteropX");
            PlaybackDiagnostics.Write("[Playback] Creating FFmpegMediaSource from original URI.");
            PlaybackDiagnostics.Write($"[Playback] FFmpeg input scheme: {uri.Scheme}; host: {uri.Host}; port: {uri.Port}; path length: {uri.AbsolutePath.Length}.");
            var forwardedHeaderNames = requestHeaders == null
                ? string.Empty
                : string.Join(", ", requestHeaders.Keys);
            PlaybackDiagnostics.Write(
                $"[Playback] Forwarding {requestHeaders?.Count ?? 0} request header(s) to FFmpeg" +
                (string.IsNullOrEmpty(forwardedHeaderNames) ? "." : $": {forwardedHeaderNames}.") +
                " Header values are intentionally not logged.");
            try
            {
                // The probe is diagnostic only. Do not substitute its redirect target:
                // signed URLs and session-bound providers may require the original URL.
                _mediaSource = await FFmpegMediaSource.CreateFromUriAsync(
                    uri.AbsoluteUri, config);
                PlaybackDiagnostics.Write(_mediaSource != null
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
            PlaybackDiagnostics.Write(playbackItem != null
                ? "[Playback] MediaPlaybackItem created successfully."
                : "[Playback] MediaPlaybackItem returned null.");

            if (playbackItem == null)
                throw new InvalidOperationException("FFmpegInteropX returned no MediaPlaybackItem.");

            reportStage?.Invoke("MediaPlaybackItem created");
            return playbackItem;
        }

        /// <summary>
        /// The ResolveAndValidateStreamAsync
        /// </summary>
        /// <param name="uri">The uri<see cref="Uri"/></param>
        /// <param name="requestHeaders">The requestHeaders<see cref="System.Collections.Generic.IReadOnlyDictionary{string, string}?"/></param>
        /// <returns>The <see cref="Task{Uri}"/></returns>
        private static async Task<Uri> ResolveAndValidateStreamAsync(
            Uri uri,
            System.Collections.Generic.IReadOnlyDictionary<string, string>? requestHeaders)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Range = new RangeHeaderValue(0, 0);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Accept.ParseAdd("*/*");
            ApplyRequestHeaders(request, requestHeaders);

            // A direct provider URL should fail quickly when expired or
            // blocked. A torrent loopback route can legitimately spend up to
            // 60 seconds resolving metadata and selecting the video file.
            var isLocalBridge = uri.IsLoopback;
            var probeTimeout = TimeSpan.FromSeconds(isLocalBridge ? 75 : 15);
            PlaybackDiagnostics.Write(
                $"[Playback] HTTP probe timeout: {probeTimeout.TotalSeconds:0}s; local bridge: {isLocalBridge}.");
            using var timeout = new CancellationTokenSource(probeTimeout);
            var client = isLocalBridge ? LocalHttp : Http;
            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token).ConfigureAwait(true);
            }
            catch (Exception ex) when (!isLocalBridge &&
                (ex is HttpRequestException || ex is TaskCanceledException))
            {
                // Provider CDNs frequently reject diagnostic range requests,
                // rate-limit secondary clients, or behave differently from FFmpeg.
                // The probe must not prevent FFmpeg from attempting the real stream.
                PlaybackDiagnostics.Write(
                    $"[Playback] External HTTP probe unavailable ({ex.GetType().Name}, HRESULT 0x{ex.HResult:X8}); continuing with the original URL in FFmpeg.");
                return uri;
            }

            using (response)
            {
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "unknown";
            var finalUri = response.RequestMessage?.RequestUri ?? uri;

            PlaybackDiagnostics.Write($"[Playback] HTTP probe: {(int)response.StatusCode} {response.ReasonPhrase ?? string.Empty}");
            PlaybackDiagnostics.Write($"[Playback] Original URI host: {uri.Host}; final URI host: {finalUri.Host}; redirected: {!Uri.Compare(uri, finalUri, UriComponents.AbsoluteUri, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase).Equals(0)}");
            PlaybackDiagnostics.Write($"[Playback] Content-Type: {contentType}; Content-Length: {response.Content.Headers.ContentLength?.ToString() ?? "unknown"}");
            foreach (var header in response.Headers)
            {
                if (header.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase))
                    continue;
                PlaybackDiagnostics.Write($"[Playback] Response header: {header.Key}");
            }
            foreach (var header in response.Content.Headers)
            {
                PlaybackDiagnostics.Write($"[Playback] Content header: {header.Key}");
            }

            if (!response.IsSuccessStatusCode)
            {
                if (isLocalBridge)
                {
                    var nativeError = StreamioX.App.NativeBridge.ReadLastError();
                    if (!string.IsNullOrWhiteSpace(nativeError))
                        PlaybackDiagnostics.Write($"[Torrent] Native bridge diagnostic: {nativeError}");

                    // A local 4xx/5xx means the bridge has not produced media.
                    // Unlike a provider probe, FFmpeg cannot recover this request.
                    throw new HttpRequestException(
                        $"Local torrent bridge returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}). " +
                        $"Content-Type: {contentType}; host: {finalUri.Host}.");
                }

                PlaybackDiagnostics.Write(
                    $"[Playback] External probe returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}); allowing FFmpeg to attempt the original URL.");
                return uri;
            }

            if (contentType.Equals("text/html", StringComparison.OrdinalIgnoreCase) ||
                contentType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
            {
                if (isLocalBridge)
                    throw new InvalidOperationException("The local torrent bridge returned HTML instead of media.");

                PlaybackDiagnostics.Write(
                    $"[Playback] External probe returned HTML (HTTP {(int)response.StatusCode}, host {finalUri.Host}); allowing FFmpeg to attempt the original URL with provider headers.");
                return uri;
            }

            return finalUri;
            }
        }

        /// <summary>
        /// The ApplyRequestHeaders
        /// </summary>
        /// <param name="request">The request<see cref="HttpRequestMessage"/></param>
        /// <param name="headers">The headers<see cref="System.Collections.Generic.IReadOnlyDictionary{string, string}?"/></param>
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
                    header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)) continue;
                if (header.Key.IndexOfAny(new[] { '\r', '\n', ':' }) >= 0 ||
                    header.Value.IndexOfAny(new[] { '\r', '\n' }) >= 0) continue;
                request.Headers.Remove(header.Key);
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        /// <summary>
        /// The BuildFfmpegHeaders
        /// </summary>
        /// <param name="headers">The headers<see cref="System.Collections.Generic.IReadOnlyDictionary{string, string}?"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string GetFfmpegUserAgent(
            System.Collections.Generic.IReadOnlyDictionary<string, string>? headers)
        {
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    if (header.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(header.Value) &&
                        header.Value.IndexOfAny(new[] { '\r', '\n' }) < 0)
                        return header.Value;
                }
            }

            return UserAgent;
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
                    header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
                    header.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)) continue;
                if (header.Key.IndexOfAny(new[] { '\r', '\n', ':' }) >= 0 ||
                    header.Value.IndexOfAny(new[] { '\r', '\n' }) >= 0) continue;
                builder.Append(header.Key).Append(": ").Append(header.Value).Append("\r\n");
            }
            return builder.ToString();
        }

        /// <summary>
        /// The Stop
        /// </summary>
        public void Stop()
        {
            // PlayerPage must clear MediaPlayer.Source before releasing this reference.
            _mediaSource = null;
        }
    }
}
