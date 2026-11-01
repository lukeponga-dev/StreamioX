using StreamioX.App.Models;
using StreamioX.App.ViewModels;
using System;
using Windows.Media.Core;
using Windows.System.Display;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StreamioX.App.Views
{
    /// <summary>
    /// Hosts the native video playback session for StreamioX.App on Xbox[cite: 1, 3].
    /// </summary>
    public sealed partial class PlayerPage : Page
    {
        private DisplayRequest _displayRequest;
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

            // Keep Xbox screen active during playback
            if (_displayRequest == null)
            {
                _displayRequest = new DisplayRequest();
                _displayRequest.RequestActive();
            }

            string targetStreamUrl = null;
            string targetMediaTitle = "Stremio Stream";

            if (e.Parameter is string urlStr)
            {
                targetStreamUrl = urlStr;
            }
            else if (e.Parameter is StreamSource streamObj)
            {
                targetStreamUrl = streamObj.Url;
                targetMediaTitle = !string.IsNullOrWhiteSpace(streamObj.Name) ? streamObj.Name : streamObj.Title;
            }

            if (!string.IsNullOrWhiteSpace(targetStreamUrl))
            {
                ViewModel.LoadStream(targetStreamUrl, targetMediaTitle);

                // Assign MediaSource and initiate hardware-accelerated playback on Xbox
                PlayerWidget.Source = MediaSource.CreateFromUri(new Uri(targetStreamUrl));
                PlayerWidget.MediaPlayer.Play();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // Release display lock[cite: 3]
            if (_displayRequest != null)
            {
                _displayRequest.RequestRelease();
                _displayRequest = null;
            }

            // Clean up player resources[cite: 1]
            if (PlayerWidget.MediaPlayer != null)
            {
                PlayerWidget.MediaPlayer.Pause();
                PlayerWidget.Source = null;
            }
        }
    }
}