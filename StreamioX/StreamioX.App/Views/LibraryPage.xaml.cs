using StreamioX.App.ViewModels;
using Windows.UI.Xaml.Controls;

namespace StreamioX.App.Views
{
    public sealed partial class LibraryPage : Page
    {
        public LibraryViewModel ViewModel { get; }

        public LibraryPage()
        {
            InitializeComponent();
            ViewModel = AppServices.GetRequiredService<LibraryViewModel>();
            DataContext = ViewModel;
        }
    }
}
