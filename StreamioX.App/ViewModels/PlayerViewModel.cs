using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StreamioX.App.ViewModels
{
    public partial class PlayerViewModel : ObservableObject
    {
        [ObservableProperty]
        private string mediaTitle = "Now Playing";

        [ObservableProperty]
        private string? currentStreamUrl;

        [ObservableProperty]
        private bool isPlaying = true;

        public void LoadStream(string streamUrl, string title)
        {
            CurrentStreamUrl = streamUrl;
            MediaTitle = title;
        }

        [RelayCommand]
        private void TogglePlayPause()
        {
            IsPlaying = !IsPlaying;
        }
    }
}
