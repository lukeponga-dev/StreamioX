using System;
using StreamioX.App.Models;
using StreamioX.App.ViewModels;
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
