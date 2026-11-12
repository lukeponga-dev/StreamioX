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
using Windows.UI.Xaml.Navigation;

namespace StreamioX.App.Views
{
    public sealed partial class DiscoverPage : Page
    {
        public DiscoverViewModel ViewModel { get; }
        private string _selectedGenre = "All";
        private string _selectedType = "All";
        private bool _isRenderingFilters;

        private static readonly SolidColorBrush ActiveBackground =
            new SolidColorBrush(Color.FromArgb(255, 91, 92, 230));
        private static readonly SolidColorBrush ActiveBorder =
            new SolidColorBrush(Color.FromArgb(255, 124, 125, 242));
        private static readonly SolidColorBrush InactiveBackground =
            new SolidColorBrush(Color.FromArgb(255, 24, 29, 43));
        private static readonly SolidColorBrush InactiveBorder =
            new SolidColorBrush(Color.FromArgb(255, 37, 44, 63));
        private static readonly SolidColorBrush InactiveForeground =
            new SolidColorBrush(Color.FromArgb(255, 212, 216, 232));

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
            FocusHelper.Restore(this, CatalogGrid);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (ViewModel.Movies.Count == 0)
                await ViewModel.LoadCatalogAsync();

            RenderContentTypes();
            RenderGenres();
            UpdateResults();
            UpdateCountSummary();
            if (CatalogGrid.Items.Count == 0)
                TypeAllButtonFocus();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            FocusHelper.Capture(this, CatalogGrid);
            base.OnNavigatingFrom(e);
        }

