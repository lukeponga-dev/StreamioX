using Windows.UI.Xaml.Controls;
using StreamioX.App.ViewModels;

namespace StreamioX.App.Views
{
    public sealed partial class LibraryPage : Page
    {
        public LibraryViewModel ViewModel { get; }

        public LibraryPage()
        {
            this.InitializeComponent();
            ViewModel = new LibraryViewModel();
            this.DataContext = ViewModel;
        }
    }
}