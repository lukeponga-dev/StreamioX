// Purpose: Connects the DetailsPage.xaml page to its view model, navigation, focus behavior, and page lifecycle.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Specialized;
using StreamioX.App.Models;
using StreamioX.App.Helpers;
using StreamioX.App.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StreamioX.App.Views
{
    public sealed partial class DetailsPage : Page
    {
        public DetailsViewModel ViewModel { get; }
        private readonly HashSet<Button> _episodeFocusHooks = new();

        public DetailsPage()
        {
            InitializeComponent();
            FocusHelper.AttachXboxFocus(WatchNowButton);
            FocusHelper.AttachXboxFocus(TrailerButton);
            FocusHelper.AttachXboxFocus(LibraryButton);
            FocusHelper.AttachXboxFocus(MarkWatchedButton);
            FocusHelper.AttachXboxFocus(ShareButton);
            FocusHelper.AttachXboxFocus(ResumeButton);
            ViewModel = AppServices.GetRequiredService<DetailsViewModel>();
            DataContext = ViewModel;
        }

        private void ContentRows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Episode cards are data-bound. Append stream cards as each provider
            // responds instead of rebuilding every existing button on every Add.
            if (ReferenceEquals(sender, ViewModel.Streams) &&
                e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (StreamSource stream in e.NewItems)
                    AddStreamButton(stream);
                return;
            }

            if (ReferenceEquals(sender, ViewModel.Streams))
                RenderContentRows();
        }

        private void RenderContentRows()
        {
            StreamsRow.Children.Clear();
            foreach (var stream in ViewModel.Streams)
                AddStreamButton(stream);
        }

        private void AddStreamButton(StreamSource stream)
        {
            var name = string.IsNullOrWhiteSpace(stream.Name) ? "Stream source" : stream.Name;
            var details = new StackPanel { Spacing = 4 };
            details.Children.Add(new TextBlock
            {
                Text = name, Foreground = new SolidColorBrush(Colors.White),
                FontSize = 16, FontWeight = Windows.UI.Text.FontWeights.Bold
            });
            if (!string.IsNullOrWhiteSpace(stream.Title))
                details.Children.Add(new TextBlock
                {
                    Text = stream.Title, Foreground = new SolidColorBrush(Color.FromArgb(255, 185, 194, 201)),
                    FontSize = 14, TextWrapping = TextWrapping.Wrap
                });
            details.Children.Add(new TextBlock
            {
                Text = stream.SourceKind, Foreground = new SolidColorBrush(Color.FromArgb(255, 47, 220, 128)),
                FontSize = 12, FontWeight = Windows.UI.Text.FontWeights.SemiBold
            });

            var button = new Button
            {
                Content = details,
                Tag = stream,
                MinHeight = 76,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(16, 12, 16, 12),
                Background = new SolidColorBrush(Color.FromArgb(255, 22, 27, 37)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 57, 69, 83)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 2, 0, 2),
                UseSystemFocusVisuals = true,
                Style = Application.Current.Resources["SecondaryButtonStyle"] as Style
            };
            FocusHelper.AttachXboxFocus(button);
            button.GotFocus += StreamButton_GotFocus;
            button.Click += StreamButton_Click;
            StreamsRow.Children.Add(button);
        }

        private void PosterImage_ImageOpened(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            PosterFallback.Visibility = Visibility.Collapsed;
        }

        private void Artwork_ImageFailed(object sender, Windows.UI.Xaml.ExceptionRoutedEventArgs e)
        {
            if (sender is Image image)
            {
                image.Source = null;
                if (ReferenceEquals(image, PosterImage))
                    PosterFallback.Visibility = Visibility.Visible;
            }
        }

        private void ShareButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                var manager = DataTransferManager.GetForCurrentView();
                manager.DataRequested -= ShareDataRequested;
                manager.DataRequested += ShareDataRequested;
                DataTransferManager.ShowShareUI();
            }
            catch (Exception ex)
            {
                ViewModel.SetActionMessage($"Sharing is unavailable on this device: {ex.Message}");
            }
        }

        private void ShareDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            sender.DataRequested -= ShareDataRequested;
            var request = args.Request;
            request.Data.Properties.Title = ViewModel.TitleText;
            request.Data.SetText(
                $"{ViewModel.TitleText} ({ViewModel.ContentTypeText})\n" +
                $"{ViewModel.DescriptionText}\n" +
                $"Stremio ID: {ViewModel.SelectedMovie?.Id}");
        }

        private async void ResumeButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            await ViewModel.ResumeWatchingCommand.ExecuteAsync(null);
        }

        private void EpisodeButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Video episode)
                ViewModel.ShowEpisodeDetails(episode);
        }

        private void EpisodeCard_GotFocus(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not Video episode)
                return;

            ViewModel.ShowEpisodeDetails(episode);
            AnimateEpisodeScale(button, 1.05);
            button.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 123, 92, 255));
            button.BorderThickness = new Thickness(3);
            if (_episodeFocusHooks.Add(button))
                button.LostFocus += EpisodeCard_LostFocus;
        }

        private void EpisodeCard_LostFocus(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;
            AnimateEpisodeScale(button, 1.0);
            button.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 58, 64, 84));
            button.BorderThickness = new Thickness(1);
        }

        private static void AnimateEpisodeScale(Button button, double target)
        {
            if (button.RenderTransform is not ScaleTransform scale)
            {
                scale = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
                button.RenderTransform = scale;
                button.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            }

            var storyboard = new Storyboard();
            var horizontal = new DoubleAnimation
            {
                To = target,
                Duration = new Duration(TimeSpan.FromMilliseconds(140)),
                EnableDependentAnimation = true
            };
            var vertical = new DoubleAnimation
            {
                To = target,
                Duration = new Duration(TimeSpan.FromMilliseconds(140)),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(horizontal, scale);
            Storyboard.SetTarget(vertical, scale);
            Storyboard.SetTargetProperty(horizontal, "ScaleX");
            Storyboard.SetTargetProperty(vertical, "ScaleY");
            storyboard.Children.Add(horizontal);
            storyboard.Children.Add(vertical);
            storyboard.Begin();
        }

        private void SeasonButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int season)
            {
                ViewModel.SelectSeason(season);
                HighlightSeason(season);
            }
        }

        private void HighlightSeason(int season)
        {
            foreach (var child in FindButtons(SeasonSelector))
            {
                var selected = child.Tag is int value && value == season;
                child.Background = selected
                    ? new SolidColorBrush(Color.FromArgb(255, 55, 43, 91))
                    : new SolidColorBrush(Color.FromArgb(255, 32, 36, 51));
                child.BorderBrush = selected
                    ? new SolidColorBrush(Color.FromArgb(255, 123, 92, 255))
                    : new SolidColorBrush(Color.FromArgb(255, 69, 64, 95));
                child.BorderThickness = new Thickness(selected ? 2 : 1);
            }
        }

        private static IEnumerable<Button> FindButtons(DependencyObject parent)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Button button)
                    yield return button;
                foreach (var nested in FindButtons(child))
                    yield return nested;
            }
        }

        private void Page_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.GamepadLeftShoulder)
            {
                ViewModel.SelectAdjacentSeason(-1);
                HighlightSeason(ViewModel.SelectedSeason);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.GamepadRightShoulder)
            {
                ViewModel.SelectAdjacentSeason(1);
                HighlightSeason(ViewModel.SelectedSeason);
                e.Handled = true;
            }
        }

        private async void PlaySelectedEpisode_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (ViewModel.SelectedEpisode != null)
                await ViewModel.PlayEpisodeCommand.ExecuteAsync(ViewModel.SelectedEpisode);
        }

        private async void ResumeSelectedEpisode_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            await ViewModel.ResumeSelectedEpisodeAsync();
        }

        private async void StartEpisode_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (ViewModel.ResumeEpisode is { } episode)
                await ViewModel.PlayEpisodeCommand.ExecuteAsync(episode);
        }

        private void CloseEpisodeDetails_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.IsEpisodeDetailsVisible = false;
        }

        private void StreamButton_GotFocus(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is StreamSource stream)
            {
                var lines = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(stream.Name)) lines.Add(stream.Name);
                if (!string.IsNullOrWhiteSpace(stream.Title)) lines.Add(stream.Title);
                lines.Add(stream.SourceKind);
                StreamDetailsText.Text = string.Join(" · ", lines);
                StreamDetailsPanel.Visibility = Windows.UI.Xaml.Visibility.Visible;
            }
        }

        private async void StreamButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is StreamSource stream)
                await ViewModel.PlayStreamCommand.ExecuteAsync(stream);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.Episodes.CollectionChanged -= ContentRows_CollectionChanged;
            ViewModel.Streams.CollectionChanged -= ContentRows_CollectionChanged;
            ViewModel.Episodes.CollectionChanged += ContentRows_CollectionChanged;
            ViewModel.Streams.CollectionChanged += ContentRows_CollectionChanged;

            var navigationRequest = e.Parameter as DetailsNavigationRequest;
            var selectedMovie = navigationRequest?.Movie ?? e.Parameter as Movie;
            if (selectedMovie != null)
            {
                await ViewModel.InitializeAsync(selectedMovie);
                HighlightSeason(ViewModel.SelectedSeason);
                RenderContentRows();
                PosterImage.Source = null;
                BackdropImage.Source = null;
                PosterFallback.Visibility = Visibility.Visible;

                if (!string.IsNullOrWhiteSpace(ViewModel.PosterUrl) &&
                    Uri.TryCreate(ViewModel.PosterUrl, UriKind.Absolute, out var posterUri) &&
                    (posterUri.Scheme == Uri.UriSchemeHttps || posterUri.Scheme == Uri.UriSchemeHttp))
                {
                    PosterImage.Source = new BitmapImage(posterUri);
                }

                // Cinemeta often has no separate backdrop; use the poster as a soft visual fallback.
                var backdropUrl = ViewModel.SelectedMovie?.BackgroundUrl;
                if (string.IsNullOrWhiteSpace(backdropUrl))
                    backdropUrl = ViewModel.PosterUrl;

                if (!string.IsNullOrWhiteSpace(backdropUrl) &&
                    Uri.TryCreate(backdropUrl, UriKind.Absolute, out var backdropUri) &&
                    (backdropUri.Scheme == Uri.UriSchemeHttps || backdropUri.Scheme == Uri.UriSchemeHttp))
                {
                    BackdropImage.Source = new BitmapImage(backdropUri);
                }

                if (navigationRequest?.ResumeAutomatically == true && ViewModel.HasResumeProgress)
                    await ViewModel.ResumeWatchingCommand.ExecuteAsync(null);
                else
                    WatchNowButton.Focus(Windows.UI.Xaml.FocusState.Programmatic);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            try { DataTransferManager.GetForCurrentView().DataRequested -= ShareDataRequested; }
            catch (Exception) { }
            ViewModel.Episodes.CollectionChanged -= ContentRows_CollectionChanged;
            ViewModel.Streams.CollectionChanged -= ContentRows_CollectionChanged;
            ViewModel.CancelPendingRequests();
            base.OnNavigatedFrom(e);
        }
    }
}
