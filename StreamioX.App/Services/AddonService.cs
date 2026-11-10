using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;
using Windows.Storage;

namespace StreamioX.App.Services
{
    public sealed class AddonService : IAddonService
    {
        public const string OfficialCinemetaManifestUrl = "https://v3-cinemeta.strem.io/manifest.json";
        private const string AddonStorageFileName = "installed-addons.json";
        private readonly StremioClient _client;
        private readonly List<InstalledAddon> _addons = new();
        private readonly SemaphoreSlim _initializeLock = new(1, 1);
        private readonly SemaphoreSlim _mutationLock = new(1, 1);
        private bool _initialized;

        public IReadOnlyList<InstalledAddon> InstalledAddons => _addons.ToList();

        public AddonService(StremioClient client) =>
            _client = client ?? throw new ArgumentNullException(nameof(client));

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await _initializeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_initialized) return;

                _addons.Clear();
                _addons.AddRange(LoadFromStorage());

                if (!_addons.Any(a => a.IsOfficial ||
                    a.ManifestUrl.Contains("v3-cinemeta.strem.io", StringComparison.OrdinalIgnoreCase)))
                {
                    // First launch: make a best-effort install of Cinemeta. Network failures
                    // do not block the app from opening; the Add-ons page can retry.
                    try { await InstallFromManifestUrlAsync(OfficialCinemetaManifestUrl, cancellationToken).ConfigureAwait(false); }
                    catch (OperationCanceledException) { throw; }
                    catch { }
                }

                foreach (var addon in _addons)
                {
                    if (addon.ManifestUrl.Contains("v3-cinemeta.strem.io", StringComparison.OrdinalIgnoreCase))
                        addon.IsOfficial = true;
                }

