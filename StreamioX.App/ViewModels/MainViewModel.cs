// ViewModels/MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamioX.App.Navigation;

namespace StreamioX.App.ViewModels
{
    /// <summary>
    /// Coordinates top-level application routing and manages the state 
    /// of the Xbox navigation rail destinations.
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        private readonly INavigationService _navigationService;

        /// <summary>
        /// Initializes the MainViewModel with the injected navigation service.
        /// </summary>
        /// <param name="navigationService">Service responsible for physical frame navigation.</param>
        public MainViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;
        }

        /// <summary>
        /// Navigates the active frame to the Home dashboard view[cite: 1].
        /// </summary>
        [RelayCommand]
        private void NavigateToHome()
        {
            _navigationService.NavigateTo(NavigationRoute.Home);
        }

        /// <summary>
        /// Navigates the active frame to the Discover catalog view[cite: 1].
        /// </summary>
        [RelayCommand]
        private void NavigateToDiscover()
        {
            _navigationService.NavigateTo(NavigationRoute.Discover);
        }

        /// <summary>
        /// Navigates the active frame to the user's Library view[cite: 1].
        /// </summary>
        [RelayCommand]
        private void NavigateToLibrary()
        {
            _navigationService.NavigateTo(NavigationRoute.Library);
        }

        /// <summary>
        /// Navigates the active frame to the Add-ons manager view[cite: 1].
        /// </summary>
        [RelayCommand]
        private void NavigateToAddons()
        {
            _navigationService.NavigateTo(NavigationRoute.Addons);
        }

        /// <summary>
        /// Navigates the active frame to the native video player view[cite: 1].
        /// </summary>
        [RelayCommand]
        private void NavigateToPlayer()
        {
            _navigationService.NavigateTo(NavigationRoute.Player);
        }

        /// <summary>
        /// Navigates the active frame to the Settings view[cite: 1].
        /// </summary>
        [RelayCommand]
        private void NavigateToSettings()
        {
            _navigationService.NavigateTo(NavigationRoute.Settings);
        }

        /// <summary>
        /// Navigates the active frame to the System Diagnostics view[cite: 1].
        /// </summary>
        [RelayCommand]
        private void NavigateToDiagnostic()
        {
            _navigationService.NavigateTo(NavigationRoute.Diagnostics);
        }
    }
}