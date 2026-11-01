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

        [ObservableProperty]
        private ObservableCollection<InstalledAddon> addons = new();

        [ObservableProperty]
        private string? manifestUrlInput;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string? statusMessage;

        public AddonsViewModel(IAddonService addonService)
        {
            _addonService = addonService ?? throw new ArgumentNullException(nameof(addonService));
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
            }
            catch (Exception ex)
            {
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task InstallAsync()
        {
            if (string.IsNullOrWhiteSpace(ManifestUrlInput))
            {
                StatusMessage = "Enter an addon manifest URL.";
                return;
            }

            IsBusy = true;
            StatusMessage = null;
            try
            {
                var addon = await _addonService.InstallFromManifestUrlAsync(ManifestUrlInput.Trim());
                if (addon == null)
                {
                    StatusMessage = "Could not install addon. Check the URL.";
                }
                else
                {
                    StatusMessage = $"Installed: {addon.Name}";
                    ManifestUrlInput = string.Empty;
                    RefreshList();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Install failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task UninstallAsync(InstalledAddon? addon)
        {
            if (addon == null) return;
            if (addon.IsOfficial)
            {
                StatusMessage = "Cannot remove the official Cinemeta addon.";
                return;
            }

            IsBusy = true;
            try
            {
                await _addonService.UninstallAsync(addon.Id);
                StatusMessage = $"Removed: {addon.Name}";
                RefreshList();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Remove failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void RefreshList()
        {
            Addons.Clear();
            foreach (var a in _addonService.InstalledAddons)
                Addons.Add(a);
        }
    }
}
