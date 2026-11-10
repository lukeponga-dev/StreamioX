using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using StreamioX.App.Models;
using StreamioX.App.Services;

namespace StreamioX.App.ViewModels
{
    public partial class AddonsViewModel : ObservableObject
    {
        private readonly IAddonService _addonService;
        private readonly IAuthService _authService;

        [ObservableProperty]
        public partial ObservableCollection<InstalledAddon> Addons { get; set; } = new();

        [ObservableProperty]
        public partial string? ManifestUrlInput { get; set; }

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        [ObservableProperty]
        public partial string? StatusMessage { get; set; }

        public AddonsViewModel(IAddonService addonService, IAuthService authService)
        {
            _addonService = addonService ?? throw new ArgumentNullException(nameof(addonService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            IsBusy = true;
            StatusMessage = null;
            try
            {
                await _addonService.InitializeAsync();
                RefreshList();
                StatusMessage = "Installed add-ons are ready. Use Sync all to refresh their manifests.";
            }
            catch (Exception ex) { StatusMessage = $"Load failed: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task InstallAsync()
        {
            if (string.IsNullOrWhiteSpace(ManifestUrlInput))
            {
                StatusMessage = "Enter an add-on manifest URL.";
                return;
            }

            IsBusy = true;
            StatusMessage = null;
            try
            {
                var addon = await _addonService.InstallFromManifestUrlAsync(ManifestUrlInput.Trim());
                if (addon == null) StatusMessage = "The add-on server returned no manifest.";
                else
                {
                    StatusMessage = $"Installed and synchronized: {addon.Name}";
                    ManifestUrlInput = string.Empty;
                    RefreshList();
                }
            }
            catch (Exception ex) { StatusMessage = $"Install failed: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task SyncAllAsync()
        {
            IsBusy = true;
            StatusMessage = "Synchronizing installed add-ons…";
            try
            {
                await _addonService.InitializeAsync();
                var result = await _addonService.SyncAllAsync();
                RefreshList();
                StatusMessage = result.Failed == 0
                    ? $"Sync complete: {result.Succeeded} add-on(s) updated."
                    : $"Sync complete: {result.Succeeded} updated, {result.Failed} failed. {string.Join(" | ", result.Errors)}";
            }
            catch (Exception ex) { StatusMessage = $"Sync failed: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task UninstallAsync(InstalledAddon? addon)
        {
            if (addon == null) return;
            IsBusy = true;
            try
            {
                await _addonService.UninstallAsync(addon.Id);
                RefreshList();
                StatusMessage = $"Removed: {addon.Name}";

                if (_authService.IsLoggedIn)
                {
                    try
                    {
                        await _authService.PushAddonCollectionAsync();
                        StatusMessage = $"Removed: {addon.Name}. Your Stremio account was updated.";
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"Removed: {addon.Name}. Your Stremio account will be updated on the next sync ({ex.Message}).";
                    }
                }
            }
            catch (Exception ex) { StatusMessage = $"Remove failed: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        private void RefreshList()
        {
            Addons.Clear();
            foreach (var addon in _addonService.InstalledAddons)
                Addons.Add(addon);
        }
    }
}