                _initialized = true;
                SaveToStorage();
            }
            finally { _initializeLock.Release(); }

            // Refresh every installed manifest at startup. Each failure is isolated and
            // the last-known-good manifest remains usable offline.
            await SyncAllAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<InstalledAddon?> InstallFromManifestUrlAsync(
            string manifestUrl, CancellationToken cancellationToken = default)
        {
            var normalizedUrl = NormalizeManifestUrl(manifestUrl);
            var manifest = await _client.GetFromJsonAsync<AddonManifest>(normalizedUrl, cancellationToken)
                .ConfigureAwait(false);
            ValidateManifest(manifest);

            await _mutationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var existing = _addons.FirstOrDefault(a =>
                    string.Equals(a.Id, manifest!.Id, StringComparison.OrdinalIgnoreCase));
                var addon = existing ?? new InstalledAddon();
                ApplyManifest(addon, manifest!, normalizedUrl);
                addon.LastSyncedAt = DateTimeOffset.UtcNow;
                addon.LastSyncError = null;
                addon.SyncState = "Synced";
                if (existing == null) _addons.Add(addon);
                SaveToStorage();
                return addon;
            }
            finally { _mutationLock.Release(); }
        }

        public async Task<AddonSyncSummary> SyncAllAsync(CancellationToken cancellationToken = default)
        {
            var summary = new AddonSyncSummary();
            var snapshot = _addons.ToList();
            foreach (var addon in snapshot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var refreshed = await _client.GetFromJsonAsync<AddonManifest>(addon.ManifestUrl, cancellationToken)
                        .ConfigureAwait(false);
                    ValidateManifest(refreshed);
                    await _mutationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        // A manifest ID can change upstream; retain the installed identity
                        // while updating the descriptor only if it still matches.
                        if (!string.Equals(refreshed!.Id, addon.Id, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("The manifest ID changed. Remove and reinstall this add-on.");
                        ApplyManifest(addon, refreshed, addon.ManifestUrl);
                        addon.LastSyncedAt = DateTimeOffset.UtcNow;
                        addon.LastSyncError = null;
                        addon.SyncState = "Synced";
                        SaveToStorage();
                    }
                    finally { _mutationLock.Release(); }
                    summary.Succeeded++;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    await _mutationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        addon.SyncState = "Error";
                        addon.LastSyncError = ex.Message;
                        SaveToStorage();
                    }
                    finally { _mutationLock.Release(); }
                    summary.Failed++;
                    summary.Errors.Add(addon.Name + ": " + ex.Message);
                }
            }
            return summary;
        }

        public async Task UninstallAsync(string addonId)
        {
            await _mutationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var item = _addons.FirstOrDefault(a =>
                    string.Equals(a.Id, addonId, StringComparison.OrdinalIgnoreCase));
                if (item == null) return;
                if (item.IsOfficial)
                    throw new InvalidOperationException("The official Cinemeta add-on cannot be removed.");
                _addons.Remove(item);
                SaveToStorage();
            }
            finally { _mutationLock.Release(); }
        }

        public IReadOnlyList<InstalledAddon> GetCatalogProviders(string? type = null) =>
            _addons.ToList().Where(a => HasResource(a, "catalog"))
                .Where(a => SupportsType(a, type))
                .ToList();

        public IReadOnlyList<InstalledAddon> GetMetaProviders(string? type = null, string? contentId = null) =>
            _addons.ToList().Where(a => HasResource(a, "meta"))
                .Where(a => SupportsType(a, type))
                .Where(a => SupportsId(a, contentId))
                .ToList();

        public IReadOnlyList<InstalledAddon> GetStreamProviders(string? type = null, string? contentId = null) =>
            _addons.ToList().Where(a => HasResource(a, "stream"))
                .Where(a => SupportsType(a, type))
                .Where(a => SupportsId(a, contentId))
                .ToList();

        private static bool HasResource(InstalledAddon addon, string name) =>
            addon.Resources.Any(r => string.Equals(r, name, StringComparison.OrdinalIgnoreCase));

        private static bool SupportsType(InstalledAddon addon, string? type) =>
            type == null || (addon.Types.Count == 0 && addon.Catalogs.Count == 0) ||
            addon.Types.Any(t => string.Equals(t, type, StringComparison.OrdinalIgnoreCase)) ||
            addon.Catalogs.Any(c => string.Equals(c.Type, type, StringComparison.OrdinalIgnoreCase));

        private static bool SupportsId(InstalledAddon addon, string? contentId) =>
            contentId == null || addon.IdPrefixes.Count == 0 ||
            addon.IdPrefixes.Any(p => contentId.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        private static void ValidateManifest(AddonManifest? manifest)
        {
            if (manifest == null) throw new InvalidDataException("The server returned an empty manifest.");
            if (string.IsNullOrWhiteSpace(manifest.Id)) throw new InvalidDataException("Manifest is missing its add-on ID.");
            if (manifest.Id.Length > 256) throw new InvalidDataException("Manifest add-on ID is too long.");
            if (string.IsNullOrWhiteSpace(manifest.Name)) throw new InvalidDataException("Manifest is missing its add-on name.");
            if (manifest.GetResourceNames().Count == 0) throw new InvalidDataException("Manifest does not declare any supported resources.");
            if (manifest.Types?.Any(string.IsNullOrWhiteSpace) == true) throw new InvalidDataException("Manifest contains an invalid content type.");
        }

        private static void ApplyManifest(InstalledAddon addon, AddonManifest manifest, string manifestUrl)
        {
            addon.ManifestUrl = manifestUrl;
            addon.BaseUrl = GetBaseUrl(manifestUrl);
            addon.Id = manifest.Id!;
            addon.Name = manifest.Name ?? manifest.Id!;
            addon.Description = manifest.Description;
            addon.Version = manifest.Version;
            addon.Logo = manifest.Logo;
            addon.Types = manifest.Types?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new();
            addon.Resources = manifest.GetResourceNames().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            addon.ManifestResources = manifest.Resources?.Select(resource => resource.Clone()).ToList() ?? new();
            addon.ManifestAdditionalProperties = manifest.AdditionalProperties?.ToDictionary(pair => pair.Key, pair => pair.Value.Clone()) ?? new();
            addon.Catalogs = manifest.Catalogs?.Where(c => !string.IsNullOrWhiteSpace(c.Type) && !string.IsNullOrWhiteSpace(c.Id)).ToList() ?? new();
            addon.IdPrefixes = manifest.IdPrefixes?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new();
            addon.IsOfficial = manifestUrl.Contains("v3-cinemeta.strem.io", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeManifestUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("Enter an add-on manifest URL.", nameof(url));

            url = url.Trim();
            if (url.StartsWith("stremio://", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url.Substring("stremio://".Length);

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                throw new ArgumentException("Add-on URLs must use HTTP or HTTPS.", nameof(url));

            if (!uri.AbsolutePath.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                var builder = new UriBuilder(uri)
                {
                    Path = uri.AbsolutePath.TrimEnd('/') + "/manifest.json"
                };
                uri = builder.Uri;
            }

            return uri.AbsoluteUri;
        }

        private static string GetBaseUrl(string manifestUrl)
        {
            var idx = manifestUrl.LastIndexOf("/manifest.json", StringComparison.OrdinalIgnoreCase);
            return idx > 0 ? manifestUrl.Substring(0, idx) : manifestUrl.TrimEnd('/');
        }

        private static string StoragePath =>
            Path.Combine(ApplicationData.Current.LocalFolder.Path, AddonStorageFileName);

        private List<InstalledAddon> LoadFromStorage()
        {
            try
            {
                if (File.Exists(StoragePath))
                {
                    var json = File.ReadAllText(StoragePath);
                    var loaded = JsonSerializer.Deserialize(json, StremioJsonContext.Default.ListInstalledAddon);
                    if (loaded != null) return loaded;
                }
                // Migrate older LocalSettings installations once.
                var values = ApplicationData.Current.LocalSettings.Values;
                if (values.TryGetValue("InstalledAddons", out var raw) && raw is string legacy && !string.IsNullOrWhiteSpace(legacy))
                {
                    var migrated = JsonSerializer.Deserialize(legacy, StremioJsonContext.Default.ListInstalledAddon) ?? new();
                    SaveToStorage(migrated);
                    return migrated;
                }
            }
            catch { /* Corrupt local cache must not prevent startup. */ }
            return new List<InstalledAddon>();
        }

        private void SaveToStorage() => SaveToStorage(_addons);

        private static void SaveToStorage(List<InstalledAddon> addons)
        {
            try
            {
                var json = JsonSerializer.Serialize(addons, StremioJsonContext.Default.ListInstalledAddon);
                var tempPath = StoragePath + ".tmp";
                File.WriteAllText(tempPath, json);
                if (File.Exists(StoragePath)) File.Delete(StoragePath);
                File.Move(tempPath, StoragePath);
            }
            catch { /* Keep current in-memory state if storage is temporarily unavailable. */ }
        }
    }

    public sealed class AddonSyncSummary
    {
        public int Succeeded { get; set; }
        public int Failed { get; set; }
        public List<string> Errors { get; } = new();
    }
}
