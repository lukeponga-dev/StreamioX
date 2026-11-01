using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
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

        private static readonly JsonSerializerOptions StorageJson = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            TypeInfoResolver = StremioJsonContext.Default
        };

        private readonly StremioClient _client;
        private readonly List<InstalledAddon> _addons = new();
        private bool _initialized;
        private readonly SemaphoreSlim _initializeLock = new(1, 1);

        public IReadOnlyList<InstalledAddon> InstalledAddons => _addons;

        public AddonService(StremioClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            // App startup and page navigation can request initialization at the same time.
            // Serialize initialization so the installed add-on list is never cleared or
            // modified while a catalog request is enumerating it.
            await _initializeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_initialized) return;

                _addons.Clear();
                var stored = LoadFromSettings();
                if (stored.Count > 0)
                {
                    _addons.AddRange(stored);
                }
                else
                {
                    // First run: install official Cinemeta
                    var cinemeta = await InstallFromManifestUrlAsync(OfficialCinemetaManifestUrl, cancellationToken)
                        .ConfigureAwait(false);
                    if (cinemeta != null)
                        cinemeta.IsOfficial = true;
                }

                // Ensure Cinemeta is present and refresh its manifest on each app start.
                // Stored manifests can become stale and no longer describe valid catalogs.
                var refreshedCinemeta = await InstallFromManifestUrlAsync(OfficialCinemetaManifestUrl, cancellationToken)
                    .ConfigureAwait(false);
                if (refreshedCinemeta != null)
                    refreshedCinemeta.IsOfficial = true;

                _initialized = true;
            }
            finally
            {
                _initializeLock.Release();
            }
        }

        public async Task<InstalledAddon?> InstallFromManifestUrlAsync(
            string manifestUrl,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(manifestUrl))
                return null;

            manifestUrl = NormalizeManifestUrl(manifestUrl);

            var manifest = await _client.GetFromJsonAsync<AddonManifest>(manifestUrl, cancellationToken)
                .ConfigureAwait(false);

            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id))
                return null;

            // Already installed?
            var existing = _addons.FirstOrDefault(a =>
                string.Equals(a.Id, manifest.Id, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                // Refresh cached manifest metadata so changed catalog IDs/resources do not
                // leave the app with a stale provider definition.
                existing.ManifestUrl = manifestUrl;
                existing.BaseUrl = GetBaseUrl(manifestUrl);
                existing.Name = manifest.Name ?? manifest.Id!;
                existing.Description = manifest.Description;
                existing.Version = manifest.Version;
                existing.Logo = manifest.Logo;
                existing.Types = manifest.Types?.ToList() ?? new List<string>();
                existing.Resources = manifest.GetResourceNames().ToList();
                existing.Catalogs = manifest.Catalogs?.ToList() ?? new List<CatalogDescriptor>();
                existing.IdPrefixes = manifest.IdPrefixes?.ToList() ?? new List<string>();
                existing.IsOfficial = manifestUrl.Contains("cinemeta", StringComparison.OrdinalIgnoreCase);
                SaveToSettings();
                return existing;
            }

            var baseUrl = GetBaseUrl(manifestUrl);
            var addon = new InstalledAddon
            {
                ManifestUrl = manifestUrl,
                BaseUrl = baseUrl,
                Id = manifest.Id!,
                Name = manifest.Name ?? manifest.Id!,
                Description = manifest.Description,
                Version = manifest.Version,
                Logo = manifest.Logo,
                Types = manifest.Types?.ToList() ?? new List<string>(),
                Resources = manifest.GetResourceNames().ToList(),
                Catalogs = manifest.Catalogs?.ToList() ?? new List<CatalogDescriptor>(),
                IdPrefixes = manifest.IdPrefixes?.ToList() ?? new List<string>(),
                IsOfficial = manifestUrl.Contains("cinemeta", StringComparison.OrdinalIgnoreCase)
            };

            _addons.Add(addon);
            SaveToSettings();
            return addon;
        }

        public Task UninstallAsync(string addonId)
        {
            var item = _addons.FirstOrDefault(a =>
                string.Equals(a.Id, addonId, StringComparison.OrdinalIgnoreCase));
            if (item == null)
                return Task.CompletedTask;

            // Keep at least one catalog provider
            if (item.IsOfficial && _addons.Count(a => a.Resources.Contains("catalog", StringComparer.OrdinalIgnoreCase)) <= 1)
                return Task.CompletedTask;

            _addons.Remove(item);
            SaveToSettings();
            return Task.CompletedTask;
        }

        public IReadOnlyList<InstalledAddon> GetCatalogProviders(string? type = null)
        {
            return _addons
                .Where(a => a.Resources.Any(r => string.Equals(r, "catalog", StringComparison.OrdinalIgnoreCase)))
                .Where(a => type == null
                            || a.Types.Count == 0
                            || a.Types.Any(t => string.Equals(t, type, StringComparison.OrdinalIgnoreCase))
                            || a.Catalogs.Any(c => string.Equals(c.Type, type, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        public IReadOnlyList<InstalledAddon> GetMetaProviders(string? type = null, string? contentId = null)
        {
            return _addons
                .Where(a => a.Resources.Any(r => string.Equals(r, "meta", StringComparison.OrdinalIgnoreCase)))
                .Where(a => type == null
                            || a.Types.Count == 0
                            || a.Types.Any(t => string.Equals(t, type, StringComparison.OrdinalIgnoreCase)))
                .Where(a => contentId == null
                            || a.IdPrefixes.Count == 0
                            || a.IdPrefixes.Any(p => contentId.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        public IReadOnlyList<InstalledAddon> GetStreamProviders(string? type = null, string? contentId = null)
        {
            return _addons
                .Where(a => a.Resources.Any(r => string.Equals(r, "stream", StringComparison.OrdinalIgnoreCase)))
                .Where(a => type == null
                            || a.Types.Count == 0
                            || a.Types.Any(t => string.Equals(t, type, StringComparison.OrdinalIgnoreCase)))
                .Where(a => contentId == null
                            || a.IdPrefixes.Count == 0
                            || a.IdPrefixes.Any(p => contentId.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        private static string NormalizeManifestUrl(string url)
        {
            url = url.Trim();
            if (url.StartsWith("stremio://", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url.Substring("stremio://".Length);

            if (!url.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase)
                && !url.EndsWith("/"))
            {
                // allow bare base
            }

            if (!url.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                url = url.TrimEnd('/') + "/manifest.json";
            }

            return url;
        }

        private static string GetBaseUrl(string manifestUrl)
        {
            var idx = manifestUrl.LastIndexOf("/manifest.json", StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
                return manifestUrl.Substring(0, idx);
            return manifestUrl.TrimEnd('/');
        }

        private const string AddonStorageFileName = "installed-addons.json";

        private List<InstalledAddon> LoadFromSettings()
        {
            var typeInfo = (System.Text.Json.Serialization.Metadata.JsonTypeInfo<List<InstalledAddon>>)
                StremioJsonContext.Default.GetTypeInfo(typeof(List<InstalledAddon>));

            try
            {
                // Add-on manifests can make this collection larger than the WinRT
                // LocalSettings value limit. Keep the JSON in LocalFolder instead.
                var filePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, AddonStorageFileName);
                if (File.Exists(filePath))
                {
                    var fileJson = File.ReadAllText(filePath);
                    if (!string.IsNullOrWhiteSpace(fileJson))
                    {
                        return JsonSerializer.Deserialize(fileJson, typeInfo) ?? new List<InstalledAddon>();
                    }
                }

                // One-time migration from the old LocalSettings string, if present.
                var local = ApplicationData.Current.LocalSettings;
                if (local.Values.TryGetValue("InstalledAddons", out var raw)
                    && raw is string legacyJson
                    && !string.IsNullOrWhiteSpace(legacyJson))
                {
                    var migrated = JsonSerializer.Deserialize(legacyJson, typeInfo) ?? new List<InstalledAddon>();
                    if (migrated.Count > 0)
                    {
                        var migratedJson = JsonSerializer.Serialize(migrated, typeInfo);
                        File.WriteAllText(filePath, migratedJson);
                    }

                    return migrated;
                }
            }
            catch (Exception)
            {
                // A corrupt or inaccessible cache should not prevent the app from starting.
            }

            return new List<InstalledAddon>();
        }

        private void SaveToSettings()
        {
            try
            {
                var typeInfo = (System.Text.Json.Serialization.Metadata.JsonTypeInfo<List<InstalledAddon>>)
                    StremioJsonContext.Default.GetTypeInfo(typeof(List<InstalledAddon>));
                var json = JsonSerializer.Serialize(_addons, typeInfo);
                var filePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, AddonStorageFileName);
                File.WriteAllText(filePath, json);
            }
            catch (Exception)
            {
                // Keep the in-memory add-on list usable if local persistence is unavailable.
            }
        }
    }
}
