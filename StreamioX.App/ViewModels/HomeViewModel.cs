using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamioX.App.Models;
using StreamioX.App.Navigation;
using WinRT;

namespace StreamioX.App.ViewModels
{
    [GeneratedBindableCustomProperty]
    public partial class HomeViewModel : ObservableObject
    {
        private readonly INavigationService _navigationService;
        public IRelayCommand NavigateToDiscoverCommand { get; }
        [ObservableProperty]
        public partial string WelcomeMessage { get; set; }

        [ObservableProperty]
        public partial Movie? FeaturedMovie { get; set; }

        public IRelayCommand PlayFeaturedCommand { get; }

        public HomeViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;

            WelcomeMessage = "Welcome to StremioX for Xbox";

            FeaturedMovie = new Movie
            {
                Id = "tt0816692",
                Type = "movie",
                Title = "Interstellar",
                Description = "When Earth becomes uninhabitable, a team of ex-pilots and scientists undertake the most important mission in human history: traveling beyond this galaxy to discover whether mankind has a future among the stars.",
                PosterUrl = "https://images.metahub.space/poster/medium/tt0816692/img"
            };

            PlayFeaturedCommand = new RelayCommand(PlayFeatured);
        }

        private void PlayFeatured()
        {
            if (FeaturedMovie != null)
            {
                _navigationService?.NavigateTo(
                    NavigationRoute.Details,
                    FeaturedMovie);
            }
        }
    }
}