using Windows.UI.Xaml.Controls;

namespace StreamioX.App.Navigation
{
    /// <summary>
    /// Abstracts physical Frame navigation to keep ViewModels strictly decoupled from XAML.
    /// Enables centralized routing logic and view-model unit testing.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Binds the navigation service to the primary ContentFrame in the application shell.
        /// </summary>
        void Initialize(Frame shellFrame);

        /// <summary>
        /// Navigates to a specific predefined application route.
        /// </summary>
        bool NavigateTo(NavigationRoute route, object parameter = null);

        /// <summary>
        /// Navigates dynamically based on the requested ViewModel type.
        /// </summary>
        bool Navigate<TViewModel>(object parameter = null);

        /// <summary>
        /// Returns to the previous view in the navigation stack, hooking natively into the Xbox B-button.
        /// </summary>
        bool GoBack();
    }
}