// Views/HomePage.xaml.cs
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using StreamioX.App.Navigation;
using StreamioX.App.ViewModels;

namespace StreamioX.App.Views
{
    public sealed partial class HomePage : Page
    {
        public HomeViewModel ViewModel { get; }

        public HomePage()
        {
            this.InitializeComponent();

            var navigationService = new NavigationService();
            if (Window.Current?.Content is Frame rootFrame)
            {
                navigationService.Initialize(rootFrame);
            }

            ViewModel = new HomeViewModel(navigationService);
            this.DataContext = ViewModel;
        }

        private void ExploreDiscover_Click(object sender, RoutedEventArgs e)
        {
            if (Frame != null)
            {
                Frame.Navigate(typeof(DiscoverPage));
            }
            else
            {
                ViewModel.NavigateToDiscoverCommand.Execute(null);
            }
        }
    }
}