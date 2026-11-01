using System;
using StreamioX.App.Helpers;
using StreamioX.App.Models;
using StreamioX.App.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace StreamioX.App.Views
{
    public sealed partial class DiscoverPage : Page
    {
        public DiscoverViewModel ViewModel { get; }

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
            RenderMovies();
            FocusHelper.Restore(this, CatalogGrid);
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DiscoverViewModel.Movies))
                RenderMovies();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.LoadCatalogAsync();
            RenderMovies();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            FocusHelper.Capture(this, CatalogGrid);
            base.OnNavigatingFrom(e);
        }

        private void RenderMovies()
        {
            if (CatalogRowsPanel == null)
                return;

            CatalogRowsPanel.Children.Clear();

            // Build five-card rows with standard panels. This avoids measuring a
            // WrapGrid directly as ScrollViewer content, which can fail in UWP.
            StackPanel? currentRow = null;
            int column = 0;

            foreach (Movie movie in ViewModel.Movies)
            {
                if (currentRow == null || column == 5)
                {
                    currentRow = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Spacing = 8
                    };
                    CatalogRowsPanel.Children.Add(currentRow);
                    column = 0;
                }
                var cardContent = new StackPanel { Spacing = 6 };
                var posterFrame = new Border
                {
                    Width = 158,
                    Height = 220,
                    Background = new Windows.UI.Xaml.Media.SolidColorBrush(
                        Windows.UI.Color.FromArgb(255, 41, 38, 56)),
                    Child = new TextBlock
                    {
                        Text = "POSTER",
                        Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(
                            Windows.UI.Color.FromArgb(255, 185, 178, 208)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };

                if (!string.IsNullOrWhiteSpace(movie.PosterUrl) &&
                    Uri.TryCreate(movie.PosterUrl, UriKind.Absolute, out var posterUri))
                {
                    try
                    {
                        posterFrame.Child = new Image
                        {
                            Source = new BitmapImage(posterUri),
                            Stretch = Windows.UI.Xaml.Media.Stretch.UniformToFill,
                            Width = 158,
                            Height = 220
                        };
                    }
                    catch
                    {
                        // Keep the visible poster placeholder if the URL is invalid.
                    }
                }

                cardContent.Children.Add(posterFrame);
                cardContent.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(movie.Title) ? "Untitled" : movie.Title,
                    Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(
                        Windows.UI.Color.FromArgb(255, 255, 255, 255)),
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
                    Background = new Windows.UI.Xaml.Media.SolidColorBrush(
                        Windows.UI.Color.FromArgb(255, 23, 24, 39)),
                    BorderBrush = new Windows.UI.Xaml.Media.SolidColorBrush(
                        Windows.UI.Color.FromArgb(255, 52, 48, 71)),
                    BorderThickness = new Thickness(1),
                    Content = cardContent,
                    Tag = movie
                };

                card.Click += MovieCard_Click;
                currentRow!.Children.Add(card);
                column++;
            }
        }

        private void MovieCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: Movie selectedMovie })
                Frame.Navigate(typeof(DetailsPage), selectedMovie);
        }
    }
}
