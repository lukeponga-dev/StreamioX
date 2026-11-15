// Purpose: Resolves full Stremio metadata concurrently, caches responses, and records provider diagnostics.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;

namespace StreamioX.App.Services
{
    public sealed class MetadataService : IMetadataService
    {
        private static readonly TimeSpan MetadataTtl = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(8);
        private static readonly ConcurrentDictionary<string, long> ProviderLatencyMs =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly StremioClient _client;
        private readonly IAddonService _addons;

        public MetadataService(StremioClient client, IAddonService addons)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _addons = addons ?? throw new ArgumentNullException(nameof(addons));
        }

        public async Task<Movie?> GetMetaAsync(
            string type,
            string id,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(id))
                return null;

            await _addons.InitializeAsync(cancellationToken).ConfigureAwait(false);
            var providers = _addons.GetMetaProviders(type, id)
                .OrderBy(addon => ProviderLatencyMs.TryGetValue(addon.Id, out var latency)
                    ? latency
                    : 4000)
                .ToArray();

            if (providers.Length == 0)
            {
                PlaybackDiagnostics.Write($"[Metadata] No metadata providers found for {type}/{id}.");
                return null;
            }

            // Start provider requests together, then return the first valid metadata
            // result rather than serially waiting for every slow or offline add-on.
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var providerGate = new SemaphoreSlim(Math.Min(4, providers.Length));
            var pending = providers
                .Select(addon => FetchFromProviderAsync(addon, type, id, linkedCts.Token, providerGate))
                .ToList();

            try
            {
                while (pending.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var completed = await Task.WhenAny(pending).ConfigureAwait(false);
                    pending.Remove(completed);

                    var metadata = await completed.ConfigureAwait(false);
                    if (metadata == null)
                        continue;

                    var synopsis = new[]
                    {
                        metadata.Description,
                        metadata.Overview,
                        metadata.Plot
                    }.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                    var descriptionLength = synopsis?.Length ?? 0;

                    if (descriptionLength == 0)
                    {
                        PlaybackDiagnostics.Write(
                            $"[Metadata] Warning: {metadata.Type}/{metadata.Id} returned without a synopsis.");
                    }
                    else
                    {
                        PlaybackDiagnostics.Write(
                            $"[Metadata] Loaded {metadata.Type}/{metadata.Id}; synopsis length: {descriptionLength} characters.");
                    }

                    return metadata;
                }

                cancellationToken.ThrowIfCancellationRequested();
                PlaybackDiagnostics.Write($"[Metadata] All metadata providers failed or returned no metadata for {type}/{id}.");
                return null;
            }
            finally
            {
                linkedCts.Cancel();
                // All provider tasks are drained before the semaphore is disposed.
                if (pending.Count > 0)
                    await Task.WhenAll(pending).ConfigureAwait(false);
            }
        }

        private async Task<Movie?> FetchFromProviderAsync(
            InstalledAddon addon,
            string type,
            string id,
            CancellationToken cancellationToken,
            SemaphoreSlim providerGate)
        {
            var acquired = false;
            try
            {
                await providerGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                acquired = true;

                var started = DateTimeOffset.UtcNow;
                var url = $"{addon.BaseUrl.TrimEnd('/')}/meta/{Uri.EscapeDataString(type)}/{Uri.EscapeDataString(id)}.json";
                var response = await MemoryTtlCache.GetOrCreateAsync(
                    "addon-meta:" + url,
                    MetadataTtl,
                    async sharedToken =>
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(sharedToken);
                        timeout.CancelAfter(ProviderTimeout);
                        return await _client.GetFromJsonAsync<MetaResponse>(url, timeout.Token)
                            .ConfigureAwait(false);
                    },
                    cancellationToken).ConfigureAwait(false);

                ProviderLatencyMs[addon.Id] = Math.Max(
                    1,
                    (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds);

                if (response?.Meta == null)
                {
                    PlaybackDiagnostics.Write($"[Metadata] {addon.Name} returned an empty meta response.");
                    return null;
                }

                PlaybackDiagnostics.Write(
                    $"[Metadata] {addon.Name} responded in {ProviderLatencyMs[addon.Id]} ms.");
                return response.Meta;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                PlaybackDiagnostics.Write($"[Metadata] {addon.Name} timed out after {ProviderTimeout.TotalSeconds:0}s.");
                return null;
            }
            catch (Exception ex)
            {
                PlaybackDiagnostics.Write($"[Metadata] {addon.Name} failed: {ex.Message}");
                return null;
            }
            finally
            {
                if (acquired)
                    providerGate.Release();
            }
        }
    }
}
