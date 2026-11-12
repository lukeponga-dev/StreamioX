// Purpose: Implements TorrentBridgeService application behavior and coordinates the related Stremio, playback, or platform APIs.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;
using Windows.Storage;

namespace StreamioX.App.Services
{
    public sealed partial class TorrentBridgeService : ITorrentBridgeService
    {
        private const ushort RequestedPort = 11470;
        private readonly SemaphoreSlim _startGate = new(1, 1);
        private ushort? _port;

        public bool IsRunning => _port.HasValue;
        public string? ServerAddress => _port.HasValue ? $"http://127.0.0.1:{_port.Value}/" : null;

        public async Task<string> StartServerAsync(CancellationToken cancellationToken = default)
        {
            await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
            return ServerAddress!;
        }

        /// <summary>Converts a torrent-backed source into a local HTTP URL served by the native bridge.</summary>
        public async Task<string> GetStreamUrlAsync(
            StreamSource stream,
            CancellationToken cancellationToken = default)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var infoHash = NormalizeInfoHash(stream.InfoHash);
            var sourceTrackers = stream.Sources?
                .Where(source => !string.IsNullOrWhiteSpace(source))
                .Select(source => source.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
            var magnetTrackers = new List<string>();
            string? originalMagnet = null;

            if (TryReadMagnet(stream.Url, out var magnetHash, out magnetTrackers))
            {
                infoHash ??= magnetHash;
                // Preserve xs= (exact torrent source), ws= (web seed), dn=,
                // and every other provider-supplied magnet parameter.
                originalMagnet = stream.Url;
            }

            if (infoHash == null)
                throw new InvalidOperationException("This torrent source does not contain a valid BitTorrent info hash.");

            // Stremio uses fileIdx=-1 when an add-on leaves file selection
            // to the player. The native server resolves -1 to the best video
            // file; defaulting to 0 can select a subtitle/sample/NFO file.
            var fileIndex = stream.FileIdx ?? -1;
            if (fileIndex < -1)
                throw new InvalidOperationException("The torrent source contains an invalid file index.");

            await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);

            var builder = new UriBuilder(
                Uri.UriSchemeHttp,
                "127.0.0.1",
                _port!.Value,
                $"/stream/{infoHash}/{fileIndex}");

            // The native route adds a torrent on first access from its
            // "magnet" query parameter. Preserve the original magnet URI so
            // exact torrent sources (xs=) and web seeds (ws=) survive too.
            var magnet = new StringBuilder(
                originalMagnet ?? $"magnet:?xt=urn:btih:{infoHash}");
            var includedTrackers = new HashSet<string>(
                magnetTrackers, StringComparer.OrdinalIgnoreCase);
            foreach (var tracker in sourceTrackers)
            {
                if (includedTrackers.Add(tracker))
                    magnet.Append("&tr=").Append(Uri.EscapeDataString(tracker));
            }

            var trackerCount = includedTrackers.Count;
            builder.Query = "magnet=" + Uri.EscapeDataString(magnet.ToString());
            var resolvedUrl = builder.Uri.AbsoluteUri;
            var cacheKey = "torrent-url:" + _port.Value + ":" + infoHash + ":" +
                fileIndex + ":" + magnet.ToString();
            PlaybackDiagnostics.Write(
                $"[Torrent] Prepared local stream route for hash {infoHash[..8]}…; file index {fileIndex}; trackers {trackerCount}; preserved original magnet: {originalMagnet != null}.");
            return await MemoryTtlCache.GetOrCreateAsync(
                cacheKey,
                TimeSpan.FromSeconds(30),
                () => Task.FromResult(resolvedUrl)).ConfigureAwait(false);
        }

        /// <summary>Normalizes supported torrent info-hash representations to a 40-character hexadecimal value.</summary>
        private static string? NormalizeInfoHash(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var hash = value.Trim();
            if (hash.Length == 40 && hash.All(Uri.IsHexDigit))
                return hash.ToLowerInvariant();

            if (hash.Length == 32)
                return Base32ToHex(hash);

            return null;
        }

        private static string? Base32ToHex(string value)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var output = new StringBuilder(40);
            var buffer = 0;
            var bits = 0;

