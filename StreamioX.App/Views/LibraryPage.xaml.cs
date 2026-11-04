using System;
using System.Collections.ObjectModel;
using System.Linq;
using StreamioX.App.Models;
using StreamioX.App.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StreamioX.App.Views
{
    public sealed partial class LibraryPage : Page
    {
        public LibraryViewModel ViewModel { get; }
        private readonly ObservableCollection<LibraryEntry> _visibleEntries = new ObservableCollection<LibraryEntry>();
        private string _filter = "All";

        public LibraryPage()
        {
            InitializeComponent();
            ViewModel = AppServices.GetRequiredService<LibraryViewModel>();
            DataContext = ViewModel;
            LibraryItems.ItemsSource = _visibleEntries;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.LoadAsync();
            ApplyFilters();
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string filter })
            {
                _filter = filter;
                AllFilterButton.Background = FilterBrush("All");
                MoviesFilterButton.Background = FilterBrush("movie");
                SeriesFilterButton.Background = FilterBrush("series");
                ApplyFilters();
            }
        }

        private Windows.UI.Xaml.Media.Brush FilterBrush(string value) =>
            new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255,
                _filter == value ? (byte)123 : (byte)23,
                _filter == value ? (byte)92 : (byte)24,
                _filter == value ? (byte)255 : (byte)39));

        private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();

        private void ApplyFilters()
        {
            if (LibraryItems == null) return;
            var query = ViewModel.Entries.AsEnumerable();
            if (_filter != "All")
                query = query.Where(entry => string.Equals(entry.Movie?.Type ?? "movie", _filter, StringComparison.OrdinalIgnoreCase));

            var sort = (SortBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "recent";
            query = sort switch
            {
                "title" => query.OrderBy(entry => entry.DisplayTitle, StringComparer.OrdinalIgnoreCase),
                "added" => query.OrderByDescending(entry => entry.AddedUtc),
                _ => query.OrderByDescending(entry => entry.LastPlayedUtc ?? entry.AddedUtc)
            };

            _visibleEntries.Clear();
            foreach (var entry in query) _visibleEntries.Add(entry);
            EmptyText.Visibility = _visibleEntries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OpenEntry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is LibraryEntry entry)
                ViewModel.OpenEntryCommand.Execute(entry);
        }

        private async void RemoveEntry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is LibraryEntry entry)
            {
                await ViewModel.RemoveEntryCommand.ExecuteAsync(entry);
                ApplyFilters();
            }
        }
    }
}