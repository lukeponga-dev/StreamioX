using StreamioX.App.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace StreamioX.App.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage()
        {
            InitializeComponent();
            ViewModel = AppServices.GetRequiredService<SettingsViewModel>();
            DataContext = ViewModel;
        }

        private async void StartQr_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.StartQrLoginCommand.ExecuteAsync(null);
        }

        private async void FacebookLogin_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoginWithFacebookCommand.ExecuteAsync(null);
        }

        private void CancelQr_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.CancelQrLoginCommand.Execute(null);
        }

        private async void Logout_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.LogoutCommand.ExecuteAsync(null);
        }

        private async void EmailLogin_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.EmailInput = EmailBox.Text;
            ViewModel.PasswordInput = PasswordBox.Password;
            await ViewModel.LoginEmailCommand.ExecuteAsync(null);
        }

        private async void AuthKeyLogin_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AuthKeyInput = AuthKeyBox.Text;
            await ViewModel.LoginAuthKeyCommand.ExecuteAsync(null);
        }

        private async void SyncAccountAddons_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.SyncAccountAddonsCommand.ExecuteAsync(null);
        }
    }
}
