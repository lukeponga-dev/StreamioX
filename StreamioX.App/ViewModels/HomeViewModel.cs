using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
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
        public ObservableCollection<Movie> RecentlyAdded { get; } = new();
        public ObservableCollection<LibraryEntry> ContinueWatching { get; } = new();

        [ObservableProperty] public partial bool IsLoading { get; set; }
        [ObservableProperty] public partial string StatusMessage { get; set; } = "Your Stremio library and installed catalogs";
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
            try
            {
                var libraryTask = _libraryService.GetEntriesAsync();
                var moviesTask = _catalogService.GetFromInstalledAddonsAsync("movie", "top");
                var seriesTask = _catalogService.GetFromInstalledAddonsAsync("series", "top");
                await Task.WhenAll(libraryTask, moviesTask, seriesTask);

                ContinueWatching.Clear();
                foreach (var entry in libraryTask.Result
                    .Where(x => x.Movie != null && x.PositionSeconds > 0 && x.DurationSeconds > 0)
                    .OrderByDescending(x => x.LastPlayedUtc ?? x.AddedUtc).Take(8))
                    ContinueWatching.Add(entry);

                PopularMovies.Clear();
                foreach (var item in moviesTask.Result.Take(14)) PopularMovies.Add(item);

                PopularSeries.Clear();
                foreach (var item in seriesTask.Result.Take(14)) PopularSeries.Add(item);

                Recommended.Clear();
                foreach (var item in moviesTask.Result.Skip(4).Take(14)) Recommended.Add(item);

                RecentlyAdded.Clear();
                foreach (var item in moviesTask.Result.Concat(seriesTask.Result)
                    .OrderByDescending(x => x.ReleaseInfo ?? string.Empty).Take(14))
                    RecentlyAdded.Add(item);

                StatusMessage = $"{PopularMovies.Count} movies · {PopularSeries.Count} series · {ContinueWatching.Count} in progress";
                if (PopularMovies.Count == 0 && PopularSeries.Count == 0)
                    ErrorMessage = "No catalog titles are available. Install a catalog add-on from Add-ons.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                StatusMessage = "Couldn't load your catalogs";
            }
            finally { IsLoading = false; }
        }

        public void OpenMovie(Movie movie)
        {
            if (movie != null) _navigationService.NavigateTo(NavigationRoute.Details, movie);
        }

        public void OpenLibraryEntry(LibraryEntry entry)
        {
            if (entry?.Movie != null) OpenMovie(entry.Movie);
        }
    }
}