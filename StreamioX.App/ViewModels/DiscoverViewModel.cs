// Purpose: Owns DiscoverViewModel screen state and user actions, keeping presentation logic separate from the XAML view.

using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;
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
        private const string CinemetaBaseUrl = "https://v3-cinemeta.strem.io";
        private readonly ICatalogService _catalogService;
        private readonly IAddonService _addonService;
        private CancellationTokenSource? _loadCts;
        private int _loadGeneration;
        private ObservableCollection<Movie> _movies = new();
        private bool _isLoading;
        private string? _errorMessage;
        private string _statusMessage = "Browse movies and series from Cinemeta.";

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

        /// <summary>Loads movie and series catalogs, optionally replacing an in-progress request.</summary>
        public async Task LoadCatalogAsync(bool forceRefresh = false)
        {
            try
            {
                if (IsLoading && !forceRefresh)
                    return;


                // A refresh supersedes the previous request. The generation check below
                // prevents a late response from an older request replacing newer results.
                _loadCts?.Cancel();
                var loadCts = new CancellationTokenSource(TimeSpan.FromSeconds(35));
                _loadCts = loadCts;
                var generation = ++_loadGeneration;
                var token = loadCts.Token;

                IsLoading = true;
                ErrorMessage = null;
                StatusMessage = "Loading movies and series from Cinemeta…";

                try
                {
                    // Cinemeta is the default metadata catalog and should work even when
                    // the user has not installed any third-party catalog add-ons.
                    var movieTask = LoadCatalogTypeAsync("movie", token);
                    var seriesTask = LoadCatalogTypeAsync("series", token);
                    await Task.WhenAll(movieTask, seriesTask).ConfigureAwait(true);

                    if (generation != _loadGeneration)
                        return;

                    var combined = movieTask.Result
                        .Concat(seriesTask.Result)
                        .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id))
                        .GroupBy(item => (item.Type ?? "movie") + ":" + item.Id,
                            StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.First())
                        .ToList();

                    if (combined.Count == 0)
                        throw new InvalidOperationException(
                            "Cinemeta and your installed catalog add-ons returned no titles.");

                    Movies = new ObservableCollection<Movie>(combined);
                    var movieCount = Movies.Count(item =>
                        string.Equals(item.Type, "movie", StringComparison.OrdinalIgnoreCase));
                    var seriesCount = Movies.Count(item =>
                        string.Equals(item.Type, "series", StringComparison.OrdinalIgnoreCase));
                    StatusMessage = $"{movieCount} movies · {seriesCount} series · {Movies.Count} titles";
                }
                catch (OperationCanceledException)
                {
                    if (generation != _loadGeneration)
                        return;

                    StatusMessage = "Catalog loading timed out or was cancelled. Try again.";
                    ErrorMessage = "The catalog request took too long. Check your connection and retry.";
                }
                catch (Exception ex)
                {
                    if (generation != _loadGeneration)
                        return;

                    System.Diagnostics.Debug.WriteLine($"[StreamioX] Discover catalog failed: {ex}");
                    StatusMessage = "Catalog unavailable";
                    ErrorMessage = "Couldn't load the catalog. " + ex.Message;
                    Movies = new ObservableCollection<Movie>();
                }
                finally
                {
                    loadCts.Dispose();
                    if (generation == _loadGeneration)
                    {
                        _loadCts = null;
                        IsLoading = false;
                    }
                }
            }
            catch (Exception ex)
            {
                StartupDiagnostics.Write(
                    "DiscoverViewModel.LoadCatalogAsync FAILED",
                    ex);

                throw;
            }

        }

        /// <summary>Cancels the active catalog request when Discover is left.</summary>
        public void CancelLoad()
        {
            if (!IsLoading)
                return;

            ++_loadGeneration;
            _loadCts?.Cancel();
            _loadCts = null;
            IsLoading = false;
            StatusMessage = "Catalog loading paused.";
        }

        /// <summary>Requests a catalog for one content type and falls back to installed providers.</summary>
        private async Task<System.Collections.Generic.IReadOnlyList<Movie>> LoadCatalogTypeAsync(
            string type, CancellationToken cancellationToken)
        {
            try
            {
                var directResults = await _catalogService.GetCatalogAsync(
             CinemetaBaseUrl, type, "top", cancellationToken)
             .ConfigureAwait(false);

                if (directResults is { Count: > 0 })
                {
                    foreach (var item in directResults.Where(x => x != null))
                    {
                        item.Type ??= type;
                    }

                    return directResults;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[StreamioX] Discover Cinemeta {type} failed: {ex}");
            }

            // Third-party catalog add-ons remain supported as a fallback.
            try
            {
                var addonResults = await _catalogService.GetFromInstalledAddonsAsync(
                    type, "top", cancellationToken).ConfigureAwait(false);
                foreach (var item in addonResults.Where(x => x != null))
                {
                    item.Type ??= type;
                }
                return addonResults;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[StreamioX] Discover installed {type} catalogs failed: {ex}");
                return Array.Empty<Movie>();
            }
        }
    }
}