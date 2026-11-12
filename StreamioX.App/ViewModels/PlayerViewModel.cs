// Purpose: Owns PlayerViewModel screen state and user actions, keeping presentation logic separate from the XAML view.

using CommunityToolkit.Mvvm.ComponentModel;
using System;

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
        [ObservableProperty] private string remainingTime = "-00:00";
        [ObservableProperty] private string currentTimeDisplay = "00:00 / 00:00";
        [ObservableProperty] private double playbackProgress;

        /// <summary>Sets the selected stream and display metadata before playback begins.</summary>
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

        /// <summary>Updates elapsed and total playback time for the player progress display.</summary>
        public void UpdatePlaybackTime(TimeSpan current, TimeSpan total)
        {
            CurrentTime = FormatTime(current);
            TotalTime = FormatTime(total);
            var remaining = TimeSpan.FromSeconds(Math.Max(0, total.TotalSeconds - current.TotalSeconds));
            RemainingTime = $"-{FormatTime(remaining)}";
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
