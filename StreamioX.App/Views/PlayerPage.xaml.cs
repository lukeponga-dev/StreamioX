using StreamioX.App.ViewModels;
using System;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StreamioX.App.Views
{
    public sealed partial class PlayerPage : Page
    {
        public PlayerViewModel ViewModel { get; }
        private readonly MediaPlayer _mediaPlayer;

        public PlayerPage()
        {
            InitializeComponent();
            ViewModel = AppServices.GetRequiredService<PlayerViewModel>();
            DataContext = ViewModel;

            _mediaPlayer = new MediaPlayer();
            PlayerWidget.SetMediaPlayer(_mediaPlayer);
            _mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
            _mediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is not string streamUrl || string.IsNullOrWhiteSpace(streamUrl))
            {
                ViewModel.SetPlaybackError("No playable stream URL was provided.");
                return;
            }

            try
            {
                if (!Uri.TryCreate(streamUrl, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    ViewModel.SetPlaybackError("This stream is not a direct HTTP/HTTPS media URL. Torrent and external-player streams need a compatible playback bridge.");
                    return;
                }

                ViewModel.LoadStream(streamUrl, "Now Playing");
                _mediaPlayer.Source = MediaSource.CreateFromUri(uri);
                _mediaPlayer.Play();
            }
            catch (Exception ex)
            {
                ViewModel.SetPlaybackError($"Unable to start playback: {ex.Message}");
            }
        }

        private void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                ViewModel.SetPlaybackError($"Playback failed: {args.ErrorMessage}"));
        }

        private void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                ViewModel.SetPlaying(sender.PlaybackState == MediaPlaybackState.Playing));
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _mediaPlayer.Pause();
            _mediaPlayer.Source = null;
        }
    }
}
