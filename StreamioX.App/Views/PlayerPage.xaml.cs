using StreamioX.App.ViewModels;
using System;
using Windows.Media.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StreamioX.App.Views
{
    /// <summary>
    /// Hosts the native video playback session for StremioX.App on Xbox.
    /// </summary>
    public sealed partial class PlayerPage : Page
    {
        public PlayerViewModel ViewModel { get; }

        public PlayerPage()
        {
            this.InitializeComponent();
            ViewModel = new PlayerViewModel();
            this.DataContext = ViewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Expecting a stream URL string passed as a navigation parameter from DetailsPage or direct route
            if (e.Parameter is string streamUrl && !string.IsNullOrWhiteSpace(streamUrl))
            {
                ViewModel.LoadStream(streamUrl, "Stremio Stream");

                // Initialize the native media player with the stream source and trigger playback
                PlayerWidget.Source = MediaSource.CreateFromUri(new Uri(streamUrl));
                PlayerWidget.MediaPlayer.Play();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // Clean up player resources when leaving the page
            if (PlayerWidget.MediaPlayer != null)
            {
                PlayerWidget.MediaPlayer.Pause();
                PlayerWidget.Source = null;
            }
        }
    }
}