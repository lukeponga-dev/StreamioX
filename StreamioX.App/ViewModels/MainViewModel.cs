// Purpose: Owns MainViewModel screen state and user actions, keeping presentation logic separate from the XAML view.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamioX.App.Navigation;

namespace StreamioX.App.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly INavigationService _navigationService;
        public MainViewModel(INavigationService navigationService) { _navigationService = navigationService; }

        [RelayCommand] private void NavigateToHome() => _navigationService.NavigateTo(NavigationRoute.Home);
        [RelayCommand] private void NavigateToDiscover() => _navigationService.NavigateTo(NavigationRoute.Discover);
        [RelayCommand] private void NavigateToSearch() => _navigationService.NavigateTo(NavigationRoute.Search);
        [RelayCommand] private void NavigateToLibrary() => _navigationService.NavigateTo(NavigationRoute.Library);
        [RelayCommand] private void NavigateToAddons() => _navigationService.NavigateTo(NavigationRoute.Addons);
        [RelayCommand] private void NavigateToPlayer() => _navigationService.NavigateTo(NavigationRoute.Player);
        [RelayCommand] private void NavigateToSettings() => _navigationService.NavigateTo(NavigationRoute.Settings);
        [RelayCommand] private void NavigateToDiagnostic() => _navigationService.NavigateTo(NavigationRoute.Diagnostics);
    }
}
