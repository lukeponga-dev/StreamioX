using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;
using StreamioX.App.Services;
using WinRT;

namespace StreamioX.App.ViewModels
{
    [GeneratedBindableCustomProperty]
    public partial class DiscoverViewModel : ObservableObject
    {
        private readonly ICatalogService _catalogService;
        private readonly IAddonService _addonService;
        private CancellationTokenSource? _loadCts;
        private ObservableCollection<Movie> _movies = new();
        private bool _isLoading;
        private string? _errorMessage;
        private string _statusMessage = "Discover page loaded. Waiting for catalog request...";

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public ObservableCollection<Movie> Movies
        {
            get => _movies;
            private set => SetProperty(ref _movies, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        public DiscoverViewModel(ICatalogService catalogService, IAddonService addonService)
        {
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _addonService = addonService ?? throw new ArgumentNullException(nameof(addonService));
        }

        public async Task LoadCatalogAsync()
        {
            if (IsLoading) return;

            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            IsLoading = true;
            ErrorMessage = null;
            StatusMessage = "Connecting to catalog add-ons...";

            try
            {
                await _addonService.InitializeAsync(token).ConfigureAwait(true);
                StatusMessage = $"Add-ons ready: {_addonService.GetCatalogProviders("movie").Count} movie catalog provider(s). Loading catalog...";
                var items = await _catalogService
                    .GetFromInstalledAddonsAsync("movie", "top", token)
                    .ConfigureAwait(true);

                Movies.Clear();
                foreach (var item in items)
                    Movies.Add(item);

                StatusMessage = $"Catalog request complete. Received {items.Count} item(s); displayed collection contains {Movies.Count}.";
                if (Movies.Count == 0)
                    ErrorMessage = "No catalog results. Install a catalog addon (e.g. Cinemeta).";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Catalog request was cancelled.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Catalog loading failed. See error below.";
                ErrorMessage = $"Failed to load catalog: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}