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
        private readonly ITorrentBridgeService _torrentBridgeService;
        private readonly ILibraryService _libraryService;

        private CancellationTokenSource? _loadCts;
        private CancellationTokenSource? _streamsCts;

        [ObservableProperty] private Movie? selectedMovie;
        [ObservableProperty] private ObservableCollection<StreamSource> streams = new();
        [ObservableProperty] private ObservableCollection<Video> episodes = new();
        [ObservableProperty] private Video? selectedEpisode;
        [ObservableProperty] private bool isLoadingStreams;
        [ObservableProperty] private bool isLoadingMetadata;
        [ObservableProperty] private string? errorMessage;
        [ObservableProperty] private bool isSavedInLibrary;
        [ObservableProperty] public partial bool HasResumeProgress { get; set; }
        [ObservableProperty] public partial bool IsResumeRequested { get; set; }
        [ObservableProperty] public partial string ResumeButtonText { get; set; } = "Resume Watching";
        // Partial-property generation is WinRT/NativeAOT compatible.
        [ObservableProperty] public partial bool IsSeries { get; set; }
        [ObservableProperty] private string libraryButtonText = "Add to Library";
        private LibraryEntry? _playbackEntry;

        // Flatten nested metadata into bindable page properties. This avoids UWP
        // nested-property binding and string-to-ImageSource conversion issues.
        private static string NonEmpty(string? value, string fallback = "") =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        public string TitleText => NonEmpty(SelectedMovie?.Title, "Title unavailable");
        public string DescriptionText => NonEmpty(SelectedMovie?.Description, "No description is available for this title.");
        public string ReleaseText => NonEmpty(SelectedMovie?.ReleaseInfo);
        public string GenreText => SelectedMovie?.Genres == null ? string.Empty :
            string.Join("  •  ", SelectedMovie.Genres.Where(g => !string.IsNullOrWhiteSpace(g)));
        public string RatingText => string.IsNullOrWhiteSpace(SelectedMovie?.ImdbRating) ? string.Empty : $"IMDb {SelectedMovie.ImdbRating}";
        public string RuntimeText => NonEmpty(SelectedMovie?.Runtime);
        public string? PosterUrl => NonEmpty(SelectedMovie?.PosterUrl) is { Length: > 0 } poster ? poster : null;
        public string? EpisodesVisibility => Episodes.Count > 0 ? "visible" : null;
        public string ContentTypeText => string.Equals(SelectedMovie?.Type, "series", StringComparison.OrdinalIgnoreCase) ? "SERIES" : "MOVIE";

        public DetailsViewModel(
            IStreamService streamService,
            IMetadataService metadataService,
            INavigationService navigationService,
            IAddonService addonService,
            ITorrentBridgeService torrentBridgeService,
            ILibraryService libraryService)
        {
            _streamService = streamService ?? throw new ArgumentNullException(nameof(streamService));
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _addonService = addonService ?? throw new ArgumentNullException(nameof(addonService));
            _torrentBridgeService = torrentBridgeService ?? throw new ArgumentNullException(nameof(torrentBridgeService));
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
        }

        public async Task InitializeAsync(Movie movie)
        {
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            movie.Type ??= "movie";
            SelectedMovie = movie;
            IsSeries = string.Equals(movie.Type, "series", StringComparison.OrdinalIgnoreCase);
            IsSavedInLibrary = await _libraryService.IsSavedAsync(movie.Type ?? "movie", movie.Id ?? string.Empty).ConfigureAwait(true);
            var libraryEntries = await _libraryService.GetEntriesAsync().ConfigureAwait(true);
            _playbackEntry = libraryEntries.FirstOrDefault(entry =>
                string.Equals(entry.Movie?.Id, movie.Id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.Movie?.Type ?? "movie", movie.Type ?? "movie", StringComparison.OrdinalIgnoreCase));
            IsResumeRequested = false;
            LibraryButtonText = IsSavedInLibrary ? "Remove from Library" : "Add to Library";
            NotifyDetailsProperties();
            Episodes.Clear();
            Streams.Clear();
            SelectedEpisode = null;
            ErrorMessage = null;
            UpdateResumeAvailability();
            OnPropertyChanged(nameof(GenreText));
            OnPropertyChanged(nameof(RatingText));
            OnPropertyChanged(nameof(RuntimeText));
            OnPropertyChanged(nameof(ContentTypeText));

            try
            {
                await _addonService.InitializeAsync(token).ConfigureAwait(true);

                // Catalog results are previews. Resolve the full Stremio meta object
                // before presenting the detail view or requesting episode streams.
                IsLoadingMetadata = true;
                var fullMeta = await _metadataService.GetMetaAsync(
                    movie.Type ?? "movie", movie.Id ?? string.Empty, token).ConfigureAwait(true);
                if (fullMeta != null)
                    SelectedMovie = MergePreview(movie, fullMeta);

                IsSeries = string.Equals(SelectedMovie?.Type, "series", StringComparison.OrdinalIgnoreCase);
                NotifyDetailsProperties();

                if (SelectedMovie?.Videos != null)
                {
                    foreach (var episode in SelectedMovie.Videos)
                        Episodes.Add(episode);
                }
                UpdateResumeAvailability();
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
        private async Task ToggleLibraryAsync()
        {
            if (SelectedMovie?.Id == null) return;
            if (IsSavedInLibrary)
            {
                await _libraryService.RemoveAsync(SelectedMovie.Type ?? "movie", SelectedMovie.Id).ConfigureAwait(true);
                IsSavedInLibrary = false;
                LibraryButtonText = "Add to Library";
            }
            else
            {
                await _libraryService.SaveAsync(SelectedMovie).ConfigureAwait(true);
                IsSavedInLibrary = true;
                LibraryButtonText = "Remove from Library";
            }
        }

        [RelayCommand]
        private async Task PlayEpisodeAsync(Video? episode)
        {
            if (episode == null || string.IsNullOrWhiteSpace(episode.Id)) return;
            IsResumeRequested = false;
            SelectedEpisode = episode;
            await LoadStreamsAsync(episode.Id, CancellationToken.None).ConfigureAwait(true);
        }

        [RelayCommand]
        private async Task ResumeWatchingAsync()
        {
            if (!HasResumeProgress || _playbackEntry == null)
                return;

            IsResumeRequested = true;
            if (IsSeries)
            {
                var episodeId = _playbackEntry.LastEpisodeId;
                var episode = Episodes.FirstOrDefault(item =>
                    string.Equals(item.Id, episodeId, StringComparison.OrdinalIgnoreCase));
                if (episode == null)
                {
                    ErrorMessage = "The last watched episode is no longer available in this catalog.";
                    IsResumeRequested = false;
                    return;
                }

                SelectedEpisode = episode;
                await LoadStreamsAsync(episode.Id!, CancellationToken.None).ConfigureAwait(true);
            }
        }

        private void UpdateResumeAvailability()
        {
            var hasSavedPosition = _playbackEntry != null &&
                _playbackEntry.DurationSeconds > 0 &&
                _playbackEntry.PositionSeconds > 0 &&
                _playbackEntry.PositionSeconds / _playbackEntry.DurationSeconds < 0.95;
            var hasLastEpisode = !IsSeries || Episodes.Any(episode =>
                !string.IsNullOrWhiteSpace(_playbackEntry?.LastEpisodeId) &&
                string.Equals(episode.Id, _playbackEntry.LastEpisodeId, StringComparison.OrdinalIgnoreCase));

            HasResumeProgress = hasSavedPosition && hasLastEpisode;
            ResumeButtonText = HasResumeProgress
                ? $"Resume Watching · {FormatResumeTime(_playbackEntry!.PositionSeconds)}"
                : "Resume Watching";
        }

        private static string FormatResumeTime(double seconds)
        {
            var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
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

        private static string? Prefer(string? primary, string? fallback) =>
            string.IsNullOrWhiteSpace(primary) ? fallback : primary;

        private static Movie MergePreview(Movie preview, Movie full)
        {
            // Add-on metadata can contain empty strings rather than null for
            // optional fields. Treat both as missing so the catalog preview
            // remains available when a provider returns partial metadata.
            full.Id = Prefer(full.Id, preview.Id);
            full.Type = Prefer(full.Type, preview.Type);
            full.Title = Prefer(full.Title, preview.Title);
            full.PosterUrl = Prefer(full.PosterUrl, preview.PosterUrl);
            full.BackgroundUrl = Prefer(full.BackgroundUrl, preview.BackgroundUrl);
            full.ReleaseInfo = Prefer(full.ReleaseInfo, preview.ReleaseInfo);
            full.Description = Prefer(full.Description, preview.Description);
            full.ImdbRating = Prefer(full.ImdbRating, preview.ImdbRating);
            full.Runtime = Prefer(full.Runtime, preview.Runtime);
            if (full.Genres == null || full.Genres.Count == 0)
                full.Genres = preview.Genres;
            if (full.Cast == null || full.Cast.Count == 0)
                full.Cast = preview.Cast;
            if (full.Director == null || full.Director.Count == 0)
                full.Director = preview.Director;
            if (full.Videos == null || full.Videos.Count == 0)
                full.Videos = preview.Videos;
            return full;
        }

        private void OpenPlayer(string streamUrl, StreamSource stream, bool forwardProxyHeaders = true)
        {
            var title = !string.IsNullOrWhiteSpace(SelectedEpisode?.Title)
                ? SelectedEpisode.Title
                : !string.IsNullOrWhiteSpace(SelectedMovie?.Title)
                    ? SelectedMovie.Title
                    : stream.Title ?? "Now Playing";

            // Add-on proxy headers belong to the provider's remote URL. The
            // torrent bridge is a local loopback endpoint and must not receive
            // provider cookies or Authorization values.
            var requestHeaders = forwardProxyHeaders
                ? stream.BehaviorHints?.ProxyHeaders?.Request
                : null;
            var request = new PlaybackRequest(
                streamUrl,
                title,
                seriesTitle: SelectedMovie?.Title,
                requestHeaders: requestHeaders,
                type: SelectedMovie?.Type ?? "movie",
                id: SelectedMovie?.Id,
                episodeId: SelectedEpisode?.Id,
                movie: SelectedMovie,
                resumePositionSeconds: IsResumeRequested ? _playbackEntry?.PositionSeconds ?? 0 : 0);
            if (!_navigationService.NavigateTo(NavigationRoute.Player, request))
                ErrorMessage = "The player could not be opened. Please try selecting the stream again.";
        }

        [RelayCommand]
        private async Task PlayStreamAsync(StreamSource? stream)
        {
            if (stream == null)
                return;

            if (Uri.TryCreate(stream.Url, UriKind.Absolute, out var directUri)
                && (directUri.Scheme == Uri.UriSchemeHttp || directUri.Scheme == Uri.UriSchemeHttps))
            {
                OpenPlayer(directUri.AbsoluteUri, stream);
                return;
            }

            var isMagnetLink = Uri.TryCreate(stream.Url, UriKind.Absolute, out var magnetUri)
                && string.Equals(magnetUri.Scheme, "magnet", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(stream.InfoHash) && !isMagnetLink)
            {
                ErrorMessage = "This source has no playable URL or torrent info hash.";
                return;
            }

            try
            {
                var trackerCount = stream.Sources?.Count(source => !string.IsNullOrWhiteSpace(source)) ?? 0;
                PlaybackDiagnostics.Write(
                    $"[Torrent] Torrent stream selected; info hash present: {!string.IsNullOrWhiteSpace(stream.InfoHash)}; file index: {stream.FileIdx?.ToString() ?? "automatic"}; tracker entries: {trackerCount}.");
                ErrorMessage = "Starting the torrent engine and preparing the stream...";
                var streamUrl = await _torrentBridgeService
                    .GetStreamUrlAsync(stream, CancellationToken.None)
                    .ConfigureAwait(true);

                ErrorMessage = null;
                OpenPlayer(streamUrl, stream, forwardProxyHeaders: false);
            }
            catch (Exception ex)
            {
                PlaybackDiagnostics.Write($"[Torrent] Failed to prepare torrent stream: 0x{ex.HResult:X8} {ex}");
                ErrorMessage = $"Unable to start torrent playback: {ex.Message}";
            }
        }
    }
}
