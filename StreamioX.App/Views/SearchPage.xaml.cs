using System;
using System.Collections.Generic;
using System.Linq;
using StreamioX.App.Helpers;
using StreamioX.App.Models;
using StreamioX.App.ViewModels;
using Windows.System;
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

        public SearchPage()
        {
            InitializeComponent();
            ViewModel = AppServices.GetRequiredService<SearchViewModel>();
            DataContext = ViewModel;
            ViewModel.ResultsChanged += (_, __) => RenderResults();
            ViewModel.StatusChanged += (_, message) => StatusText.Text = message;
            Loaded += (_, __) => SearchInput.Focus(FocusState.Programmatic);
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await RunSearchAsync();
        }

        private async void SearchInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                await RunSearchAsync();
            }
        }

        private async System.Threading.Tasks.Task RunSearchAsync()
        {
            var query = SearchInput.Text?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                StatusText.Text = "Type a movie or series title to search.";
                SearchInput.Focus(FocusState.Programmatic);
                return;
            }

            SearchButton.IsEnabled = false;
            try
            {
                await ViewModel.SearchAsync(query);
                RenderResults();
            }
            finally
            {
                SearchButton.IsEnabled = true;
            }
        }

        private void RenderResults()
        {
            ResultsRowsPanel.Children.Clear();
            StackPanel? row = null;
            var column = 0;

            foreach (var movie in ViewModel.Results)
            {
                if (row == null || column == 5)
                {
                    row = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Spacing = 8
                    };
                    ResultsRowsPanel.Children.Add(row);
                    column = 0;
                }

                var poster = new Border
                {
                    Width = 158,
                    Height = 220,
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 41, 38, 56)),
                    Child = new TextBlock
                    {
                        Text = "NO POSTER",
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 185, 178, 208)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };

                if (!string.IsNullOrWhiteSpace(movie.PosterUrl) &&
                    Uri.TryCreate(movie.PosterUrl, UriKind.Absolute, out var posterUri))
                {
                    try
                    {
                        poster.Child = new Image
                        {
                            Source = new BitmapImage(posterUri),
                            Stretch = Stretch.UniformToFill,
                            Width = 158,
                            Height = 220
                        };
                    }
                    catch { }
                }

                var content = new StackPanel { Spacing = 6 };
                content.Children.Add(poster);
                content.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(movie.Title) ? "Untitled" : movie.Title,
                    Foreground = new SolidColorBrush(Windows.UI.Colors.White),
                    FontSize = 14,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 40
                });

                var card = new Button
                {
                    Width = 174,
                    Height = 274,
                    Margin = new Thickness(8),
                    Padding = new Thickness(8),
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 23, 24, 39)),
                    BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 52, 48, 71)),
                    BorderThickness = new Thickness(1),
                    Content = content,
                    Tag = movie
                };
                card.Click += ResultCard_Click;
                row.Children.Add(card);
                column++;
            }
        }

        private void ResultCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: Movie selectedMovie })
                Frame.Navigate(typeof(DetailsPage), selectedMovie);
        }
    }
}