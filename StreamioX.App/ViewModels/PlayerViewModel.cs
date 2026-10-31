// ViewModels/PlayerViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace StreamioX.App.ViewModels
{
    /// <summary>
    /// Manages playback state, stream URL resolution, and media transport controls 
    /// for the native UWP MediaPlayerElement.
    /// </summary>
    public partial class PlayerViewModel : ObservableObject
    {
        [ObservableProperty]
        private string mediaTitle = "Now Playing";

        [ObservableProperty]
        private string currentStreamUrl;

        [ObservableProperty]
        private bool isPlaying = true;

        /// <summary>
        /// Initializes the PlayerViewModel with an optional media stream URL or identifier.
        /// </summary>
        /// <param name="streamUrl">The direct HTTP, HLS, or DASH stream URL.</param>
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