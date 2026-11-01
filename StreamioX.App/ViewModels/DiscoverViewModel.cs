using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;
using StreamioX.App.Services;

namespace StreamioX.App.ViewModels
{
    public partial class DiscoverViewModel : ObservableObject
    {
        private readonly ICatalogService _catalogService;
        private readonly IAddonService _addonService;
        private CancellationTokenSource? _loadCts;

        [ObservableProperty]
        private ObservableCollection<Movie> movies = new();

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string? errorMessage;

        public DiscoverViewModel(ICatalogService catalogService, IAddonService addonService)
        {
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _addonService = addonService ?? throw new ArgumentNullException(nameof(addonService));
        }

        [RelayCommand]
        public async Task LoadCatalogAsync()
        {
            if (IsLoading) return;

            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            IsLoading = true;
            ErrorMessage = null;

            try
            {
                await _addonService.InitializeAsync(token).ConfigureAwait(true);

                var items = await _catalogService
                    .GetFromInstalledAddonsAsync("movie", "top", token)
                    .ConfigureAwait(true);

                Movies.Clear();
                foreach (var item in items)
                    Movies.Add(item);

                if (Movies.Count == 0)
                    ErrorMessage = "No catalog results. Install a catalog addon (e.g. Cinemeta).";
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load catalog: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
