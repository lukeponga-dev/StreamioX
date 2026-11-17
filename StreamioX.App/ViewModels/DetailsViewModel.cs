// Purpose: Owns DetailsViewModel screen state and user actions, keeping presentation logic separate from the XAML view.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using StreamioX.App.Models;
using StreamioX.App.Navigation;
using StreamioX.App.Services;
using WinRT;

namespace StreamioX.App.ViewModels
{
    [GeneratedBindableCustomProperty]
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
        private CancellationTokenSource? _prefetchCts;

        // A request can finish and dispose its CTS while navigation is trying to cancel it.
        // Treat that narrow race as an already-completed request.
        private static void CancelIfActive(CancellationTokenSource? source)
        {
            try { source?.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        // Partial-property generation is WinRT/NativeAOT compatible and keeps
        // XAML-bound state marshalable when the app is trimmed or AOT compiled.
        [ObservableProperty] public partial Movie? SelectedMovie { get; set; }
        [ObservableProperty] public partial ObservableCollection<StreamSource> Streams { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<string> ProviderStatusLines { get; set; } = new();
        public int StreamCount => Streams.Count;
        [ObservableProperty] public partial ObservableCollection<Video> Episodes { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<Video> SeasonEpisodes { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<int> Seasons { get; set; } = new();
        [ObservableProperty] public partial int SelectedSeason { get; set; } = -1;
        [ObservableProperty] public partial Video? SelectedEpisode { get; set; }
        [ObservableProperty] public partial bool IsEpisodeDetailsVisible { get; set; }
        [ObservableProperty] public partial bool IsLoadingStreams { get; set; }
        [ObservableProperty] public partial bool IsLoadingMetadata { get; set; }
        [ObservableProperty] public partial bool IsLoadingSeasons { get; set; }
        [ObservableProperty] public partial bool IsLoadingEpisodes { get; set; }
        [ObservableProperty] public partial string? ErrorMessage { get; set; }
        [ObservableProperty] public partial string? StatusMessage { get; set; }
        [ObservableProperty] public partial bool IsSavedInLibrary { get; set; }
        [ObservableProperty] public partial bool HasResumeProgress { get; set; }
        [ObservableProperty] public partial bool IsResumeRequested { get; set; }
        [ObservableProperty] public partial string ResumeButtonText { get; set; } = "Resume Watching";
        [ObservableProperty] public partial bool IsSeries { get; set; }
        [ObservableProperty] public partial string LibraryButtonText { get; set; } = "＋  Add to Library";
        private LibraryEntry? _playbackEntry;

        // Explicit command properties are required because WinRT's generated bindable
        // metadata cannot see command properties produced by a second source generator.
        public IAsyncRelayCommand WatchNowCommand { get; }
        public IRelayCommand CancelStreamSearchCommand { get; }
        public IAsyncRelayCommand ToggleLibraryCommand { get; }
        public IAsyncRelayCommand PlayFromStartCommand { get; }
        public IAsyncRelayCommand<Video?> PlayEpisodeCommand { get; }
        public IAsyncRelayCommand ResumeWatchingCommand { get; }
        public IRelayCommand PlayTrailerCommand { get; }
        public IAsyncRelayCommand MarkWatchedCommand { get; }
        public IAsyncRelayCommand MarkUnwatchedCommand { get; }
        public IAsyncRelayCommand<StreamSource?> PlayStreamCommand { get; }

        // Flatten nested metadata into bindable page properties. This avoids UWP
        // nested-property binding and string-to-ImageSource conversion issues.
        private static string NonEmpty(string? value, string fallback = "") =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        public string TitleText => NonEmpty(SelectedMovie?.Title, "Title unavailable");
        public string DescriptionText => IsLoadingMetadata &&
            string.IsNullOrWhiteSpace(SelectedMovie?.Description) &&
            string.IsNullOrWhiteSpace(SelectedMovie?.Overview) &&
            string.IsNullOrWhiteSpace(SelectedMovie?.Plot)
                ? "Loading synopsis..."
                : NonEmpty(SelectedMovie?.Description,
                    NonEmpty(SelectedMovie?.Overview,
                        NonEmpty(SelectedMovie?.Plot, "No description available.")));
        public string ReleaseText => NonEmpty(SelectedMovie?.ReleaseInfo);
        public string GenreText => SelectedMovie?.Genres == null ? string.Empty :
            string.Join("  •  ", SelectedMovie.Genres.Where(g => !string.IsNullOrWhiteSpace(g)));
        public string RatingText => string.IsNullOrWhiteSpace(SelectedMovie?.ImdbRating) ? string.Empty : $"IMDb {SelectedMovie.ImdbRating}";
        public string RuntimeText => NonEmpty(SelectedMovie?.Runtime);
        public double ResumeProgressPercent => _playbackEntry?.ProgressPercent ?? 0;
        public string ResumeProgressText => HasResumeProgress && _playbackEntry != null
            ? $"Resume from {FormatResumeTime(_playbackEntry.PositionSeconds)} · {ResumeProgressPercent:0}% watched"
            : string.Empty;
        public bool HasTrailer => IsHttpUrl(SelectedMovie?.TrailerLink);
        public bool CanMarkWatched => SelectedMovie != null && (!IsSeries || SelectedEpisode != null);
        public string? PosterUrl => NonEmpty(SelectedMovie?.PosterUrl) is { Length: > 0 } poster ? poster : null;
        public string? EpisodesVisibility => Episodes.Count > 0 ? "visible" : null;
        public string SeasonCountText => Seasons.Count == 1 ? "1 season" : $"{Seasons.Count} seasons";
        public string ContinueWatchingEpisodeText => _playbackEntry == null ? string.Empty :
            Episodes.FirstOrDefault(item => string.Equals(item.Id, _playbackEntry.LastEpisodeId, StringComparison.OrdinalIgnoreCase)) is { } episode
                ? $"{episode.EpisodeCode}  ·  {episode.Title}" : "Continue your last episode";
        public string ContinueWatchingTimeText => _playbackEntry != null && _playbackEntry.DurationSeconds > 0
            ? $"{ResumeProgressPercent:0}% watched · {FormatResumeTime(_playbackEntry.PositionSeconds)} / {FormatResumeTime(_playbackEntry.DurationSeconds)} · {FormatResumeTime(Math.Max(0, _playbackEntry.DurationSeconds - _playbackEntry.PositionSeconds))} remaining"
            : ResumeProgressText;
        public bool CanMarkUnwatched => SelectedEpisode?.IsCompleted == true;
        public bool CanResumeSelectedEpisode => SelectedEpisode != null &&
            string.Equals(SelectedEpisode.Id, _playbackEntry?.LastEpisodeId, StringComparison.OrdinalIgnoreCase) &&
            _playbackEntry != null && _playbackEntry.PositionSeconds > 0 &&
            _playbackEntry.DurationSeconds > _playbackEntry.PositionSeconds;
        public bool HasSelectedEpisode => SelectedEpisode != null;
        public bool HasNoSeasonEpisodes => !IsLoadingEpisodes &&
            ((Seasons.Count > 0 && SeasonEpisodes.Count == 0) || (IsSeries && Episodes.Count == 0));
        public string SelectedEpisodeRuntimeText => NonEmpty(SelectedEpisode?.RuntimeText);
        public string SelectedEpisodeCode => SelectedEpisode?.EpisodeCode ?? string.Empty;
        public string SelectedEpisodeTitle => NonEmpty(SelectedEpisode?.Title, "Episode details");
        public string SelectedEpisodeAirDateText => SelectedEpisode?.AirDateText ?? string.Empty;
        public string SelectedEpisodeOverview => NonEmpty(SelectedEpisode?.Overview, "No episode description available.");
        public string ContentTypeText => string.Equals(SelectedMovie?.Type, "series", StringComparison.OrdinalIgnoreCase) ? "SERIES" : "MOVIE";
        public string PrimaryActionText => IsSeries ? "Play From Start" : "Watch Now";
        public Video? ResumeEpisode => Episodes.FirstOrDefault(item =>
            string.Equals(item.Id, _playbackEntry?.LastEpisodeId, StringComparison.OrdinalIgnoreCase))
            ?? Episodes.OrderBy(item => item.Season ?? int.MaxValue).ThenBy(item => item.Episode ?? int.MaxValue).FirstOrDefault();

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
            Streams.CollectionChanged += (_, _) => OnPropertyChanged(nameof(StreamCount));

            WatchNowCommand = new AsyncRelayCommand(WatchNowAsync);
            CancelStreamSearchCommand = new RelayCommand(CancelStreamSearch);
            ToggleLibraryCommand = new AsyncRelayCommand(ToggleLibraryAsync);
            PlayFromStartCommand = new AsyncRelayCommand(PlayFromStartAsync);
            PlayEpisodeCommand = new AsyncRelayCommand<Video?>(PlayEpisodeAsync);
            ResumeWatchingCommand = new AsyncRelayCommand(ResumeWatchingAsync);
            PlayTrailerCommand = new RelayCommand(PlayTrailer);
            MarkWatchedCommand = new AsyncRelayCommand(MarkWatchedAsync);
            MarkUnwatchedCommand = new AsyncRelayCommand(MarkUnwatchedAsync);
            PlayStreamCommand = new AsyncRelayCommand<StreamSource?>(PlayStreamAsync);
        }

        /// <summary>Loads full metadata, library state, and available streams for the selected media item.</summary>
        public async Task InitializeAsync(Movie movie)
        {
            CancelIfActive(_loadCts);
            CancelIfActive(_streamsCts);
            CancelIfActive(_prefetchCts);
            var loadCts = new CancellationTokenSource();
            _loadCts = loadCts;
            var token = loadCts.Token;

            movie.Type ??= "movie";
            IsLoadingMetadata = true;
            OnPropertyChanged(nameof(DescriptionText));
            try
            {
                SelectedMovie = movie;
                IsSeries = string.Equals(movie.Type, "series", StringComparison.OrdinalIgnoreCase);
                IsSavedInLibrary = await _libraryService.IsSavedAsync(movie.Type ?? "movie", movie.Id ?? string.Empty).ConfigureAwait(true);
                var libraryEntries = await _libraryService.GetEntriesAsync().ConfigureAwait(true);
                token.ThrowIfCancellationRequested();
                _playbackEntry = libraryEntries.FirstOrDefault(entry =>
                    string.Equals(entry.Movie?.Id, movie.Id, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.Movie?.Type ?? "movie", movie.Type ?? "movie", StringComparison.OrdinalIgnoreCase));
                IsResumeRequested = false;
                LibraryButtonText = IsSavedInLibrary ? "✓  Remove from Library" : "＋  Add to Library";
                NotifyDetailsProperties();
                Episodes = new ObservableCollection<Video>();
                SeasonEpisodes = new ObservableCollection<Video>();
                Seasons = new ObservableCollection<int>();
                SelectedSeason = -1;
                IsEpisodeDetailsVisible = false;
                IsLoadingSeasons = IsSeries;
                IsLoadingEpisodes = IsSeries;
                Streams.Clear();
                ProviderStatusLines.Clear();
                SelectedEpisode = null;
                OnPropertyChanged(nameof(CanMarkWatched));
                ErrorMessage = null;
                StatusMessage = null;
                UpdateResumeAvailability();
                OnPropertyChanged(nameof(GenreText));
                OnPropertyChanged(nameof(RatingText));
                OnPropertyChanged(nameof(RuntimeText));
                OnPropertyChanged(nameof(ContentTypeText));

                await _addonService.InitializeAsync(token).ConfigureAwait(true);

                // Catalog results are previews. Resolve the full Stremio meta object
                // before presenting the detail view or requesting episode streams.
                IsLoadingMetadata = true;
                var fullMeta = await _metadataService.GetMetaAsync(
                    movie.Type ?? "movie", movie.Id ?? string.Empty, token).ConfigureAwait(true);
                if (fullMeta != null)
                    SelectedMovie = MergePreview(movie, fullMeta);

                IsSeries = string.Equals(SelectedMovie?.Type, "series", StringComparison.OrdinalIgnoreCase);
                OnPropertyChanged(nameof(CanMarkWatched));
                OnPropertyChanged(nameof(HasTrailer));
                NotifyDetailsProperties();

                if (SelectedMovie?.Videos != null)
                {
                    // Keep the season rail responsive: materialize season metadata first,
                    // then populate the selected season on the next UI turn.
                    IsLoadingSeasons = true;
                    foreach (var episode in SelectedMovie.Videos)
                        Episodes.Add(episode);
                    BuildSeasonCollections();
                    IsLoadingSeasons = false;
                    IsLoadingEpisodes = false;
                }
                else if (IsSeries)
                {
                    IsLoadingSeasons = false;
                    IsLoadingEpisodes = false;
                    StatusMessage = "Episode information is not available for this title yet.";
                }
                UpdateResumeAvailability();
                OnPropertyChanged(nameof(EpisodesVisibility));

                if (string.Equals(SelectedMovie?.Type, "series", StringComparison.OrdinalIgnoreCase)
                    && Episodes.Count > 0)
                {
                    // Resolve the first episode's add-on responses while the user
                    // is reading the details page. Selecting it later is a cache hit.
                    var firstEpisodeId = Episodes.FirstOrDefault()?.Id;
                    if (!string.IsNullOrWhiteSpace(firstEpisodeId))
                    {
                        CancelIfActive(_prefetchCts);
                        var prefetchCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                        _prefetchCts = prefetchCts;
                        _ = PrefetchEpisodeStreamsAsync(firstEpisodeId, prefetchCts);
                    }
                    return;
                }

                // Keep the details page responsive while stream providers resolve.
                // The stream rail fills in as soon as the background request completes.
                _ = LoadStreamsAsync(SelectedMovie?.Id ?? movie.Id ?? string.Empty, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ErrorMessage = $"Unable to load title details: {ex.Message}"; }
            finally
            {
                // An older cancelled metadata request must not clear the loading
                // state owned by a newer navigation to another title.
                if (ReferenceEquals(_loadCts, loadCts))
                {
                    IsLoadingMetadata = false;
                    OnPropertyChanged(nameof(DescriptionText));
                    _loadCts = null;
                }
                loadCts.Dispose();
            }
        }

        /// <summary>Cancels outstanding detail, metadata, prefetch, and provider requests when leaving the page.</summary>
        public void CancelPendingRequests()
        {
            // Each asynchronous operation owns and disposes its CTS in its finally block.
            // Detach here so a later navigation cannot mistake a cancelled request for the current one.
            var loadCts = _loadCts;
            var streamsCts = _streamsCts;
            var prefetchCts = _prefetchCts;
            _loadCts = null;
            _streamsCts = null;
            _prefetchCts = null;

            CancelIfActive(loadCts);
            CancelIfActive(streamsCts);
            CancelIfActive(prefetchCts);
            IsLoadingMetadata = false;
            OnPropertyChanged(nameof(DescriptionText));
            IsLoadingStreams = false;
        }
        private async Task WatchNowAsync()
        {
            if (SelectedMovie == null)
                return;

            if (IsSeries)
            {
                await PlayFromStartAsync().ConfigureAwait(true);
                return;
            }

            var contentId = SelectedEpisode?.Id ?? SelectedMovie.Id;
            if (string.IsNullOrWhiteSpace(contentId))
                return;

            if (Streams.Count == 0 && !IsLoadingStreams)
                _ = LoadStreamsAsync(contentId, CancellationToken.None);

            var stream = await WaitForAvailableStreamAsync(CancellationToken.None).ConfigureAwait(true);
            if (stream != null)
            {
                await PlayStreamAsync(stream).ConfigureAwait(true);
                return;
            }

            if (string.IsNullOrWhiteSpace(ErrorMessage))
                ErrorMessage = "No playable streams were found. Check your installed add-ons and try again.";
        }
        private void CancelStreamSearch()
        {
            ErrorMessage = "Stream search cancelled.";
            CancelIfActive(_streamsCts);
        }
        private async Task ToggleLibraryAsync()
        {
            if (SelectedMovie?.Id == null) return;
            if (IsSavedInLibrary)
            {
                await _libraryService.RemoveAsync(SelectedMovie.Type ?? "movie", SelectedMovie.Id).ConfigureAwait(true);
                IsSavedInLibrary = false;
                LibraryButtonText = "＋  Add to Library";
            }
            else
            {
                await _libraryService.SaveAsync(SelectedMovie).ConfigureAwait(true);
                IsSavedInLibrary = true;
                LibraryButtonText = "✓  Remove from Library";
            }
        }
        private async Task PlayFromStartAsync()
        {
            var firstEpisode = Episodes.OrderBy(item => item.Season ?? int.MaxValue)
                .ThenBy(item => item.Episode ?? int.MaxValue).FirstOrDefault();
            if (firstEpisode != null)
                await PlayEpisodeAsync(firstEpisode).ConfigureAwait(true);
            else
                ErrorMessage = "Episode metadata is not available for this series yet. Try again after the title details finish loading.";
        }

        public void SelectSeason(int season)
        {
            if (!Seasons.Contains(season))
                return;

            IsLoadingEpisodes = true;
            OnPropertyChanged(nameof(HasNoSeasonEpisodes));

            // Replace the filtered collection in one operation. GridView can retain
            // a stale ItemsSource when its source collection is cleared/repopulated
            // during the same navigation/layout pass; replacing the property forces
            // a fresh binding notification and gives the virtualizing panel a stable
            // snapshot for the newly selected season.
            var selectedEpisodes = Episodes
                .Where(item => item.Season == season || (!item.Season.HasValue && season == 1))
                .OrderBy(item => item.Episode ?? int.MaxValue)
                .ToList();

            SeasonEpisodes = new ObservableCollection<Video>(selectedEpisodes);
            SelectedSeason = season;
            IsEpisodeDetailsVisible = SelectedEpisode != null;
            IsLoadingEpisodes = false;
            OnPropertyChanged(nameof(HasNoSeasonEpisodes));
        }

        public void SelectAdjacentSeason(int direction)
        {
            if (Seasons.Count == 0 || direction == 0)
                return;
            var index = Seasons.IndexOf(SelectedSeason);
            if (index < 0) index = 0;
            index = Math.Clamp(index + Math.Sign(direction), 0, Seasons.Count - 1);
            SelectSeason(Seasons[index]);
        }

        public void ShowEpisodeDetails(Video? episode)
        {
            if (episode == null) return;
            SelectedEpisode = episode;
            IsEpisodeDetailsVisible = true;
            OnPropertyChanged(nameof(SelectedEpisodeCode));
            OnPropertyChanged(nameof(SelectedEpisodeTitle));
            OnPropertyChanged(nameof(SelectedEpisodeAirDateText));
            OnPropertyChanged(nameof(SelectedEpisodeOverview));
            OnPropertyChanged(nameof(CanMarkWatched));
            OnPropertyChanged(nameof(CanMarkUnwatched));
            OnPropertyChanged(nameof(CanResumeSelectedEpisode));
            OnPropertyChanged(nameof(HasSelectedEpisode));
            OnPropertyChanged(nameof(SelectedEpisodeRuntimeText));
            if (!string.IsNullOrWhiteSpace(episode.Id))
                _ = LoadStreamsAsync(episode.Id, CancellationToken.None);
        }

        private void BuildSeasonCollections()
        {
            Seasons.Clear();
            foreach (var season in Episodes.Where(item => item.Season.HasValue)
                .Select(item => item.Season!.Value).Distinct().OrderBy(value => value))
                Seasons.Add(season);
            if (Seasons.Count == 0 && Episodes.Count > 0) Seasons.Add(1);

            foreach (var episode in Episodes)
            {
                EpisodeWatchProgress? progress = null;
                if (!string.IsNullOrWhiteSpace(episode.Id))
                    _playbackEntry?.EpisodeProgress?.TryGetValue(episode.Id, out progress);
                if (progress == null && string.Equals(episode.Id, _playbackEntry?.LastEpisodeId, StringComparison.OrdinalIgnoreCase)
                    && _playbackEntry != null && _playbackEntry.DurationSeconds > 0)
                    progress = new EpisodeWatchProgress { PositionSeconds = _playbackEntry.PositionSeconds, DurationSeconds = _playbackEntry.DurationSeconds };
                episode.ProgressPercent = progress?.ProgressPercent ?? 0;
                episode.IsCompleted = progress != null && progress.DurationSeconds > 0 && progress.ProgressPercent >= 95;
                episode.ProgressText = episode.IsCompleted ? "✓ Watched" :
                    progress != null && progress.PositionSeconds > 0
                        ? $"Resume from {FormatResumeTime(progress.PositionSeconds)} · {progress.ProgressPercent:0}%"
                        : "○ Unwatched";
            }

            var resumeSeason = Episodes.FirstOrDefault(item =>
                string.Equals(item.Id, _playbackEntry?.LastEpisodeId, StringComparison.OrdinalIgnoreCase))?.Season;
            SelectSeason(resumeSeason.HasValue && Seasons.Contains(resumeSeason.Value) ? resumeSeason.Value : Seasons.FirstOrDefault());
            OnPropertyChanged(nameof(SeasonCountText));
            OnPropertyChanged(nameof(ContinueWatchingEpisodeText));
            OnPropertyChanged(nameof(ContinueWatchingTimeText));
        }
        private Task PlayEpisodeAsync(Video? episode) => PlayEpisodeCoreAsync(episode, resume: false);

        public Task ResumeSelectedEpisodeAsync() => PlayEpisodeCoreAsync(SelectedEpisode, resume: true);

        private async Task PlayEpisodeCoreAsync(Video? episode, bool resume)
        {
            if (episode == null || string.IsNullOrWhiteSpace(episode.Id))
                return;

            IsResumeRequested = resume;
            SelectedEpisode = episode;
            OnPropertyChanged(nameof(CanResumeSelectedEpisode));
            OnPropertyChanged(nameof(CanMarkWatched));
            OnPropertyChanged(nameof(CanMarkUnwatched));
            OnPropertyChanged(nameof(HasSelectedEpisode));
            OnPropertyChanged(nameof(SelectedEpisodeRuntimeText));
            _ = LoadStreamsAsync(episode.Id, CancellationToken.None);

            var stream = await WaitForAvailableStreamAsync(CancellationToken.None).ConfigureAwait(true);
            if (stream != null)
            {
                await PlayStreamAsync(stream).ConfigureAwait(true);
                return;
            }

            if (string.IsNullOrWhiteSpace(ErrorMessage))
                ErrorMessage = "No playable streams were found for this episode. Try another provider.";
        }
        private async Task ResumeWatchingAsync()
        {
            if (!HasResumeProgress || _playbackEntry == null || SelectedMovie == null)
                return;

            IsResumeRequested = true;
            string contentId;

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
                OnPropertyChanged(nameof(CanMarkWatched));
                OnPropertyChanged(nameof(CanMarkUnwatched));
                OnPropertyChanged(nameof(SelectedEpisodeRuntimeText));
                SelectSeason(episode.Season ?? Seasons.FirstOrDefault());
                contentId = episode.Id!;
            }
            else
            {
                SelectedEpisode = null;
                OnPropertyChanged(nameof(CanMarkWatched));
                contentId = SelectedMovie.Id ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(contentId))
            {
                IsResumeRequested = false;
                ErrorMessage = "The saved playback item is missing its content ID.";
                return;
            }

            if (Streams.Count == 0 && !IsLoadingStreams)
                _ = LoadStreamsAsync(contentId, CancellationToken.None);

            var stream = await WaitForAvailableStreamAsync(CancellationToken.None).ConfigureAwait(true);
            if (stream != null)
            {
                await PlayStreamAsync(stream).ConfigureAwait(true);
                return;
            }

            IsResumeRequested = false;
            if (string.IsNullOrWhiteSpace(ErrorMessage))
                ErrorMessage = "No playable streams were found for this title. Your saved resume position is still available.";
        }

        /// <summary>
        /// Waits for the first progressive provider result or for discovery to finish.
        /// The caller can start playback without waiting for the slowest add-on.
        /// </summary>
        private async Task<StreamSource?> WaitForAvailableStreamAsync(CancellationToken cancellationToken)
        {
            if (Streams.Count > 0)
                return SelectPreferredStream(Streams);
            if (!IsLoadingStreams)
                return null;

            var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            NotifyCollectionChangedEventHandler collectionChanged = (_, _) =>
            {
                if (Streams.Count > 0 || !IsLoadingStreams)
                    signal.TrySetResult(true);
            };
            PropertyChangedEventHandler propertyChanged = (_, args) =>
            {
                if (args.PropertyName == nameof(IsLoadingStreams) && !IsLoadingStreams)
                    signal.TrySetResult(true);
            };

            Streams.CollectionChanged += collectionChanged;
            PropertyChanged += propertyChanged;
            using var registration = cancellationToken.Register(
                () => signal.TrySetCanceled(cancellationToken));

            try
            {
                // Recheck after subscribing so a provider result cannot be missed
                // between the initial state check and event registration.
                if (Streams.Count > 0 || !IsLoadingStreams)
                    signal.TrySetResult(true);

                await signal.Task.ConfigureAwait(true);
                return SelectPreferredStream(Streams);
            }
            finally
            {
                Streams.CollectionChanged -= collectionChanged;
                PropertyChanged -= propertyChanged;
            }
        }

        /// <summary>Ranks currently available playable sources without relying on provider response order.</summary>
        private static StreamSource? SelectPreferredStream(System.Collections.Generic.IEnumerable<StreamSource> streams)
        {
            return streams
                .Where(IsViableStream)
                .OrderByDescending(GetStreamScore)
                .FirstOrDefault();
        }

        private static int GetStreamScore(StreamSource stream)
        {
            var score = IsDirectHttpStream(stream) ? 1000 : 500;
            var description = $"{stream.Name} {stream.Title}".ToLowerInvariant();

            if (description.Contains("2160p") || description.Contains("4k"))
                score += 300;
            else if (description.Contains("1080p"))
                score += 200;
            else if (description.Contains("720p"))
                score += 100;

            if (description.Contains("hdr"))
                score += 15;
            if (description.Contains("hevc") || description.Contains("h265"))
                score += 5;

            return score;
        }
        private void PlayTrailer()
        {
            var trailerUrl = SelectedMovie?.TrailerLink;
            if (!IsHttpUrl(trailerUrl) || SelectedMovie == null)
            {
                ErrorMessage = "A playable trailer is not available for this title.";
                return;
            }

            var request = new PlaybackRequest(
                trailerUrl!,
                $"{SelectedMovie.Title ?? "Title"} — Trailer",
                seriesTitle: SelectedMovie.Title);

            if (!_navigationService.NavigateTo(NavigationRoute.Player, request))
                ErrorMessage = "The trailer player could not be opened.";
        }
        private async Task MarkWatchedAsync()
        {
            if (SelectedMovie == null || string.IsNullOrWhiteSpace(SelectedMovie.Id))
                return;

            if (IsSeries && SelectedEpisode == null)
            {
                ErrorMessage = "Select an episode before marking it as watched.";
                return;
            }

            var duration = 0d;
            if (IsSeries && !string.IsNullOrWhiteSpace(SelectedEpisode?.Id))
            {
                if (_playbackEntry?.EpisodeProgress?.TryGetValue(SelectedEpisode.Id, out var episodeProgress) == true)
                    duration = episodeProgress.DurationSeconds;
                if (duration <= 0)
                    duration = ParseRuntimeSeconds(SelectedEpisode.Runtime);
            }
            else if (!IsSeries)
                duration = ParseRuntimeSeconds(SelectedMovie.Runtime);
            if (duration <= 0 && !IsSeries && _playbackEntry?.DurationSeconds > 0)
                duration = _playbackEntry.DurationSeconds;

            if (duration <= 0)
            {
                ErrorMessage = "The runtime is unavailable, so this title cannot be marked watched yet.";
                return;
            }

            var episodeId = SelectedEpisode?.Id;
            try
            {
                await _libraryService.UpdateProgressAsync(
                    SelectedMovie.Type ?? "movie",
                    SelectedMovie.Id,
                    duration,
                    duration,
                    episodeId,
                    SelectedMovie).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Couldn't update watch history: {ex.Message}";
                return;
            }

            _playbackEntry ??= new LibraryEntry
            {
                Movie = SelectedMovie,
                IsSaved = false,
                AddedUtc = DateTimeOffset.UtcNow
            };
            _playbackEntry.PositionSeconds = duration;
            _playbackEntry.DurationSeconds = duration;
            _playbackEntry.LastEpisodeId = episodeId;
            _playbackEntry.LastPlayedUtc = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(episodeId))
            {
                _playbackEntry.EpisodeProgress ??= new System.Collections.Generic.Dictionary<string, EpisodeWatchProgress>(StringComparer.OrdinalIgnoreCase);
                _playbackEntry.EpisodeProgress[episodeId] = new EpisodeWatchProgress
                {
                    PositionSeconds = duration,
                    DurationSeconds = duration,
                    LastPlayedUtc = _playbackEntry.LastPlayedUtc
                };
                if (SelectedEpisode != null)
                {
                    SelectedEpisode.ProgressPercent = 100;
                    SelectedEpisode.ProgressText = "Watched";
                    SelectedEpisode.IsCompleted = true;
                    var currentSeason = SelectedSeason;
                    SelectSeason(currentSeason);
                }
            }
            UpdateResumeAvailability();
            OnPropertyChanged(nameof(CanMarkUnwatched));
            OnPropertyChanged(nameof(ContinueWatchingTimeText));
            ErrorMessage = null;
            StatusMessage = "Marked as watched.";
        }

        private async Task MarkUnwatchedAsync()
        {
            if (SelectedMovie == null || string.IsNullOrWhiteSpace(SelectedMovie.Id) ||
                SelectedEpisode == null || string.IsNullOrWhiteSpace(SelectedEpisode.Id))
                return;

            var episode = SelectedEpisode;
            var duration = ParseRuntimeSeconds(episode.Runtime);
            if (_playbackEntry?.EpisodeProgress?.TryGetValue(episode.Id, out var saved) == true)
                duration = Math.Max(duration, saved.DurationSeconds);
            if (duration <= 0)
            {
                ErrorMessage = "The episode runtime is unavailable, so watch history cannot be updated.";
                return;
            }

            try
            {
                await _libraryService.UpdateProgressAsync(
                    SelectedMovie.Type ?? "series",
                    SelectedMovie.Id,
                    0,
                    duration,
                    episode.Id,
                    SelectedMovie).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Couldn't update watch history: {ex.Message}";
                return;
            }

            _playbackEntry ??= new LibraryEntry
            {
                Movie = SelectedMovie,
                IsSaved = false,
                AddedUtc = DateTimeOffset.UtcNow
            };
            _playbackEntry.EpisodeProgress ??= new System.Collections.Generic.Dictionary<string, EpisodeWatchProgress>(StringComparer.OrdinalIgnoreCase);
            _playbackEntry.EpisodeProgress[episode.Id] = new EpisodeWatchProgress
            {
                PositionSeconds = 0,
                DurationSeconds = duration,
                LastPlayedUtc = DateTimeOffset.UtcNow
            };
            episode.ProgressPercent = 0;
            episode.ProgressText = "Unwatched";
            episode.IsCompleted = false;
            if (string.Equals(_playbackEntry.LastEpisodeId, episode.Id, StringComparison.OrdinalIgnoreCase))
            {
                _playbackEntry.PositionSeconds = 0;
                _playbackEntry.DurationSeconds = duration;
            }
            SelectSeason(SelectedSeason);
            UpdateResumeAvailability();
            OnPropertyChanged(nameof(CanMarkUnwatched));
            OnPropertyChanged(nameof(ContinueWatchingTimeText));
            ErrorMessage = null;
            StatusMessage = "Marked as unwatched.";
        }

        public void SetActionMessage(string message) => ErrorMessage = message;

        private static bool IsHttpUrl(string? value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        private static double ParseRuntimeSeconds(string? runtime)
        {
            if (string.IsNullOrWhiteSpace(runtime))
                return 0;

            if (runtime.Contains(':') && TimeSpan.TryParse(runtime, out var time))
                return Math.Max(0, time.TotalSeconds);

            var match = Regex.Match(
                runtime,
                @"(?:(?<hours>\d+)\s*h(?:ours?)?)?\s*(?:(?<minutes>\d+)\s*m(?:in(?:utes?)?)?)?",
                RegexOptions.IgnoreCase);

            if (match.Success &&
                (match.Groups["hours"].Success || match.Groups["minutes"].Success))
            {
                var hours = match.Groups["hours"].Success
                    ? int.Parse(match.Groups["hours"].Value)
                    : 0;
                var minutes = match.Groups["minutes"].Success
                    ? int.Parse(match.Groups["minutes"].Value)
                    : 0;
                return TimeSpan.FromHours(hours).TotalSeconds +
                    TimeSpan.FromMinutes(minutes).TotalSeconds;
            }

            return int.TryParse(runtime.Trim(), out var minutesOnly)
                ? TimeSpan.FromMinutes(minutesOnly).TotalSeconds
                : 0;
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
            var resumeEpisode = Episodes.FirstOrDefault(item =>
                string.Equals(item.Id, _playbackEntry?.LastEpisodeId, StringComparison.OrdinalIgnoreCase));
            ResumeButtonText = HasResumeProgress
                ? IsSeries && resumeEpisode != null
                    ? $"Resume {resumeEpisode.EpisodeCode} · {FormatResumeTime(_playbackEntry!.PositionSeconds)}"
                    : $"Resume Watching · {FormatResumeTime(_playbackEntry!.PositionSeconds)}"
                : "Resume Watching";
            OnPropertyChanged(nameof(ResumeProgressPercent));
            OnPropertyChanged(nameof(ResumeProgressText));
        }

        private static string FormatResumeTime(double seconds)
        {
            var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
        }

        /// <summary>Queries compatible add-ons for streams and filters out unusable playback sources.</summary>
        private async Task LoadStreamsAsync(string contentId, CancellationToken cancellationToken)
        {
            if (SelectedMovie == null || string.IsNullOrWhiteSpace(contentId)) return;

            CancelIfActive(_streamsCts);
            var requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _streamsCts = requestCts;
            var token = requestCts.Token;

            IsLoadingStreams = true;
            ErrorMessage = null;
            Streams.Clear();
            ProviderStatusLines.Clear();
            try
            {
                var fetched = await _streamService.GetFromInstalledAddonsAsync(
                    SelectedMovie.Type ?? "movie",
                    contentId,
                    token,
                    async providerStreams =>
                    {
                        var ordered = providerStreams
                            .Where(IsViableStream)
                            .OrderByDescending(IsDirectHttpStream)
                            .ToArray();
                        if (ordered.Length == 0)
                            return;

                        // Provider callbacks can arrive on background threads. Marshal
                        // each batch to the UI immediately instead of waiting for all add-ons.
                        await Windows.ApplicationModel.Core.CoreApplication.MainView
                            .CoreWindow.Dispatcher.RunAsync(
                                Windows.UI.Core.CoreDispatcherPriority.Normal,
                                () =>
                                {
                                    if (token.IsCancellationRequested)
                                        return;
                                    foreach (var stream in ordered)
                                    {
                                        var key = StreamSourceIdentity.GetKey(stream);
                                        if (key == null || !Streams.Any(existing =>
                                            string.Equals(
                                                StreamSourceIdentity.GetKey(existing),
                                                key,
                                                StringComparison.OrdinalIgnoreCase)))
                                        {
                                            Streams.Add(stream);
                                        }
                                    }
                                });
                        // Do not start torrent engines during discovery. Torrent setup is deferred until playback.
                    },
                    async (providerName, state) =>
                    {
                        await Windows.ApplicationModel.Core.CoreApplication.MainView
                            .CoreWindow.Dispatcher.RunAsync(
                                Windows.UI.Core.CoreDispatcherPriority.Normal,
                                () =>
                                {
                                    if (token.IsCancellationRequested)
                                        return;
                                    var prefix = providerName + " — ";
                                    for (var index = ProviderStatusLines.Count - 1; index >= 0; index--)
                                    {
                                        if (ProviderStatusLines[index].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                            ProviderStatusLines.RemoveAt(index);
                                    }
                                    ProviderStatusLines.Add(prefix + state);
                                });
                    }).ConfigureAwait(true);

                // If no provider published a viable stream, retain a useful empty-state message.
                if (Streams.Count == 0)
                    ErrorMessage = fetched.Count == 0
                        ? "No streams found for this selection. Install a stream add-on that supports this title."
                        : "Add-ons returned no playable stream for this selection.";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ErrorMessage = $"Failed to load streams: {ex.Message}"; }
            finally
            {
                // A cancelled earlier lookup must not hide the loading state of a newer request.
                if (ReferenceEquals(_streamsCts, requestCts))
                {
                    IsLoadingStreams = false;
                    _streamsCts = null;
                }
                requestCts.Dispose();
            }
        }

        private static bool IsDirectHttpStream(StreamSource stream) =>
            Uri.TryCreate(stream.Url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        private static bool IsViableStream(StreamSource stream) =>
            IsDirectHttpStream(stream) ||
            !string.IsNullOrWhiteSpace(stream.InfoHash) ||
            (Uri.TryCreate(stream.Url, UriKind.Absolute, out var uri) &&
             string.Equals(uri.Scheme, "magnet", StringComparison.OrdinalIgnoreCase));

        /// <summary>Preloads episode stream results so selecting the next episode feels more responsive.</summary>
        private async Task PrefetchEpisodeStreamsAsync(string episodeId, CancellationTokenSource prefetchCts)
        {
            try
            {
                var type = SelectedMovie?.Type ?? "series";
                var streams = await _streamService.GetFromInstalledAddonsAsync(
                    type,
                    episodeId,
                    prefetchCts.Token)
                    .ConfigureAwait(false);
                PlaybackDiagnostics.Write(
                    $"[Prefetch] Cached {streams.Count} stream result(s) for episode {episodeId}.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                PlaybackDiagnostics.Write($"[Prefetch] Episode stream prefetch failed: {ex.Message}");
            }
            finally
            {
                if (ReferenceEquals(_prefetchCts, prefetchCts))
                    _prefetchCts = null;
                prefetchCts.Dispose();
            }
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
            OnPropertyChanged(nameof(PrimaryActionText));
        }

        private static string? Prefer(string? primary, string? fallback) =>
            string.IsNullOrWhiteSpace(primary) ? fallback : primary;

        /// <summary>Combines lightweight catalog data with full metadata while retaining useful preview fields.</summary>
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
            full.Description = Prefer(full.Description,
                Prefer(full.Overview, Prefer(full.Plot,
                    Prefer(preview.Description, Prefer(preview.Overview, preview.Plot)))));
            full.TrailerUrl = Prefer(full.TrailerLink, preview.TrailerLink);
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
            var title = IsSeries && SelectedEpisode != null
                ? $"{SelectedEpisode.EpisodeCode} · {SelectedEpisode.Title}"
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
        /// <summary>Validates the selected source and prepares the player navigation request.</summary>
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
