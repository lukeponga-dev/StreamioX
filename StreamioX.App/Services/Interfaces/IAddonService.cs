using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;

namespace StreamioX.App.Services
{
    public interface IAddonService
    {
        IReadOnlyList<InstalledAddon> InstalledAddons { get; }
        Task InitializeAsync(CancellationToken cancellationToken = default);
        Task<InstalledAddon?> InstallFromManifestUrlAsync(string manifestUrl, CancellationToken cancellationToken = default);
        Task<AddonSyncSummary> SyncAllAsync(CancellationToken cancellationToken = default);
        Task UninstallAsync(string addonId);
        IReadOnlyList<InstalledAddon> GetCatalogProviders(string? type = null);
        IReadOnlyList<InstalledAddon> GetMetaProviders(string? type = null, string? contentId = null);
        IReadOnlyList<InstalledAddon> GetStreamProviders(string? type = null, string? contentId = null);
    }
}