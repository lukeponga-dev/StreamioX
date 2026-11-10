using System;
using System.Collections.Generic;
using StreamioX.App.Models;
using StreamioX.App.ViewModels;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace StreamioX.App.Views
{
    public sealed partial class HomePage : Page
    {
        public HomeViewModel ViewModel { get; }

        public HomePage()
        {
            InitializeComponent();
            ViewModel = AppServices.GetRequiredService<HomeViewModel>();
            DataContext = ViewModel;
            ViewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(HomeViewModel.StatusMessage) ||
                    args.PropertyName == nameof(HomeViewModel.ErrorMessage) ||
                    args.PropertyName == nameof(HomeViewModel.IsLoading) ||
                    args.PropertyName == nameof(HomeViewModel.FeaturedMovie) ||
                    args.PropertyName == nameof(HomeViewModel.HasCatalogContent))
                    RenderHome();
            };
            Loaded += HomePage_Loaded;
        }

        private async void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();
            RenderHome();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();
            RenderHome();
        }

        private void RenderHome()
        {
            if (PopularMoviesRow == null) return;

            LoadingRing.IsActive = ViewModel.IsLoading;
            LoadingRing.Visibility = ViewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = ViewModel.StatusMessage;
            ErrorText.Text = ViewModel.ErrorMessage ?? string.Empty;

            RenderHero(ViewModel.FeaturedMovie);

            ContinueRow.Children.Clear();
            foreach (var entry in ViewModel.ContinueWatching)
                ContinueRow.Children.Add(CreateContinueCard(entry));
            ContinueSection.Visibility = ViewModel.ContinueWatching.Count > 0
                ? Visibility.Visible : Visibility.Collapsed;

            RenderPosterRow(PopularMoviesRow, ViewModel.PopularMovies);
            RenderPosterRow(PopularSeriesRow, ViewModel.PopularSeries);
            RenderPosterRow(RecommendedRow, ViewModel.Recommended);

            PopularMoviesSection.Visibility = ViewModel.PopularMovies.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            PopularSeriesSection.Visibility = ViewModel.PopularSeries.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            RecommendedSection.Visibility = ViewModel.Recommended.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyState.Visibility = !ViewModel.IsLoading && !ViewModel.HasCatalogContent
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RenderHero(Movie? movie)
        {
            HeroTitle.Text = movie?.Title ?? (ViewModel.IsLoading ? "Finding your next favourite…" : "Discover something great");
            HeroMetadata.Text = movie == null ? string.Empty : BuildMetadata(movie);
            HeroDescription.Text = movie?.Description ?? string.Empty;
            HeroWatchButton.IsEnabled = movie != null;

            HeroBackdrop.Source = null;
            var artwork = movie?.BackgroundUrl;
            if (string.IsNullOrWhiteSpace(artwork)) artwork = movie?.PosterUrl;
            if (TryCreateBitmap(artwork, out var bitmap))
                HeroBackdrop.Source = bitmap;
        }

        private static string BuildMetadata(Movie movie)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(movie.ReleaseInfo)) parts.Add(movie.ReleaseInfo);
            if (!string.IsNullOrWhiteSpace(movie.ImdbRating)) parts.Add("IMDb " + movie.ImdbRating);
            if (movie.Genres != null && movie.Genres.Count > 0) parts.Add(string.Join(" · ", movie.Genres));
            return string.Join("  •  ", parts);
        }

        private void RenderPosterRow(Panel row, IEnumerable<Movie> movies)
        {
            row.Children.Clear();
            foreach (var movie in movies)
                row.Children.Add(CreatePosterCard(movie));
        }

        private Button CreatePosterCard(Movie movie)
        {
            var image = CreatePosterImage(movie.PosterUrl, 150, 218);
            var title = new TextBlock
            {
                Text = movie.Title ?? "Untitled",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 13,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 2,
                TextWrapping = TextWrapping.Wrap
            };
            var content = new StackPanel { Width = 150, Spacing = 8 };
            content.Children.Add(image);
            content.Children.Add(title);

            var button = new Button
            {
                Width = 164,
                MinHeight = 258,
                Padding = new Thickness(6),
                Margin = new Thickness(2, 4, 6, 10),
                Background = new SolidColorBrush(Color.FromArgb(255, 16, 21, 27)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 38, 51, 59)),
                BorderThickness = new Thickness(1),
                Content = content,
                Tag = movie,
                UseSystemFocusVisuals = true
            };
            button.Click += Poster_Click;
            AttachCardFocus(button);
            return button;
        }

        private Button CreateContinueCard(LibraryEntry entry)
        {
            var grid = new Grid { Width = 350, Height = 178 };
            grid.Children.Add(CreatePosterImage(entry.PosterUrl, 350, 178));
            grid.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(165, 5, 10, 13)) });
            var details = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Spacing = 6,
                Margin = new Thickness(14)
            };
            details.Children.Add(new TextBlock
            {
                Text = entry.DisplayTitle,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 17,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold
            });
            details.Children.Add(new TextBlock
            {
                Text = entry.ProgressText,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 208, 217, 220)),
                FontSize = 12
            });
            details.Children.Add(new ProgressBar
            {
                Height = 5,
                Maximum = 100,
                Value = entry.ProgressPercent,
                Background = new SolidColorBrush(Color.FromArgb(255, 69, 81, 89)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 32, 230, 107))
            });
            grid.Children.Add(details);

            var button = new Button
            {
                Width = 350,
                Height = 178,
                Padding = new Thickness(0),
                Margin = new Thickness(2, 4, 8, 10),
                Content = grid,
                Tag = entry,
                Background = new SolidColorBrush(Color.FromArgb(255, 16, 21, 27)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 38, 51, 59)),
                BorderThickness = new Thickness(1),
                UseSystemFocusVisuals = true
            };
            button.Click += Continue_Click;
            AttachCardFocus(button);
            return button;
        }

        private static Image CreatePosterImage(string? url, double width, double height)
        {
            var image = new Image
            {
                Width = width,
                Height = height,
                Stretch = Stretch.UniformToFill
            };
            if (TryCreateBitmap(url, out var bitmap))
                image.Source = bitmap;
            else
                image.Source = null;
            return image;
        }

        private static bool TryCreateBitmap(string? url, out BitmapImage bitmap)
        {
            bitmap = null!;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                return false;

            try
            {
                bitmap = new BitmapImage(uri);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void AttachCardFocus(Button button)
        {
            var scale = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
            button.RenderTransform = scale;
            button.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            var normalBrush = button.BorderBrush;
            var normalThickness = button.BorderThickness;

            button.GotFocus += (_, _) =>
            {
                scale.ScaleX = 1.055;
                scale.ScaleY = 1.055;
                button.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 32, 230, 107));
                button.BorderThickness = new Thickness(3);
                button.StartBringIntoView();
            };
            button.LostFocus += (_, _) =>
            {
                scale.ScaleX = 1;
                scale.ScaleY = 1;
                button.BorderBrush = normalBrush;
                button.BorderThickness = normalThickness;
            };
        }

        private void HeroWatch_Click(object sender, RoutedEventArgs e) => ViewModel.OpenFeatured();

        private void Poster_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Movie movie)
                ViewModel.OpenMovie(movie);
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is LibraryEntry entry)
                ViewModel.OpenLibraryEntry(entry);
        }
    }
}
