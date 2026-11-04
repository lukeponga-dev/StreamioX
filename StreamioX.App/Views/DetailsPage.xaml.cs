using System;
using System.Collections.Specialized;
using StreamioX.App.Models;
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

        public DetailsPage()
        {
            InitializeComponent();
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
            EpisodesRow.Children.Clear();
            EpisodesSection.Visibility = ViewModel.Episodes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            foreach (var episode in ViewModel.Episodes)
            {
                var title = string.IsNullOrWhiteSpace(episode.Title) ? "Episode" : episode.Title;
                if (episode.Season.HasValue || episode.Episode.HasValue)
                    title = $"S{episode.Season ?? 1:00} E{episode.Episode ?? 0:00}  ·  {title}";

                var content = new StackPanel { Spacing = 4 };
                content.Children.Add(new TextBlock
                {
                    Text = title, Foreground = new SolidColorBrush(Colors.White),
                    FontSize = 16, FontWeight = Windows.UI.Text.FontWeights.SemiBold
                });
                if (!string.IsNullOrWhiteSpace(episode.Overview))
                    content.Children.Add(new TextBlock
                    {
                        Text = episode.Overview, Foreground = new SolidColorBrush(Color.FromArgb(255, 185, 194, 201)),
                        FontSize = 13, TextWrapping = TextWrapping.Wrap, MaxHeight = 48
                    });
                var button = new Button
                {
                    Content = content, Tag = episode, HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(16, 12, 16, 12),
                    Background = new SolidColorBrush(Color.FromArgb(255, 22, 27, 37)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 57, 69, 83)),
                    BorderThickness = new Thickness(1), Margin = new Thickness(0, 2, 0, 2),
                    UseSystemFocusVisuals = true
                };
                button.Click += EpisodeButton_Click;
                EpisodesRow.Children.Add(button);
            }

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
                    UseSystemFocusVisuals = true
                };
                button.GotFocus += StreamButton_GotFocus;
                button.Click += StreamButton_Click;
                StreamsRow.Children.Add(button);
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

            if (e.Parameter is Movie selectedMovie)
            {
                await ViewModel.InitializeAsync(selectedMovie);
                PosterImage.Source = null;
                if (!string.IsNullOrWhiteSpace(ViewModel.PosterUrl) &&
                    Uri.TryCreate(ViewModel.PosterUrl, UriKind.Absolute, out var posterUri))
                {
                    PosterImage.Source = new BitmapImage(posterUri);
                }
            }
        }
    }
}
