using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using StreamioX.App.Models;
using StreamioX.App.Navigation;
using StreamioX.App.Services;
using WinRT;

namespace StreamioX.App.ViewModels
{
    [GeneratedBindableCustomProperty]
    public partial class HomeViewModel : ObservableObject
    {
        private readonly INavigationService _navigationService;
        private readonly ICatalogService _catalogService;
        private readonly ILibraryService _libraryService;

        public ObservableCollection<Movie> PopularMovies { get; } = new();
        public ObservableCollection<Movie> PopularSeries { get; } = new();
        public ObservableCollection<Movie> Recommended { get; } = new();
        public ObservableCollection<Movie> MoreMovies { get; } = new();
        public ObservableCollection<LibraryEntry> ContinueWatching { get; } = new();

        [ObservableProperty] public partial Movie? FeaturedMovie { get; set; }
        [ObservableProperty] public partial bool IsLoading { get; set; }
        [ObservableProperty] public partial bool HasCatalogContent { get; set; }
        [ObservableProperty] public partial string StatusMessage { get; set; } = "Loading your Stremio catalogs…";
        [ObservableProperty] public partial string? ErrorMessage { get; set; }

        public HomeViewModel(INavigationService navigationService, ICatalogService catalogService, ILibraryService libraryService)
        {
            _navigationService = navigationService;
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
        }

        public async Task LoadAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            ErrorMessage = null;
            StatusMessage = "Loading real catalog data from Cinemeta…";
            try
            {
                // Home must always have a deterministic source for its core catalog.
                // Fetch Cinemeta directly first; installed add-ons are an optional fallback.
                using var catalogTimeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(20));
                var libraryTask = _libraryService.GetEntriesAsync();
                var moviesTask = LoadHomeCatalogAsync("movie", catalogTimeout.Token);
                var seriesTask = LoadHomeCatalogAsync("series", catalogTimeout.Token);
                await Task.WhenAll(libraryTask, moviesTask, seriesTask);

                ContinueWatching.Clear();
                foreach (var entry in libraryTask.Result
                    .Where(x => x.Movie != null && x.PositionSeconds > 0 && x.DurationSeconds > 0 &&
                                x.PositionSeconds / x.DurationSeconds < 0.95)
                    .OrderByDescending(x => x.LastPlayedUtc ?? x.AddedUtc).Take(8))
                    ContinueWatching.Add(entry);

                var movies = moviesTask.Result.Where(x => !string.IsNullOrWhiteSpace(x.Id)).ToList();
                foreach (var item in movies)
                    item.Type ??= "movie";

                PopularMovies.Clear();
                foreach (var item in movies.Take(20))
                    PopularMovies.Add(item);

                PopularSeries.Clear();
                foreach (var item in seriesTask.Result.Where(x => !string.IsNullOrWhiteSpace(x.Id)).Take(20))
                {
                    item.Type ??= "series";
                    PopularSeries.Add(item);
                }

                FeaturedMovie = PopularMovies.FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(x.BackgroundUrl) || !string.IsNullOrWhiteSpace(x.PosterUrl))
                    ?? PopularMovies.FirstOrDefault();

                // Rows below Popular continue through the catalog instead of repeating it.
                Recommended.Clear();
                foreach (var item in movies.Skip(20).Take(15)) Recommended.Add(item);

                MoreMovies.Clear();
                foreach (var item in movies.Skip(35).Take(15)) MoreMovies.Add(item);

                HasCatalogContent = PopularMovies.Count > 0 || PopularSeries.Count > 0;
                StatusMessage = $"{PopularMovies.Count} movies · {PopularSeries.Count} series · {ContinueWatching.Count} in progress";
                if (!HasCatalogContent)
                    ErrorMessage = "Cinemeta returned no titles. Check your connection or retry from Home.";
            }
            catch (Exception ex)
            {
                HasCatalogContent = false;
                FeaturedMovie = null;
                ErrorMessage = "Couldn't load catalog data. " + ex.Message;
                StatusMessage = "Catalog unavailable";
            }
            finally { IsLoading = false; }
        }

        private async Task<IReadOnlyList<Movie>> LoadHomeCatalogAsync(string type, System.Threading.CancellationToken cancellationToken)
        {
            try
            {
                var cinemeta = await _catalogService.GetCatalogAsync(
                    "https://v3-cinemeta.strem.io", type, "top", cancellationToken);
                if (cinemeta.Count > 0)
                    return cinemeta;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StreamioX] Direct Cinemeta {type} catalog failed: {ex}");
            }

            // Preserve support for user-installed catalog providers when Cinemeta is unreachable.
            return await _catalogService.GetFromInstalledAddonsAsync(type, "top", cancellationToken);
        }

        public void OpenMovie(Movie movie)
        {
            if (movie != null) _navigationService.NavigateTo(NavigationRoute.Details, movie);
        }

        public void OpenFeatured()
        {
            if (FeaturedMovie != null) OpenMovie(FeaturedMovie);
        }

        public void OpenLibraryEntry(LibraryEntry entry)
        {
            if (entry?.Movie != null) OpenMovie(entry.Movie);
        }
    }
}
