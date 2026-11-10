using StreamioX.App.Models;
using StreamioX.App.Helpers;
using StreamioX.App.Services;
using StreamioX.App.ViewModels;
using System;
using System.Threading.Tasks;
using Windows.Media.Playback;
using Windows.Media.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StreamioX.App.Views
{
    public sealed partial class PlayerPage : Page
    {
        public PlayerViewModel ViewModel { get; }
        private readonly MediaPlayer _mediaPlayer;
        private readonly FFmpegPlaybackService _ffmpegService;
        private readonly DispatcherTimer _playbackTimer;
        private readonly DispatcherTimer _osdTimer;
        private MediaPlaybackItem? _playbackItem;
        private bool _updatingProgressSlider;
        private static readonly TimeSpan ProgressSaveInterval = TimeSpan.FromSeconds(15);
        private DateTimeOffset _lastProgressSave = DateTimeOffset.MinValue;
        private TimeSpan? _pendingResumePosition;

        public PlayerPage()
        {
            InitializeComponent();
            ViewModel = AppServices.GetRequiredService<PlayerViewModel>();
            DataContext = ViewModel;
            FocusHelper.AttachXboxFocus(BackButton);
            FocusHelper.AttachXboxFocus(SkipBackButton);
            FocusHelper.AttachXboxFocus(PlayPauseButton);
            FocusHelper.AttachXboxFocus(SkipForwardButton);
            FocusHelper.AttachXboxFocus(ProgressSlider);
            FocusHelper.AttachXboxFocus(PlaybackSpeedBox);
            FocusHelper.AttachXboxFocus(SubtitleButton);
            FocusHelper.AttachXboxFocus(AudioButton);
            _mediaPlayer = new MediaPlayer();
            _ffmpegService = AppServices.GetRequiredService<FFmpegPlaybackService>();
            PlaybackSpeedBox.SelectionChanged += PlaybackSpeed_Changed;
            PlayerWidget.SetMediaPlayer(_mediaPlayer);
            _mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
            _mediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;

            // Setup timer for playback progress updates
            _playbackTimer = new DispatcherTimer();
            _playbackTimer.Interval = TimeSpan.FromMilliseconds(500);
            _playbackTimer.Tick += UpdatePlaybackTime;

            _osdTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _osdTimer.Tick += (_, _) =>
            {
                _osdTimer.Stop();
                SetOsdVisible(false);
            };

            Loaded += PlayerPage_Loaded;
        }

        private void PlayerPage_Loaded(object sender, RoutedEventArgs e)
        {
            _playbackTimer.Start();
            BackButton.Focus(Windows.UI.Xaml.FocusState.Programmatic);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var streamUrl = e.Parameter is PlaybackRequest request
                ? request.StreamUrl
                : e.Parameter as string;
            var title = e.Parameter is PlaybackRequest playbackRequest
                ? playbackRequest.Title
                : "Now Playing";
            var seriesTitle = e.Parameter is PlaybackRequest pbRequest
                ? pbRequest.SeriesTitle
                : null;
            var requestHeaders = e.Parameter is PlaybackRequest headerRequest
                ? headerRequest.RequestHeaders
                : null;

            PlaybackDiagnostics.Write($"[PlayerPage] Navigation received title '{title}'. Stream parameter present: {!string.IsNullOrWhiteSpace(streamUrl)}.");

            if (string.IsNullOrWhiteSpace(streamUrl))
            {
                ViewModel.SetPlaybackError("No playable stream URL was provided.");
                return;
            }

            if (!Uri.TryCreate(streamUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                ViewModel.SetPlaybackError("The selected source did not resolve to a playable HTTP stream.");
                return;
            }

            ShowLoadingOverlay("Preparing player...");
            ShowOsd();
            var progressRequest = e.Parameter as PlaybackRequest;
            ViewModel.SetProgressTarget(progressRequest?.Movie, progressRequest?.EpisodeId);
            _pendingResumePosition = await ViewModel.GetResumePositionAsync();
            await PlayWithFFmpegAsync(streamUrl, title, seriesTitle, requestHeaders);
        }

        private async Task PlayWithFFmpegAsync(string streamUrl, string title, string? seriesTitle = null,
            System.Collections.Generic.IReadOnlyDictionary<string, string>? requestHeaders = null)
        {
            var stage = "Preparing player";
            ShowLoadingOverlay(stage);
            try
            {
                _mediaPlayer.Pause();
                _mediaPlayer.Source = null;
                _ffmpegService.Stop();
                ViewModel.LoadStream(streamUrl, title, seriesTitle);

                // FFmpegInteropX demuxes the source and exposes a MediaPlaybackItem
                // so MediaPlayerElement retains its normal transport controls.
                var playbackItem = await _ffmpegService.CreateMediaPlaybackItemAsync(
                    streamUrl,
                    currentStage =>
                    {
                        stage = currentStage;
                        PlaybackDiagnostics.Write($"[PlayerPage] Stage: {currentStage}");
                        _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            UpdateLoadingStatus(currentStage);
                            ViewModel.SetStreamingStatus($"Loading: {currentStage}");
                        });
                    },
                    requestHeaders);

                stage = "Assigning FFmpeg item to MediaPlayer";
                _playbackItem = playbackItem;
                _mediaPlayer.Source = playbackItem;
                PlaybackDiagnostics.Write("[PlayerPage] MediaPlayer.Source assigned FFmpeg MediaPlaybackItem.");

                stage = "Starting playback";
                PlaybackDiagnostics.Write("[PlayerPage] Calling MediaPlayer.Play().");
                _mediaPlayer.Play();

                // Keep the overlay up until the playback session confirms it is playing.
                ViewModel.SetStreamingStatus("Waiting for playback to start...");
            }
            catch (Exception ex)
            {
                var details = $"FFmpeg playback failed during: {stage}\n" +
                    $"HRESULT: 0x{ex.HResult:X8}\n" +
                    ex.ToString();
                PlaybackDiagnostics.Write(details);
                HideLoadingOverlay();
                ViewModel.SetPlaybackError(details);
            }
        }

        private void SkipBack_Click(object sender, RoutedEventArgs e)
        {
            SeekBy(TimeSpan.FromSeconds(-10));
            ShowOsd();
        }

        private void SkipForward_Click(object sender, RoutedEventArgs e)
        {
            SeekBy(TimeSpan.FromSeconds(10));
            ShowOsd();
        }

        private void SeekBy(TimeSpan offset)
        {
            var session = _mediaPlayer.PlaybackSession;
            if (_mediaPlayer.Source == null || !session.CanSeek)
            {
                ViewModel.SetStreamingStatus("Seeking is not available for this stream.");
                return;
            }

            var target = session.Position + offset;
            if (target < TimeSpan.Zero)
                target = TimeSpan.Zero;
            if (session.NaturalDuration > TimeSpan.Zero && target > session.NaturalDuration)
                target = session.NaturalDuration;

            try
            {
                session.Position = target;
                ViewModel.UpdatePlaybackTime(session.Position, session.NaturalDuration);
                ViewModel.SetStreamingStatus(offset < TimeSpan.Zero
                    ? "Rewound 10 seconds"
                    : "Fast-forwarded 10 seconds");
            }
            catch (Exception ex)
            {
                PlaybackDiagnostics.Write($"[PlayerPage] Seek failed (HRESULT 0x{ex.HResult:X8}): {ex.GetType().Name}");
                ViewModel.SetStreamingStatus("Unable to seek this stream.");
            }
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer.Source == null)
            {
                ViewModel.SetStreamingStatus("Choose a stream before starting playback.");
                ShowOsd();
                return;
            }

            if (_mediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
                _mediaPlayer.Pause();
            else
                _mediaPlayer.Play();

            ShowOsd();
        }

        private void PlaybackSpeed_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (PlaybackSpeedBox?.SelectedItem is ComboBoxItem item &&
                double.TryParse(item.Tag?.ToString(), System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var speed))
            {
                _mediaPlayer.PlaybackSession.PlaybackRate = speed;
            }
        }

        private void UpdatePlaybackTime(object? sender, object e)
        {
            var session = _mediaPlayer.PlaybackSession;
            ViewModel.UpdatePlaybackTime(session.Position, session.NaturalDuration);

            if (session.PlaybackState == MediaPlaybackState.Playing &&
                DateTimeOffset.UtcNow - _lastProgressSave >= ProgressSaveInterval)
            {
                SaveProgress();
            }

            // Update progress slider
            try
            {
                if (session.NaturalDuration.TotalSeconds > 0)
                {
                    _updatingProgressSlider = true;
                    try
                    {
                        ProgressSlider.Value = (session.Position.TotalSeconds / session.NaturalDuration.TotalSeconds) * 100;
                    }
                    finally
                    {
                        _updatingProgressSlider = false;
                    }
                }
            }
            catch { }
        }

        private void ProgressSlider_ValueChanged(
            object sender,
            Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_updatingProgressSlider || _mediaPlayer == null)
                return;

            var session = _mediaPlayer.PlaybackSession;
            var duration = session.NaturalDuration;
            if (_mediaPlayer.Source == null || !session.CanSeek || duration <= TimeSpan.Zero)
            {
                ViewModel.SetStreamingStatus("Seeking is not available for this stream.");
                return;
            }

            var targetSeconds = duration.TotalSeconds * (e.NewValue / 100.0);
            try
            {
                session.Position = TimeSpan.FromSeconds(
                    Math.Max(0, Math.Min(targetSeconds, duration.TotalSeconds)));
                ViewModel.UpdatePlaybackTime(session.Position, duration);
                ShowOsd();
            }
            catch (Exception ex)
            {
                PlaybackDiagnostics.Write($"[PlayerPage] Progress seek failed (HRESULT 0x{ex.HResult:X8}): {ex.GetType().Name}");
                ViewModel.SetStreamingStatus("Unable to seek this stream.");
            }
        }

        private void ShowLoadingOverlay(string status)
        {
            if (LoadingOverlay == null)
                return;

            UpdateLoadingStatus(status);
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        private void UpdateLoadingStatus(string status)
        {
            if (LoadingStatusText != null)
                LoadingStatusText.Text = status;
        }

        private void HideLoadingOverlay()
        {
            if (LoadingOverlay != null)
                LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            var error = $"Playback failed (0x{args.ExtendedErrorCode.HResult:X8}): {args.ErrorMessage}";
            PlaybackDiagnostics.Write($"[Playback] MediaPlayer.MediaFailed: {error}");
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                HideLoadingOverlay();
                ViewModel.SetPlaybackError(error);
            });
        }

        private void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            PlaybackDiagnostics.Write($"[Playback] MediaPlayer state changed: {sender.PlaybackState}; position: {sender.Position}; duration: {sender.NaturalDuration}.");
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var isPlaying = sender.PlaybackState == MediaPlaybackState.Playing;
                ViewModel.SetPlaying(isPlaying);
                if (isPlaying)
                {
                    HideLoadingOverlay();
                    ViewModel.SetStreamingStatus("Playing...");
                    TryResumePlayback(sender);
                }
                else if (sender.PlaybackState == MediaPlaybackState.Paused)
                {
                    SaveProgress();
                }

                // Update play/pause icon
                try
                {
                    if (PlayPauseIcon != null)
                    {
                        PlayPauseIcon.Glyph = sender.PlaybackState == MediaPlaybackState.Playing
                            ? "\uE769"  // Pause icon
                            : "\uE768"; // Play icon
                    }
                }
                catch { }
            });
        }

        private void TryResumePlayback(MediaPlaybackSession session)
        {
            if (_pendingResumePosition is not { } resumeAt) return;
            _pendingResumePosition = null;
            if (!session.CanSeek || session.NaturalDuration <= resumeAt) return;

            try
            {
                session.Position = resumeAt;
                ViewModel.UpdatePlaybackTime(session.Position, session.NaturalDuration);
                ViewModel.SetStreamingStatus($"Resumed from {resumeAt:hh\\:mm\\:ss}");
                PlaybackDiagnostics.Write($"[PlayerPage] Resumed playback at {resumeAt}.");
            }
            catch (Exception ex)
            {
                PlaybackDiagnostics.Write($"[PlayerPage] Resume seek failed: {ex.Message}");
            }
        }

        private void SaveProgress()
        {
            if (_mediaPlayer.Source == null) return;
            var session = _mediaPlayer.PlaybackSession;
            _lastProgressSave = DateTimeOffset.UtcNow;
            _ = ViewModel.SaveProgressAsync(session.Position, session.NaturalDuration);
        }

        private void SetOsdVisible(bool visible)
        {
            var opacity = visible ? 1.0 : 0.0;
            var visibility = visible ? Visibility.Visible : Visibility.Collapsed;

            TopControls.Opacity = opacity;
            CenterControls.Opacity = opacity;
            BottomControls.Opacity = opacity;
            ProgressControls.Opacity = opacity;

            // Collapsed controls cannot receive invisible controller focus.
            TopControls.Visibility = visibility;
            CenterControls.Visibility = visibility;
            BottomControls.Visibility = visibility;
            ProgressControls.Visibility = visibility;

            if (!visible)
                Focus(Windows.UI.Xaml.FocusState.Programmatic);
        }

        private void ShowOsd()
        {
            SetOsdVisible(true);
            _osdTimer.Stop();
            _osdTimer.Start();
        }

        private void ToggleOsd()
        {
            var visible = CenterControls.Opacity < 0.5;
            SetOsdVisible(visible);
            _osdTimer.Stop();
            if (visible)
                _osdTimer.Start();
        }

        private void PlayerPage_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case Windows.System.VirtualKey.GamepadA:
                case Windows.System.VirtualKey.Space:
                    PlayPause_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    ShowOsd();
                    break;

                case Windows.System.VirtualKey.GamepadX:
                    ToggleOsd();
                    e.Handled = true;
                    break;

                case Windows.System.VirtualKey.GamepadB:
                case Windows.System.VirtualKey.Escape:
                    BackButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;

                case Windows.System.VirtualKey.Left:
                    SeekBy(TimeSpan.FromSeconds(-10));
                    e.Handled = true;
                    ShowOsd();
                    break;

                case Windows.System.VirtualKey.Right:
                case Windows.System.VirtualKey.L:
                    SeekBy(TimeSpan.FromSeconds(10));
                    e.Handled = true;
                    ShowOsd();
                    break;

                case Windows.System.VirtualKey.J:
                    SeekBy(TimeSpan.FromSeconds(-10));
                    e.Handled = true;
                    ShowOsd();
                    break;

                case Windows.System.VirtualKey.K:
                    PlayPause_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    ShowOsd();
                    break;
            }
        }

        private void AudioButton_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            if (_playbackItem == null || _playbackItem.AudioTracks.Count == 0)
            {
                flyout.Items.Add(new MenuFlyoutItem { Text = "No alternate audio tracks" , IsEnabled = false });
            }
            else
            {
                for (var index = 0; index < _playbackItem.AudioTracks.Count; index++)
                {
                    var item = new MenuFlyoutItem
                    {
                        Text = $"Audio track {index + 1}",
                        Tag = index
                    };
                    item.Click += AudioTrack_Click;
                    flyout.Items.Add(item);
                }
            }
            flyout.ShowAt(AudioButton);
            ShowOsd();
        }

        private void AudioTrack_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is int index &&
                _playbackItem != null && index >= 0 && index < _playbackItem.AudioTracks.Count)
            {
                _playbackItem.AudioTracks.SelectedIndex = index;
            }
        }

        private void SubtitleButton_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            var off = new MenuFlyoutItem { Text = "Subtitles off", Tag = -1 };
            off.Click += SubtitleTrack_Click;
            flyout.Items.Add(off);

            if (_playbackItem != null)
            {
                for (var index = 0; index < _playbackItem.TimedMetadataTracks.Count; index++)
                {
                    var item = new MenuFlyoutItem
                    {
                        Text = $"Subtitle track {index + 1}",
                        Tag = index
                    };
                    item.Click += SubtitleTrack_Click;
                    flyout.Items.Add(item);
                }
            }

            flyout.ShowAt(SubtitleButton);
            ShowOsd();
        }

        private void SubtitleTrack_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem item || item.Tag is not int index || _playbackItem == null)
                return;

            // UWP timed metadata tracks are selected through presentation modes.
            // Disable every track first, then enable only the chosen subtitle track.
            for (uint trackIndex = 0; trackIndex < _playbackItem.TimedMetadataTracks.Count; trackIndex++)
            {
                _playbackItem.TimedMetadataTracks.SetPresentationMode(
                    trackIndex,
                    TimedMetadataTrackPresentationMode.Disabled);
            }

            if (index >= 0 && index < _playbackItem.TimedMetadataTracks.Count)
            {
                _playbackItem.TimedMetadataTracks.SetPresentationMode(
                    (uint)index,
                    TimedMetadataTrackPresentationMode.PlatformPresented);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            SaveProgress();
            _playbackTimer?.Stop();
            _osdTimer?.Stop();
            _mediaPlayer.Pause();
            _mediaPlayer.Source = null;
            _playbackItem = null;
            _ffmpegService.Stop();
            if (Frame.CanGoBack)
                Frame.GoBack();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            SaveProgress();
            _pendingResumePosition = null;
            _playbackTimer?.Stop();
            _osdTimer?.Stop();
            _mediaPlayer.Pause();
            _mediaPlayer.Source = null;
            _playbackItem = null;
            _ffmpegService.Stop();
        }
    }
}
