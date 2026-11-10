using System;
using System.Collections.Specialized;
using StreamioX.App.Models;
using StreamioX.App.Helpers;
using StreamioX.App.ViewModels;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StreamioX.App.Views
{
    public sealed partial class DetailsPage : Page
    {
        public DetailsViewModel ViewModel { get; }
        private bool _movieStreamFocusApplied;

        public DetailsPage()
        {
            InitializeComponent();
            FocusHelper.AttachXboxFocus(LibraryButton);
            ViewModel = AppServices.GetRequiredService<DetailsViewModel>();
            DataContext = ViewModel;
            ViewModel.Episodes.CollectionChanged += ContentRows_CollectionChanged;
            ViewModel.Streams.CollectionChanged += ContentRows_CollectionChanged;
        }

        private void ContentRows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RenderContentRows();
        }

        private void RenderContentRows()
        {
            // Episodes are rendered by the horizontal ItemsRepeater bound to
            // ViewModel.Episodes; only stream options need imperative construction.
            StreamsRow.Children.Clear();
            foreach (var stream in ViewModel.Streams)
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
                    Content = details, Tag = stream, HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(16, 12, 16, 12),
                    Background = new SolidColorBrush(Color.FromArgb(255, 22, 27, 37)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 57, 69, 83)),
                    BorderThickness = new Thickness(1), Margin = new Thickness(0, 2, 0, 2),
                    UseSystemFocusVisuals = true,
                    Style = Application.Current.Resources["XboxFocusCardStyle"] as Style
                };
                FocusHelper.AttachXboxFocus(button);
                button.GotFocus += StreamButton_GotFocus;
                button.Click += StreamButton_Click;
                StreamsRow.Children.Add(button);
            }

            // Movie pages have no episode rail, so move controller focus directly
            // to the first playable source as soon as stream results arrive.
            if (!ViewModel.IsSeries && !_movieStreamFocusApplied && StreamsRow.Children.Count > 0)
            {
                _movieStreamFocusApplied = true;
                (StreamsRow.Children[0] as Control)?.Focus(Windows.UI.Xaml.FocusState.Programmatic);
            }
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

        private void EpisodeButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Video episode)
                ViewModel.PlayEpisodeCommand.Execute(episode);
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

        private void StreamButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is StreamSource stream)
                ViewModel.PlayStreamCommand.Execute(stream);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _movieStreamFocusApplied = false;

            if (e.Parameter is Movie selectedMovie)
            {
                await ViewModel.InitializeAsync(selectedMovie);
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
            }
        }
    }
}
