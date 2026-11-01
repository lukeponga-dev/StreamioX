using Windows.UI.Xaml.Controls;

namespace StreamioX.App.Navigation
{
    /// <summary>
    /// Abstracts physical Frame navigation from ViewModels.
    /// </summary>
    public interface INavigationService
    {
        void Initialize(Frame shellFrame);

        bool NavigateTo(
            NavigationRoute route,
            object? parameter = null);

        bool GoBack();
    }
}