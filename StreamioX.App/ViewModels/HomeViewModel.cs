using CommunityToolkit.Mvvm.ComponentModel;

namespace StreamioX.App.ViewModels
{
    /// <summary>
    /// ViewModel for the Home dashboard displaying continue watching and recommendations.
    /// </summary>
    public partial class HomeViewModel : ObservableObject
    {
        [ObservableProperty]
        private string welcomeMessage = "Welcome to StremioX";
    }
}