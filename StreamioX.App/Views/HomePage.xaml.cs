using StreamioX.App.Helpers;
using StreamioX.App.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StreamioX.App.Views
{
    public sealed partial class HomePage : Page
    {
        public HomeViewModel ViewModel { get; }

        public HomePage()
        {
            InitializeComponent();

            ViewModel = new HomeViewModel();
            DataContext = ViewModel;

            Loaded += HomePage_Loaded;
        }

        private void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            FocusHelper.Restore(this, ExploreDiscoverButton);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            FocusHelper.Capture(this, ExploreDiscoverButton);
            base.OnNavigatingFrom(e);
        }

        private void ExploreDiscover_Click(
            object sender,
            RoutedEventArgs e)
        {
            Frame.Navigate(typeof(DiscoverPage));
        }
    }
}