using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Models;
using StreamioX.App.Navigation;
using StreamioX.App.Services;

namespace StreamioX.App.ViewModels
{
    public partial class DetailsViewModel : ObservableObject
    {
        private readonly IStreamService _streamService;
        private readonly IMetadataService _metadataService;
        private readonly INavigationService _navigationService;
        private readonly IAddonService _addonService;
        private const string TestAddonUrl = "https://caching.stremio.net/publicdomainmovies.now.sh/manifest.json";

        private CancellationTokenSource? _loadCts;
        private CancellationTokenSource? _streamsCts;

        [ObservableProperty] private Movie? selectedMovie;
        [ObservableProperty] private ObservableCollection<StreamSource> streams = new();
        [ObservableProperty] private ObservableCollection<Video> episodes = new();
        [ObservableProperty] private Video? selectedEpisode;
        [ObservableProperty] private bool isLoadingStreams;
        [ObservableProperty] private bool isLoadingMetadata;
        [ObservableProperty] private string? errorMessage;

        // Flatten nested metadata into bindable page properties. This avoids UWP
        // nested-property binding and string-to-ImageSource conversion issues.
        public string TitleText => SelectedMovie?.Title ?? "Title unavailable";
        public string DescriptionText => SelectedMovie?.Description ?? "No description is available for this title.";
        public string ReleaseText => SelectedMovie?.ReleaseInfo ?? string.Empty;
        public string GenreText => SelectedMovie?.Genres == null ? string.Empty : string.Join("  •  ", SelectedMovie.Genres);
        public string RatingText => string.IsNullOrWhiteSpace(SelectedMovie?.ImdbRating) ? string.Empty : $"IMDb {SelectedMovie.ImdbRating}";
        public string RuntimeText => SelectedMovie?.Runtime ?? string.Empty;
        public string? PosterUrl => SelectedMovie?.PosterUrl;
        public string? EpisodesVisibility => Episodes.Count > 0 ? "visible" : null;
        public string ContentTypeText => string.Equals(SelectedMovie?.Type, "series", StringComparison.OrdinalIgnoreCase) ? "SERIES" : "MOVIE";

        public DetailsViewModel(
            IStreamService streamService,
            IMetadataService metadataService,
            INavigationService navigationService,
            IAddonService addonService)
        {
            _streamService = streamService ?? throw new ArgumentNullException(nameof(streamService));
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _addonService = addonService ?? throw new ArgumentNullException(nameof(addonService));
        }

        public async Task InitializeAsync(Movie movie)
        {
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            movie.Type ??= "movie";
            SelectedMovie = movie;
            NotifyDetailsProperties();
            Episodes.Clear();
            Streams.Clear();
            SelectedEpisode = null;
            ErrorMessage = null;
            OnPropertyChanged(nameof(GenreText));
            OnPropertyChanged(nameof(RatingText));
            OnPropertyChanged(nameof(RuntimeText));
            OnPropertyChanged(nameof(ContentTypeText));

            try
            {
                await _addonService.InitializeAsync(token).ConfigureAwait(true);

                // Test provider: official Stremio Public Domain Movies add-on.
                // Its manifest advertises movie streams and IMDb ID support.
                await _addonService.InstallFromManifestUrlAsync(TestAddonUrl, token)
                    .ConfigureAwait(true);

                // Catalog results are previews. Resolve the full Stremio meta object
                // before presenting the detail view or requesting episode streams.
                IsLoadingMetadata = true;
                var fullMeta = await _metadataService.GetMetaAsync(
                    movie.Type ?? "movie", movie.Id ?? string.Empty, token).ConfigureAwait(true);
                if (fullMeta != null)
                    SelectedMovie = MergePreview(movie, fullMeta);

                NotifyDetailsProperties();

                if (SelectedMovie?.Videos != null)
                {
                    foreach (var episode in SelectedMovie.Videos)
                        Episodes.Add(episode);
                }
                OnPropertyChanged(nameof(EpisodesVisibility));

                if (string.Equals(SelectedMovie?.Type, "series", StringComparison.OrdinalIgnoreCase)
                    && Episodes.Count > 0)
                {
                    // Series streams are requested by video/episode ID, not the series ID.
                    return;
                }

                await LoadStreamsAsync(SelectedMovie?.Id ?? movie.Id ?? string.Empty, token).ConfigureAwait(true);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ErrorMessage = $"Unable to load title details: {ex.Message}"; }
            finally
            {
                IsLoadingMetadata = false;
            }
        }

        [RelayCommand]
        private async Task PlayEpisodeAsync(Video? episode)
        {
            if (episode == null || string.IsNullOrWhiteSpace(episode.Id)) return;
            SelectedEpisode = episode;
            await LoadStreamsAsync(episode.Id, CancellationToken.None).ConfigureAwait(true);
        }

        private async Task LoadStreamsAsync(string contentId, CancellationToken cancellationToken)
        {
            if (SelectedMovie == null || string.IsNullOrWhiteSpace(contentId)) return;

            _streamsCts?.Cancel();
            _streamsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _streamsCts.Token;

            IsLoadingStreams = true;
            ErrorMessage = null;
            Streams.Clear();
            try
            {
                var fetched = await _streamService.GetFromInstalledAddonsAsync(
                    SelectedMovie.Type ?? "movie", contentId, token).ConfigureAwait(true);
                foreach (var stream in fetched)
                    Streams.Add(stream);

                if (Streams.Count == 0)
                    ErrorMessage = "No streams found for this selection. Install a stream add-on that supports this title.";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ErrorMessage = $"Failed to load streams: {ex.Message}"; }
            finally { IsLoadingStreams = false; }
        }

        private void NotifyDetailsProperties()
        {
            OnPropertyChanged(nameof(TitleText));
            OnPropertyChanged(nameof(DescriptionText));
            OnPropertyChanged(nameof(ReleaseText));
            OnPropertyChanged(nameof(GenreText));
            OnPropertyChanged(nameof(RatingText));
            OnPropertyChanged(nameof(RuntimeText));
            OnPropertyChanged(nameof(PosterUrl));
            OnPropertyChanged(nameof(ContentTypeText));
        }

        private static Movie MergePreview(Movie preview, Movie full)
        {
            full.Id ??= preview.Id;
            full.Type ??= preview.Type;
            full.Title ??= preview.Title;
            full.PosterUrl ??= preview.PosterUrl;
            full.ReleaseInfo ??= preview.ReleaseInfo;
            full.Description ??= preview.Description;
            return full;
        }

        [RelayCommand]
        private void PlayStream(StreamSource? stream)
        {
            if (stream == null || string.IsNullOrWhiteSpace(stream.Url)) return;
            _navigationService.NavigateTo(NavigationRoute.Player, stream.Url);
        }
    }
}
