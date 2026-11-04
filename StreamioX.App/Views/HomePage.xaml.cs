using System;
using System.Linq;
using StreamioX.App.Models;
using StreamioX.App.ViewModels;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

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
                    args.PropertyName == nameof(HomeViewModel.IsLoading))
                    RenderRows();
            };
            Loaded += HomePage_Loaded;
        }

        private async void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();
        }

        private void RenderRows()
        {
            if (PopularMoviesRow == null) return;
            ContinueRow.Children.Clear();
            foreach (var entry in ViewModel.ContinueWatching)
                ContinueRow.Children.Add(CreateContinueCard(entry));

            RenderPosterRow(PopularMoviesRow, ViewModel.PopularMovies);
            RenderPosterRow(PopularSeriesRow, ViewModel.PopularSeries);
            RenderPosterRow(RecommendedRow, ViewModel.Recommended);
            RenderPosterRow(RecentlyAddedRow, ViewModel.RecentlyAdded);
        }

        private void RenderPosterRow(Panel row, System.Collections.Generic.IEnumerable<Movie> movies)
        {
            row.Children.Clear();
            foreach (var movie in movies)
                row.Children.Add(CreatePosterCard(movie));
        }

        private Button CreatePosterCard(Movie movie)
        {
            var poster = CreatePosterImage(movie.PosterUrl, 132, 190);
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
            var content = new StackPanel { Width = 132, Spacing = 7 };
            content.Children.Add(poster);
            content.Children.Add(title);
            var button = new Button
            {
                Width = 146,
                MinHeight = 228,
                Padding = new Thickness(6),
                Margin = new Thickness(0, 2, 4, 8),
                Background = new SolidColorBrush(Color.FromArgb(255, 16, 21, 27)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 38, 51, 59)),
                BorderThickness = new Thickness(1),
                Content = content,
                Tag = movie,
                UseSystemFocusVisuals = true
            };
            button.Click += Poster_Click;
            button.GotFocus += Card_GotFocus;
            button.LostFocus += Card_LostFocus;
            return button;
        }

        private Button CreateContinueCard(LibraryEntry entry)
        {
            var grid = new Grid { Width = 350, Height = 174 };
            grid.Children.Add(CreatePosterImage(entry.PosterUrl, 350, 174));
            grid.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(150, 5, 10, 13)) });
            var details = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Spacing = 5, Margin = new Thickness(12) };
            details.Children.Add(new TextBlock { Text = entry.DisplayTitle, Foreground = new SolidColorBrush(Colors.White), FontSize = 17, FontWeight = Windows.UI.Text.FontWeights.SemiBold });
            details.Children.Add(new TextBlock { Text = entry.ProgressText, Foreground = new SolidColorBrush(Color.FromArgb(255, 208, 217, 220)), FontSize = 12 });
            details.Children.Add(new ProgressBar { Height = 5, Maximum = 100, Value = entry.ProgressPercent, Background = new SolidColorBrush(Color.FromArgb(255, 69, 81, 89)), Foreground = new SolidColorBrush(Color.FromArgb(255, 32, 230, 107)) });
            grid.Children.Add(details);
            var button = new Button
            {
                Width = 350, Height = 174, Padding = new Thickness(0),
                Margin = new Thickness(0, 2, 4, 8), Content = grid, Tag = entry,
                Background = new SolidColorBrush(Color.FromArgb(255, 16, 21, 27)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 38, 51, 59)),
                BorderThickness = new Thickness(1), UseSystemFocusVisuals = true
            };
            button.Click += Continue_Click;
            return button;
        }

        private Image CreatePosterImage(string? posterUrl, double width, double height)
        {
            var image = new Image
            {
                Width = width, Height = height,
                Stretch = Windows.UI.Xaml.Media.Stretch.UniformToFill
            };
            if (Uri.TryCreate(posterUrl, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            {
                try { image.Source = new BitmapImage(uri); }
                catch { }
            }
            else
            {
                image.Source = null;
            }
            return image;
        }

        private void Card_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
                control.RenderTransform = new ScaleTransform { ScaleX = 1.05, ScaleY = 1.05 };
        }

        private void Card_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
                control.RenderTransform = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
        }

        private void Poster_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Movie movie) ViewModel.OpenMovie(movie);
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is LibraryEntry entry) ViewModel.OpenLibraryEntry(entry);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
        }
    }
}