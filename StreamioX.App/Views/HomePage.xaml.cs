using Windows.UI.Xaml.Controls;
using StreamioX.App.ViewModels;

namespace StreamioX.App.Views
{
    public sealed partial class HomePage : Page
    {
        public HomeViewModel ViewModel { get; }

        public HomePage()
        {
            this.InitializeComponent();
            ViewModel = new HomeViewModel();
            this.DataContext = ViewModel;
        }
    }
}