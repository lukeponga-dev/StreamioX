using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using StreamioX.App.Models;
using StreamioX.App.Navigation;
using StreamioX.App.Services;

namespace StreamioX.App.ViewModels
{
    public partial class DetailsViewModel : ObservableObject
    {
        private readonly IStreamService _streamService;
        private readonly INavigationService _navigationService;

        [ObservableProperty]
        private Movie selectedMovie;

        [ObservableProperty]
        private ObservableCollection<StreamSource> streams = new ObservableCollection<StreamSource>();

        [ObservableProperty]
        private bool isLoadingStreams;

        public DetailsViewModel(IStreamService streamService, INavigationService navigationService)
        {
            _streamService = streamService ?? throw new ArgumentNullException(nameof(streamService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        }

        public async Task InitializeAsync(Movie movie)
        {
            SelectedMovie = movie;
            await LoadStreamsAsync();
        }

        private async Task LoadStreamsAsync()
        {
            if (SelectedMovie == null) return;

            IsLoadingStreams = true;
            Streams.Clear();

            try
            {
                // Default Cinemeta metadata or stream resolution endpoint
                string testAddonUrl = "https://v3-cinemeta.strem.fun";
                var fetchedStreams = await _streamService.GetStreamsAsync(testAddonUrl, SelectedMovie.Type ?? "movie", SelectedMovie.Id);

                if (fetchedStreams != null)
                {
                    foreach (var stream in fetchedStreams)
                    {
                        Streams.Add(stream);
                    }
                }
            }
            finally
            {
                IsLoadingStreams = false;
            }
        }

        [RelayCommand]
        private void PlayStream(StreamSource stream)
        {
            if (stream == null || string.IsNullOrWhiteSpace(stream.Url)) return;

            // Route directly to PlayerPage with the resolved stream URL[cite: 1, 2]
            _navigationService.NavigateTo(NavigationRoute.Player, stream.Url);
        }
    }
}