        private void ViewModel_PropertyChanged(object? sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DiscoverViewModel.Movies))
            {
                if (!_isRenderingFilters)
                {
                    _selectedGenre = "All";
                    RenderGenres();
                }
                UpdateResults();
                UpdateCountSummary();
            }
            else if (e.PropertyName == nameof(DiscoverViewModel.StatusMessage))
            {
                UpdateCountSummary();
            }
            else if (e.PropertyName == nameof(DiscoverViewModel.IsLoading))
            {
                UpdateResults();
            }
        }

        private Button? _typeAllButton;

        private void TypeAllButtonFocus()
        {
            _typeAllButton?.Focus(FocusState.Programmatic);
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            _ = ReloadAsync();
        }

        private async System.Threading.Tasks.Task ReloadAsync()
        {
            await ViewModel.LoadCatalogAsync();
            RenderContentTypes();
            RenderGenres();
            UpdateResults();
            UpdateCountSummary();
        }

        private void RenderContentTypes()
        {
            _isRenderingFilters = true;
            ContentTypePanel.Children.Clear();
            _typeAllButton = AddFilterChip(ContentTypePanel, "All", _selectedType,
                () => ChangeType("All"));
            var moviesButton = AddFilterChip(ContentTypePanel, "Movies", _selectedType,
                () => ChangeType("movie"));
            var seriesButton = AddFilterChip(ContentTypePanel, "Series", _selectedType,
                () => ChangeType("series"));

            RefreshButton.XYFocusDown = _typeAllButton;
            _typeAllButton.XYFocusUp = RefreshButton;
            _typeAllButton.XYFocusRight = moviesButton;
            _typeAllButton.XYFocusDown = GenreScrollViewer;
            moviesButton.XYFocusLeft = _typeAllButton;
            moviesButton.XYFocusRight = seriesButton;
            moviesButton.XYFocusUp = RefreshButton;
            moviesButton.XYFocusDown = GenreScrollViewer;
            seriesButton.XYFocusLeft = moviesButton;
            seriesButton.XYFocusRight = seriesButton;
            seriesButton.XYFocusUp = RefreshButton;
            seriesButton.XYFocusDown = GenreScrollViewer;
            GenreScrollViewer.XYFocusUp = _typeAllButton;
            GenreScrollViewer.XYFocusDown = CatalogGrid;
            CatalogGrid.XYFocusUp = GenreScrollViewer;
            _isRenderingFilters = false;
        }

        private void ChangeType(string type)
        {
            _selectedType = type;
            _selectedGenre = "All";
            RenderContentTypes();
            RenderGenres();
            UpdateResults();
            UpdateCountSummary();
        }

        private void RenderGenres()
        {
            _isRenderingFilters = true;
            GenreChipsPanel.Children.Clear();
            var source = GetTypeFilteredMovies();
            var genres = new List<string> { "All" };
            genres.AddRange(source.SelectMany(movie => movie.Genres ?? new List<string>())
                .Where(genre => !string.IsNullOrWhiteSpace(genre))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(genre => genre, StringComparer.CurrentCultureIgnoreCase));

            var genreButtons = new List<Button>();
            foreach (var genre in genres)
            {
                var capturedGenre = genre;
                var genreButton = AddFilterChip(GenreChipsPanel, capturedGenre, _selectedGenre,
                    () =>
                    {
                        _selectedGenre = capturedGenre;
                        RenderGenres();
                        UpdateResults();
                        UpdateCountSummary();
                    });
                genreButton.XYFocusUp = _typeAllButton;
                genreButton.XYFocusDown = CatalogGrid;
                genreButtons.Add(genreButton);
            }
            for (var i = 0; i < genreButtons.Count; i++)
            {
                genreButtons[i].XYFocusLeft = genreButtons[Math.Max(0, i - 1)];
                genreButtons[i].XYFocusRight = genreButtons[Math.Min(genreButtons.Count - 1, i + 1)];
            }
            _isRenderingFilters = false;
        }

        private Button AddFilterChip(Panel panel, string label, string selected, Action onClick)
        {
            var isSelected = string.Equals(label, selected, StringComparison.OrdinalIgnoreCase);
            var button = new Button
            {
                Content = label,
                MinWidth = label.Length <= 5 ? 72 : 88,
                MinHeight = 38,
                Padding = new Thickness(18, 8, 18, 8),
                Margin = new Thickness(0, 1, 0, 1),
                Background = isSelected ? ActiveBackground : InactiveBackground,
                Foreground = isSelected ? new SolidColorBrush(Colors.White) : InactiveForeground,
                BorderBrush = isSelected ? ActiveBorder : InactiveBorder,
                BorderThickness = new Thickness(1),
                Style = (Style)Resources["PillFilterButtonStyle"],
                UseSystemFocusVisuals = false
            };
            button.Click += (_, _) => onClick();
            panel.Children.Add(button);
            return button;
        }

        private IEnumerable<Movie> GetTypeFilteredMovies()
        {
            if (_selectedType == "All")
                return ViewModel.Movies;

            return ViewModel.Movies.Where(movie =>
                string.Equals(movie.Type, _selectedType, StringComparison.OrdinalIgnoreCase));
        }

        private void UpdateCountSummary()
        {
            var visible = GetGenreFilteredMovies().ToList();
            var movieCount = visible.Count(movie =>
                string.Equals(movie.Type, "movie", StringComparison.OrdinalIgnoreCase));
            var seriesCount = visible.Count(movie =>
                string.Equals(movie.Type, "series", StringComparison.OrdinalIgnoreCase));
            CountSummaryText.Text = $"{movieCount} movies · {seriesCount} series · {visible.Count} titles";
        }

        private void UpdateResults()
        {
            var visible = GetGenreFilteredMovies().ToList();
            var typeTitle = _selectedType == "movie" ? "Movies"
                : _selectedType == "series" ? "Series" : "All titles";
            SectionTitleText.Text = _selectedGenre == "All"
                ? typeTitle
                : $"{_selectedGenre} {(typeTitle == "All titles" ? "titles" : typeTitle.ToLowerInvariant())}";
            CatalogGrid.ItemsSource = visible;
            EmptyStateText.Visibility = visible.Count == 0 && !ViewModel.IsLoading
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private IEnumerable<Movie> GetGenreFilteredMovies()
        {
            var query = GetTypeFilteredMovies();
            if (!string.Equals(_selectedGenre, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(movie => movie.Genres?.Any(genre =>
                    string.Equals(genre, _selectedGenre, StringComparison.OrdinalIgnoreCase)) == true);
            }
            return query;
        }

        private void TitlesGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Movie movie)
                Frame.Navigate(typeof(DetailsPage), movie);
        }
    }
}
