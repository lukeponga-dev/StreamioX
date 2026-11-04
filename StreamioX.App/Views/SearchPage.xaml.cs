using System;
using System.Collections.Generic;
using StreamioX.App.Helpers;
using StreamioX.App.Models;
using StreamioX.App.ViewModels;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace StreamioX.App.Views
{
    public sealed partial class SearchPage : Page
    {
        public SearchViewModel ViewModel { get; }
        private readonly DispatcherTimer _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        private readonly List<string> _recentSearches = new List<string>();
        private bool _isRunningSearch;

        public SearchPage()
        {
            InitializeComponent();
            ViewModel = AppServices.GetRequiredService<SearchViewModel>();
            DataContext = ViewModel;
            ViewModel.ResultsChanged += (_, __) => RenderResults();
            ViewModel.StatusChanged += (_, message) => StatusText.Text = message;
            _searchTimer.Tick += SearchTimer_Tick;
            Loaded += (_, __) => { RenderRecentSearches(); SearchInput.Focus(FocusState.Programmatic); };
        }

        private async void SearchTimer_Tick(object sender, object e)
        {
            _searchTimer.Stop();
            await RunSearchAsync();
        }

        private void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer.Stop();
            if (!string.IsNullOrWhiteSpace(SearchInput.Text)) _searchTimer.Start();
            else
            {
                ResultsRowsPanel.Children.Clear();
                StatusText.Text = "Start typing to search your installed catalogs.";
            }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e) => await RunSearchAsync();

        private async void SearchInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter) { e.Handled = true; _searchTimer.Stop(); await RunSearchAsync(); }
        }

        private async System.Threading.Tasks.Task RunSearchAsync()
        {
            var query = SearchInput.Text?.Trim();
            if (string.IsNullOrWhiteSpace(query) || _isRunningSearch) return;
            _isRunningSearch = true;
            SearchButton.IsEnabled = false;
            try
            {
                await ViewModel.SearchAsync(query);
                _recentSearches.RemoveAll(x => string.Equals(x, query, StringComparison.OrdinalIgnoreCase));
                _recentSearches.Insert(0, query);
                if (_recentSearches.Count > 6) _recentSearches.RemoveAt(_recentSearches.Count - 1);
                RenderRecentSearches();
            }
            finally { _isRunningSearch = false; SearchButton.IsEnabled = true; }
        }

        private void RenderRecentSearches()
        {
            RecentSearchesPanel.Children.Clear();
            foreach (var query in _recentSearches)
            {
                var button = new Button { Content = query, Tag = query, Padding = new Thickness(12, 6, 12, 6),
                    Background = new SolidColorBrush(Color.FromArgb(255, 24, 25, 39)),
                    Foreground = new SolidColorBrush(Colors.White),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 67, 62, 91)), BorderThickness = new Thickness(1) };
                button.Click += RecentSearch_Click;
                RecentSearchesPanel.Children.Add(button);
            }
        }

        private async void RecentSearch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string query })
            {
                SearchInput.Text = query;
                _searchTimer.Stop();
                await RunSearchAsync();
            }
        }

        private void RenderResults()
        {
            ResultsRowsPanel.Children.Clear();
            StackPanel? row = null;
            var column = 0;
            foreach (var movie in ViewModel.Results)
            {
                if (row == null || column == 6)
                {
                    row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Spacing = 8 };
                    ResultsRowsPanel.Children.Add(row);
                    column = 0;
                }
                var poster = new Border { Width = 132, Height = 184, Background = new SolidColorBrush(Color.FromArgb(255, 41, 38, 56)),
                    Child = new TextBlock { Text = "NO POSTER", Foreground = new SolidColorBrush(Color.FromArgb(255, 185, 178, 208)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                if (!string.IsNullOrWhiteSpace(movie.PosterUrl) && Uri.TryCreate(movie.PosterUrl, UriKind.Absolute, out var posterUri))
                {
                    try { poster.Child = new Image { Source = new BitmapImage(posterUri), Stretch = Stretch.UniformToFill, Width = 132, Height = 184 }; } catch { }
                }
                var content = new StackPanel { Spacing = 6 };
                content.Children.Add(poster);
                content.Children.Add(new TextBlock { Text = movie.Title ?? "Untitled", Foreground = new SolidColorBrush(Colors.White), FontSize = 13,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, MaxHeight = 38, MaxWidth = 132 });
                var card = new Button { Width = 146, MinHeight = 238, Margin = new Thickness(5), Padding = new Thickness(6),
                    Background = new SolidColorBrush(Color.FromArgb(255, 23, 24, 39)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 52, 48, 71)), BorderThickness = new Thickness(1), Content = content, Tag = movie };
                card.Click += ResultCard_Click;
                row.Children.Add(card);
                column++;
            }
        }

        private void ResultCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: Movie selectedMovie }) Frame.Navigate(typeof(DetailsPage), selectedMovie);
        }
    }
}