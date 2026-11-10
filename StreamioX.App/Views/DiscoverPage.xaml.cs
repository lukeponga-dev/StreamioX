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
            RenderGenres();
            RenderMovies();
            FocusHelper.Restore(this, CatalogGrid);
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DiscoverViewModel.Movies))
            {
                _selectedGenre = "All";
                RenderGenres();
                RenderMovies();
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.LoadCatalogAsync();
            RenderGenres();
            RenderMovies();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            FocusHelper.Capture(this, CatalogGrid);
            base.OnNavigatingFrom(e);
        }

        private void RenderGenres()
        {
            GenreChipsPanel.Children.Clear();
            var genres = new List<string> { "All" };
            genres.AddRange(ViewModel.Movies.SelectMany(m => m.Genres ?? new List<string>())
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g));
            foreach (var genre in genres)
            {
                var selected = string.Equals(genre, _selectedGenre, StringComparison.OrdinalIgnoreCase);
                var chip = new Button
                {
                    Content = genre,
                    MinWidth = 88,
                    MinHeight = 42,
                    Padding = new Thickness(18, 8, 18, 8),
                    Margin = new Thickness(0, 2, 0, 2),
                    Background = new SolidColorBrush(selected ? Color.FromArgb(255, 123, 92, 255) : Color.FromArgb(255, 20, 22, 34)),
                    Foreground = new SolidColorBrush(Colors.White),
                    BorderBrush = new SolidColorBrush(selected ? Color.FromArgb(255, 166, 145, 255) : Color.FromArgb(255, 55, 52, 75)),
                    BorderThickness = new Thickness(1),
                    Tag = genre
                };
                chip.Click += GenreChip_Click;
                GenreChipsPanel.Children.Add(chip);
            }
        }

        private void GenreChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string genre })
            {
                _selectedGenre = genre;
                RenderGenres();
                RenderMovies();
            }
        }

        private void RenderMovies()
        {
            if (CatalogRowsPanel == null) return;
            CatalogRowsPanel.Children.Clear();

            IEnumerable<Movie> movies = string.Equals(_selectedGenre, "All", StringComparison.OrdinalIgnoreCase)
                ? ViewModel.Movies
                : ViewModel.Movies.Where(m => m.Genres?.Contains(_selectedGenre, StringComparer.OrdinalIgnoreCase) == true);

            if (!movies.Any())
            {
                CatalogRowsPanel.Children.Add(new TextBlock
                {
                    Text = ViewModel.Movies.Count == 0 ? "Your installed catalog add-ons have no titles yet." : "No titles in this genre. Choose another genre.",
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 185, 178, 208)),
                    FontSize = 18,
                    Margin = new Thickness(4, 24, 0, 0)
                });
                return;
            }

            var heading = new TextBlock
            {
                Text = string.Equals(_selectedGenre, "All", StringComparison.OrdinalIgnoreCase) ? "Catalog picks" : _selectedGenre,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 22,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(2, 0, 0, 2)
            };
            CatalogRowsPanel.Children.Add(heading);

            var horizontal = new ScrollViewer
            {
                HorizontalScrollMode = ScrollMode.Enabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollMode = ScrollMode.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                ZoomMode = ZoomMode.Disabled
            };
            // Leave room for the Xbox focus scale so adjacent poster cards do not collide.
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 22 };
            horizontal.Content = row;
            CatalogRowsPanel.Children.Add(horizontal);

            foreach (var movie in movies)
            {
                var poster = new Border
                {
                    Width = 142, Height = 202,
                    CornerRadius = new CornerRadius(7),
                    Background = new SolidColorBrush(Color.FromArgb(255, 28, 30, 45)),
                    Child = new TextBlock { Text = "NO POSTER", Foreground = new SolidColorBrush(Color.FromArgb(255, 185, 178, 208)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                };
                if (!string.IsNullOrWhiteSpace(movie.PosterUrl) && Uri.TryCreate(movie.PosterUrl, UriKind.Absolute, out var uri))
                {
                    try { poster.Child = new Image { Source = new BitmapImage(uri), Stretch = Stretch.UniformToFill, Width = 142, Height = 202 }; }
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
                    Width = 158, MinHeight = 270, Padding = new Thickness(7),
                    Margin = new Thickness(3, 5, 3, 10),
                    UseSystemFocusVisuals = true,
                    Background = new SolidColorBrush(Color.FromArgb(255, 16, 18, 29)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 48, 46, 67)),
                    BorderThickness = new Thickness(1), Content = content, Tag = movie
                };
                card.Click += MovieCard_Click;
                card.GotFocus += Card_GotFocus;
                card.LostFocus += Card_LostFocus;
                row.Children.Add(card);
            }
        }

        private void Card_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Control control) control.RenderTransform = new ScaleTransform { ScaleX = 1.06, ScaleY = 1.06 };
        }

        private void Card_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Control control) control.RenderTransform = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
        }

        private void MovieCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: Movie selectedMovie }) Frame.Navigate(typeof(DetailsPage), selectedMovie);
        }
    }
}