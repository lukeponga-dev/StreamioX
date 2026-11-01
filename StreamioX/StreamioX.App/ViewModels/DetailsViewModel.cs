using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading;
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
        private readonly IAddonService _addonService;
        private CancellationTokenSource? _loadCts;

        [ObservableProperty]
        private Movie? selectedMovie;

        [ObservableProperty]
        private ObservableCollection<StreamSource> streams = new();

        [ObservableProperty]
        private bool isLoadingStreams;

        [ObservableProperty]
        private string? errorMessage;

        public DetailsViewModel(
            IStreamService streamService,
            INavigationService navigationService,
            IAddonService addonService)
        {
            _streamService = streamService ?? throw new ArgumentNullException(nameof(streamService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _addonService = addonService ?? throw new ArgumentNullException(nameof(addonService));
        }

        public async Task InitializeAsync(Movie movie)
        {
            SelectedMovie = movie;
            await LoadStreamsAsync();
        }

        private async Task LoadStreamsAsync()
        {
            if (SelectedMovie == null) return;

            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            IsLoadingStreams = true;
            ErrorMessage = null;
            Streams.Clear();

            try
            {
                await _addonService.InitializeAsync(token).ConfigureAwait(true);

                var type = SelectedMovie.Type ?? "movie";
                var id = SelectedMovie.Id ?? string.Empty;

                var fetched = await _streamService
                    .GetFromInstalledAddonsAsync(type, id, token)
                    .ConfigureAwait(true);

                foreach (var stream in fetched)
                    Streams.Add(stream);

                if (Streams.Count == 0)
                    ErrorMessage = "No streams found. Install a stream addon that supports this title.";
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load streams: {ex.Message}";
            }
            finally
            {
                IsLoadingStreams = false;
            }
        }

        [RelayCommand]
        private void PlayStream(StreamSource? stream)
        {
            if (stream == null || string.IsNullOrWhiteSpace(stream.Url)) return;
            _navigationService.NavigateTo(NavigationRoute.Player, stream.Url);
        }
    }
}
