using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;
using Windows.Storage;

namespace StreamioX.App.Services
{
    public sealed partial class TorrentBridgeService : ITorrentBridgeService
    {
        private const string NativeLibrary = "streamio_torrent_bridge";
        private readonly SemaphoreSlim _startGate = new(1, 1);
        private ushort? _port;

        public bool IsRunning => _port.HasValue;

        public async Task<string> GetStreamUrlAsync(
            StreamSource stream,
            CancellationToken cancellationToken = default)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var infoHash = NormalizeInfoHash(stream.InfoHash);
            var trackers = stream.Sources?
                .Where(source => !string.IsNullOrWhiteSpace(source))
                .Select(source => source.Trim())
                .ToList() ?? new List<string>();

            if (TryReadMagnet(stream.Url, out var magnetHash, out var magnetTrackers))
            {
                infoHash ??= magnetHash;
                trackers.AddRange(magnetTrackers);
            }

            if (infoHash == null)
                throw new InvalidOperationException("This torrent source does not contain a valid BitTorrent info hash.");

            var fileIndex = stream.FileIdx ?? -1;
            if (fileIndex < -1)
                throw new InvalidOperationException("The torrent source contains an invalid file index.");

            await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);

            var builder = new UriBuilder(
                Uri.UriSchemeHttp,
                "127.0.0.1",
                _port!.Value,
                $"/stream/{infoHash}/{fileIndex}");

            if (trackers.Count > 0)
            {
                builder.Query = string.Join(
                    "&",
                    trackers.Select(tracker => "tr=" + Uri.EscapeDataString(tracker)));
            }

            return builder.Uri.AbsoluteUri;
        }

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
                    "TorrentBridge",
                    CreationCollisionOption.OpenIfExists);
                var cacheFolder = await appData.LocalCacheFolder.CreateFolderAsync(
                    "TorrentCache",
                    CreationCollisionOption.OpenIfExists);

                var result = await Task.Run(
                    () => StartNative(configFolder.Path, cacheFolder.Path),
                    cancellationToken).ConfigureAwait(false);

                if (result.Status != 0)
                    throw new InvalidOperationException(ReadNativeError());

                if (result.Port == 0)
                    throw new InvalidOperationException("Torrent engine started without returning a listening port.");

                _port = result.Port;
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

        public async Task StopAsync()
        {
            await _startGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_port.HasValue)
                    return;

                var result = await Task.Run(NativeMethods.Stop).ConfigureAwait(false);
                _port = null;

                if (result != 0)
                    throw new InvalidOperationException(ReadNativeError());
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
                var status = NativeMethods.Start(configPointer, cachePointer, 0, &port);
                return (status, port);
            }
        }

        private static unsafe string ReadNativeError()
        {
            var buffer = new byte[2048];
            fixed (byte* pointer = buffer)
            {
                NativeMethods.LastError(pointer, (nuint)buffer.Length);
            }

            var length = Array.IndexOf(buffer, (byte)0);
            if (length < 0)
                length = buffer.Length;

            return Encoding.UTF8.GetString(buffer, 0, length);
        }

        private static unsafe partial class NativeMethods
        {
            [LibraryImport(NativeLibrary, EntryPoint = "streamio_torrent_bridge_start")]
            internal static partial int Start(byte* configPath, byte* cachePath, ushort requestedPort, ushort* actualPort);

            [LibraryImport(NativeLibrary, EntryPoint = "streamio_torrent_bridge_stop")]
            internal static partial int Stop();

            [LibraryImport(NativeLibrary, EntryPoint = "streamio_torrent_bridge_last_error")]
            internal static partial nuint LastError(byte* buffer, nuint capacity);
        }
    }
}
