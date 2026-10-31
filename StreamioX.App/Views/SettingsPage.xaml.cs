using Windows.UI.Xaml.Controls;
using StreamioX.App.ViewModels;

namespace StreamioX.App.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage()
        {
            this.InitializeComponent();
            ViewModel = new SettingsViewModel();
            this.DataContext = ViewModel;
        }
    }
}