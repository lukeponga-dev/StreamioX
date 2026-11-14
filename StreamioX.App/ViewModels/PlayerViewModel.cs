// Purpose: Owns PlayerViewModel screen state and user actions, keeping presentation logic separate from the XAML view.

using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace StreamioX.App.ViewModels
{
    public partial class PlayerViewModel : ObservableObject
    {
        [ObservableProperty] public partial string MediaTitle { get; set; } = "Now Playing";
        [ObservableProperty] public partial string SeriesTitle { get; set; } = "StreamioX";
        [ObservableProperty] public partial string? CurrentStreamUrl { get; set; }
        [ObservableProperty] public partial bool IsPlaying { get; set; }
        [ObservableProperty] public partial string? PlaybackError { get; set; }
        [ObservableProperty] public partial string StreamingStatus { get; set; } = "Ready to play";
        [ObservableProperty] public partial string CurrentTime { get; set; } = "00:00";
        [ObservableProperty] public partial string TotalTime { get; set; } = "00:00";
        [ObservableProperty] public partial string RemainingTime { get; set; } = "-00:00";
        [ObservableProperty] public partial string CurrentTimeDisplay { get; set; } = "00:00 / 00:00";
        [ObservableProperty] public partial double PlaybackProgress { get; set; }

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