            foreach (var character in value.ToUpperInvariant())
            {
                var digit = alphabet.IndexOf(character);
                if (digit < 0)
                    return null;

                buffer = (buffer << 5) | digit;
                bits += 5;

                if (bits >= 8)
                {
                    bits -= 8;
                    var nextByte = (buffer >> bits) & 0xff;
                    output.Append(nextByte.ToString("x2"));
                    buffer &= (1 << bits) - 1;
                }
            }

            return output.Length == 40 ? output.ToString() : null;
        }

        /// <summary>Extracts and validates the torrent hash and tracker parameters from a magnet URI.</summary>
        private static bool TryReadMagnet(
            string? value,
            out string? infoHash,
            out List<string> trackers)
        {
            infoHash = null;
            trackers = new List<string>();

            if (!Uri.TryCreate(value, UriKind.Absolute, out var magnetUri)
                || !string.Equals(magnetUri.Scheme, "magnet", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            foreach (var part in magnetUri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = part.IndexOf('=');
                var key = Uri.UnescapeDataString(separator >= 0 ? part[..separator] : part);
                var itemValue = separator >= 0
                    ? Uri.UnescapeDataString(part[(separator + 1)..])
                    : string.Empty;

                if (string.Equals(key, "xt", StringComparison.OrdinalIgnoreCase)
                    && itemValue.StartsWith("urn:btih:", StringComparison.OrdinalIgnoreCase))
                {
                    infoHash = NormalizeInfoHash(itemValue["urn:btih:".Length..]);
                }
                else if (string.Equals(key, "tr", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(itemValue))
                {
                    trackers.Add(itemValue);
                }
            }

            return infoHash != null;
        }

        /// <summary>Starts the native bridge once and waits until its local HTTP endpoint is ready.</summary>
        private async Task EnsureStartedAsync(CancellationToken cancellationToken)
        {
            if (_port.HasValue)
                return;

            await _startGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_port.HasValue)
                    return;

                var appData = ApplicationData.Current;
                var configFolder = await appData.LocalFolder.CreateFolderAsync(
                    "rqbit_dht",
                    CreationCollisionOption.OpenIfExists);
                var cacheFolder = await appData.LocalCacheFolder.CreateFolderAsync(
                    "TorrentCache",
                    CreationCollisionOption.OpenIfExists);

                var result = await Task.Run(
                    () => StartNative(configFolder.Path, cacheFolder.Path),
                    cancellationToken).ConfigureAwait(false);

                if (result.Status != 0)
                    throw new InvalidOperationException(NativeBridge.ReadLastError());

                if (result.Port == 0)
                    throw new InvalidOperationException("Torrent engine started without returning a listening port.");

                _port = result.Port;
                PlaybackDiagnostics.Write(
                    $"[Torrent] Native engine started successfully on 127.0.0.1:{_port.Value}.");
            }
            catch (DllNotFoundException ex)
            {
                throw new InvalidOperationException(
                    "The Xbox torrent engine library is missing from this package. Build the native UWP bridge before deploying StreamioX.",
                    ex);
            }
            catch (EntryPointNotFoundException ex)
            {
                throw new InvalidOperationException(
                    "The packaged torrent engine does not expose the expected native API.",
                    ex);
            }
            finally
            {
                _startGate.Release();
            }
        }

        /// <summary>Stops the native torrent server and releases its managed bridge state.</summary>
        public async Task StopAsync()
        {
            await _startGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_port.HasValue)
                    return;

                var result = await Task.Run(NativeBridge.Stop).ConfigureAwait(false);
                _port = null;
                MemoryTtlCache.RemoveByPrefix("torrent-url:");

                if (result != 0)
                    throw new InvalidOperationException(NativeBridge.ReadLastError());
            }
            finally
            {
                _startGate.Release();
            }
        }

        private static unsafe (int Status, ushort Port) StartNative(string configPath, string cachePath)
        {
            var configBytes = Encoding.UTF8.GetBytes(configPath + "\0");
            var cacheBytes = Encoding.UTF8.GetBytes(cachePath + "\0");
            ushort port = 0;

            fixed (byte* configPointer = configBytes)
            fixed (byte* cachePointer = cacheBytes)
            {
                var status = NativeBridge.Start(configPointer, cachePointer, RequestedPort, &port);
                return (status, port);
            }
        }


    }
}
