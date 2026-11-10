using CommunityToolkit.Mvvm.ComponentModel;
using StreamioX.App.Models;
using StreamioX.App.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace StreamioX.App.ViewModels
{
    public partial class PlayerViewModel : ObservableObject
    {
        [ObservableProperty] private string mediaTitle = "Now Playing";
        [ObservableProperty] private string seriesTitle = "StreamioX";
        [ObservableProperty] private string? currentStreamUrl;
        [ObservableProperty] private bool isPlaying;
        [ObservableProperty] private string? playbackError;
        [ObservableProperty] private string streamingStatus = "Ready to play";
        [ObservableProperty] private string currentTime = "00:00";
        [ObservableProperty] private string totalTime = "00:00";
        [ObservableProperty] private string currentTimeDisplay = "00:00 / 00:00";
        [ObservableProperty] private double playbackProgress;

        private static readonly TimeSpan MinimumResumePosition = TimeSpan.FromSeconds(30);
        private const double WatchedThreshold = 0.95;

        private readonly ILibraryService _libraryService;
        private Movie? _progressMovie;
        private string? _progressEpisodeId;

        public PlayerViewModel(ILibraryService libraryService) =>
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));

        public void SetProgressTarget(Movie? movie, string? episodeId)
        {
            _progressMovie = string.IsNullOrWhiteSpace(movie?.Id) ? null : movie;
            _progressEpisodeId = episodeId;
        }

        public async Task<TimeSpan?> GetResumePositionAsync()
        {
            var movie = _progressMovie;
            if (movie?.Id == null) return null;
            try
            {
                var type = movie.Type ?? "movie";
                var entry = (await _libraryService.GetEntriesAsync()).FirstOrDefault(x =>
                    string.Equals(x.Movie?.Id, movie.Id, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.Movie?.Type ?? "movie", type, StringComparison.OrdinalIgnoreCase));
                if (entry == null ||
                    !string.Equals(entry.LastEpisodeId, _progressEpisodeId, StringComparison.OrdinalIgnoreCase) ||
                    entry.DurationSeconds <= 0 ||
                    entry.PositionSeconds / entry.DurationSeconds >= WatchedThreshold)
                    return null;

                var position = TimeSpan.FromSeconds(entry.PositionSeconds);
                return position >= MinimumResumePosition ? position : null;
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveProgressAsync(TimeSpan position, TimeSpan duration)
        {
            var movie = _progressMovie;
            if (movie?.Id == null || duration <= TimeSpan.Zero || position <= TimeSpan.Zero) return;
            try
            {
                await _libraryService.UpdateProgressAsync(movie.Type ?? "movie", movie.Id,
                    position.TotalSeconds, duration.TotalSeconds, _progressEpisodeId, movie);
            }
            catch (Exception ex)
            {
                PlaybackDiagnostics.Write($"[PlayerViewModel] Saving watch progress failed: {ex.Message}");
            }
        }

        public void LoadStream(string streamUrl, string title, string? series = null)
        {
            CurrentStreamUrl = streamUrl;
            MediaTitle = title;
            SeriesTitle = series ?? "StreamioX";
            IsPlaying = false;
            PlaybackError = null;
            StreamingStatus = "Initializing stream...";
        }

        public void SetPlaybackError(string message)
        {
            PlaybackError = message;
            IsPlaying = false;
            StreamingStatus = $"Error: {message}";
        }

        public void SetPlaying(bool playing) => IsPlaying = playing;

        public void UpdatePlaybackTime(TimeSpan current, TimeSpan total)
        {
            CurrentTime = FormatTime(current);
            TotalTime = FormatTime(total);
            CurrentTimeDisplay = $"{FormatTime(current)} / {FormatTime(total)}";
            PlaybackProgress = total.TotalSeconds > 0 ? (current.TotalSeconds / total.TotalSeconds) * 100 : 0;
        }

        public void SetStreamingStatus(string status)
        {
            StreamingStatus = status;
        }

        private static string FormatTime(TimeSpan time)
        {
            if (time.TotalHours >= 1)
                return time.ToString(@"hh\:mm\:ss");
            return time.ToString(@"mm\:ss");
        }
    }
}
