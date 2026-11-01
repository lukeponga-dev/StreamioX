using System;
using System.Collections.Generic;
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

        private static readonly JsonSerializerOptions StorageJson = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        private readonly StremioClient _client;
        private readonly List<InstalledAddon> _addons = new();
        private bool _initialized;

        public IReadOnlyList<InstalledAddon> InstalledAddons => _addons;

        public AddonService(StremioClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
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

            // Ensure Cinemeta is present
            if (!_addons.Any(a => a.Id.Contains("cinemeta", StringComparison.OrdinalIgnoreCase)
                                  || a.BaseUrl.Contains("cinemeta", StringComparison.OrdinalIgnoreCase)))
            {
                var cinemeta = await InstallFromManifestUrlAsync(OfficialCinemetaManifestUrl, cancellationToken)
                    .ConfigureAwait(false);
                if (cinemeta != null)
                    cinemeta.IsOfficial = true;
            }

            _initialized = true;
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
                return existing;

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

        private List<InstalledAddon> LoadFromSettings()
        {
            try
            {
                var local = ApplicationData.Current.LocalSettings;
                if (local.Values.TryGetValue("InstalledAddons", out var raw) && raw is string json
                    && !string.IsNullOrWhiteSpace(json))
                {
                    return JsonSerializer.Deserialize<List<InstalledAddon>>(json, StorageJson)
                           ?? new List<InstalledAddon>();
                }
            }
            catch
            {
                // ignore corrupt storage
            }
            return new List<InstalledAddon>();
        }

        private void SaveToSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_addons, StorageJson);
                ApplicationData.Current.LocalSettings.Values["InstalledAddons"] = json;
            }
            catch
            {
                // storage full / restricted — ignore for prototype
            }
        }
    }
}
