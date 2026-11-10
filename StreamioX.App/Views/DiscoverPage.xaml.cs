using System;
using System.Collections.Generic;
using System.Linq;
using StreamioX.App.Helpers;
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
    public sealed partial class DiscoverPage : Page
    {
        public DiscoverViewModel ViewModel { get; }
        private string _selectedGenre = "All";
        private string _selectedType = "All";

        public DiscoverPage()
        {
            InitializeComponent();
            ViewModel = AppServices.GetRequiredService<DiscoverViewModel>();
            DataContext = ViewModel;
            Loaded += DiscoverPage_Loaded;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void DiscoverPage_Loaded(object sender, RoutedEventArgs e)
        {
            RenderContentTypes();
            RenderGenres();
            RenderMovies();
            FocusHelper.Restore(this, CatalogGrid);
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DiscoverViewModel.Movies))
            {
                _selectedGenre = "All";
                RenderContentTypes();
                RenderGenres();
                RenderMovies();
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.LoadCatalogAsync();
            RenderContentTypes();
            RenderGenres();
            RenderMovies();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            FocusHelper.Capture(this, CatalogGrid);
            base.OnNavigatingFrom(e);
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e) => _ = ReloadAsync();

        private async System.Threading.Tasks.Task ReloadAsync()
        {
            await ViewModel.LoadCatalogAsync();
            RenderContentTypes();
            RenderGenres();
            RenderMovies();
        }

        private void RenderContentTypes()
        {
            ContentTypePanel.Children.Clear();
            AddFilterChip(ContentTypePanel, "All", _selectedType,
                () => { _selectedType = "All"; _selectedGenre = "All"; RefreshFiltersAndResults(); });
            AddFilterChip(ContentTypePanel, "Movies", _selectedType,
                () => { _selectedType = "movie"; _selectedGenre = "All"; RefreshFiltersAndResults(); });
            AddFilterChip(ContentTypePanel, "Series", _selectedType,
                () => { _selectedType = "series"; _selectedGenre = "All"; RefreshFiltersAndResults(); });
        }

        private void RenderGenres()
        {
            GenreChipsPanel.Children.Clear();
            var source = GetTypeFilteredMovies();
            var genres = new List<string> { "All" };
            genres.AddRange(source.SelectMany(m => m.Genres ?? new List<string>())
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g));
            foreach (var genre in genres)
                AddFilterChip(GenreChipsPanel, genre, _selectedGenre,
                    () => { _selectedGenre = genre; RenderGenres(); RenderMovies(); });
        }

        private static void AddFilterChip(Panel panel, string label, string selected,
            Action onClick)
        {
            var isSelected = string.Equals(label, selected, StringComparison.OrdinalIgnoreCase) ||
                (label == "Movies" && selected == "movie") ||
                (label == "Series" && selected == "series");
            var chip = new Button
            {
                Content = label,
                MinWidth = 88,
                MinHeight = 42,
                Padding = new Thickness(18, 8, 18, 8),
                Margin = new Thickness(0, 2, 0, 2),
                Background = new SolidColorBrush(isSelected
                    ? Color.FromArgb(255, 123, 92, 255)
                    : Color.FromArgb(255, 20, 22, 34)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(isSelected
                    ? Color.FromArgb(255, 166, 145, 255)
                    : Color.FromArgb(255, 55, 52, 75)),
                BorderThickness = new Thickness(1),
                Tag = label,
                UseSystemFocusVisuals = true
            };
            chip.Click += (_, _) => onClick();
            panel.Children.Add(chip);
        }

        private IEnumerable<Movie> GetTypeFilteredMovies()
        {
            if (_selectedType == "All") return ViewModel.Movies;
            return ViewModel.Movies.Where(m =>
                string.Equals(m.Type, _selectedType, StringComparison.OrdinalIgnoreCase));
        }

        private void RefreshFiltersAndResults()
        {
            RenderContentTypes();
            RenderGenres();
            RenderMovies();
        }

        private void RenderMovies()
        {
            if (CatalogRowsPanel == null) return;
            CatalogRowsPanel.Children.Clear();

            IEnumerable<Movie> movies = GetTypeFilteredMovies();
            if (!string.Equals(_selectedGenre, "All", StringComparison.OrdinalIgnoreCase))
                movies = movies.Where(m => m.Genres?.Contains(
                    _selectedGenre, StringComparer.OrdinalIgnoreCase) == true);

            var visibleMovies = movies.ToList();
            if (visibleMovies.Count == 0)
            {
                CatalogRowsPanel.Children.Add(new TextBlock
                {
                    Text = ViewModel.IsLoading
                        ? "Loading catalog titles…"
                        : ViewModel.Movies.Count == 0
                            ? "No catalog titles are available. Check your connection and refresh."
                            : "No titles match this filter. Choose another type or genre.",
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 185, 178, 208)),
                    FontSize = 18,
                    Margin = new Thickness(4, 24, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            CatalogRowsPanel.Children.Add(new TextBlock
            {
                Text = (_selectedType == "All" ? "All titles" :
                    _selectedType == "movie" ? "Movies" : "Series") +
                    (_selectedGenre == "All" ? string.Empty : " · " + _selectedGenre),
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 22,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(2, 0, 0, 2)
            });

            var horizontal = new ScrollViewer
            {
                HorizontalScrollMode = ScrollMode.Enabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollMode = ScrollMode.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                ZoomMode = ZoomMode.Disabled
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 22 };
            horizontal.Content = row;
            CatalogRowsPanel.Children.Add(horizontal);

            foreach (var movie in visibleMovies)
            {
                var poster = new Border
                {
                    Width = 142,
                    Height = 202,
                    CornerRadius = new CornerRadius(7),
                    Background = new SolidColorBrush(Color.FromArgb(255, 28, 30, 45)),
                    Child = new TextBlock
                    {
                        Text = "NO POSTER",
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 185, 178, 208)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };

                if (!string.IsNullOrWhiteSpace(movie.PosterUrl) &&
                    Uri.TryCreate(movie.PosterUrl, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
                {
                    try
                    {
                        poster.Child = new Image
                        {
                            Source = new BitmapImage(uri),
                            Stretch = Stretch.UniformToFill,
                            Width = 142,
                            Height = 202
                        };
                    }
                    catch { }
                }

                var content = new StackPanel { Spacing = 7 };
                content.Children.Add(poster);
                content.Children.Add(new TextBlock
                {
                    Text = movie.Title ?? "Untitled",
                    Foreground = new SolidColorBrush(Colors.White),
                    FontSize = 14,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxLines = 2,
                    MaxWidth = 142,
                    MinHeight = 36,
                    MaxHeight = 40
                });

                var card = new Button
                {
                    Width = 158,
                    MinHeight = 270,
                    Padding = new Thickness(7),
                    Margin = new Thickness(3, 5, 3, 10),
                    UseSystemFocusVisuals = true,
                    Background = new SolidColorBrush(Color.FromArgb(255, 16, 18, 29)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 48, 46, 67)),
                    BorderThickness = new Thickness(1),
                    Content = content,
                    Tag = movie
                };
                card.Click += MovieCard_Click;
                card.GotFocus += Card_GotFocus;
                card.LostFocus += Card_LostFocus;
                row.Children.Add(card);
            }
        }

        private void Card_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
                control.RenderTransform = new ScaleTransform { ScaleX = 1.06, ScaleY = 1.06 };
        }

        private void Card_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
                control.RenderTransform = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
        }

        private void MovieCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: Movie selectedMovie })
                Frame.Navigate(typeof(DetailsPage), selectedMovie);
        }
    }
}
