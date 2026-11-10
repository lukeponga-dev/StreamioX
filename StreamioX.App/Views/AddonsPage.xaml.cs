using StreamioX.App.Models;
using StreamioX.App.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StreamioX.App.Views
{
    public sealed partial class AddonsPage : Page
    {
        public AddonsViewModel ViewModel { get; }

        public AddonsPage()
        {
            InitializeComponent();
            ViewModel = AppServices.GetRequiredService<AddonsViewModel>();
            DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.LoadAsync();
        }

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ManifestUrlInput = ManifestUrlBox.Text;
            await ViewModel.InstallAsync();
        }

        private async void SyncAll_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.SyncAllAsync();
        }

        private async void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is InstalledAddon addon)
            {
                await ViewModel.UninstallAsync(addon);
            }
        }
    }
}
