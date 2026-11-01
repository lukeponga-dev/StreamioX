using CommunityToolkit.Mvvm.ComponentModel;

namespace StreamioX.App.ViewModels
{
    /// <summary>
    /// ViewModel managing the user's saved collection and library items.
    /// </summary>
    public partial class LibraryViewModel : ObservableObject
    {
        [ObservableProperty]
        private string title = "My Library";
    }
}