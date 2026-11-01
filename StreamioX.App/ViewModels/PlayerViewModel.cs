using CommunityToolkit.Mvvm.ComponentModel;

namespace StreamioX.App.ViewModels
{
    public partial class PlayerViewModel : ObservableObject
    {
        [ObservableProperty] private string mediaTitle = "Now Playing";
        [ObservableProperty] private string? currentStreamUrl;
        [ObservableProperty] private bool isPlaying;
        [ObservableProperty] private string? playbackError;

        public void LoadStream(string streamUrl, string title)
        {
            CurrentStreamUrl = streamUrl;
            MediaTitle = title;
            IsPlaying = false;
            PlaybackError = null;
        }

        public void SetPlaybackError(string message)
        {
            PlaybackError = message;
            IsPlaying = false;
        }

        public void SetPlaying(bool playing) => IsPlaying = playing;
    }
}
