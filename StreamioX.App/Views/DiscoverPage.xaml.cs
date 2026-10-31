using StreamioX.App.Helpers;
using StreamioX.App.Models;
using StreamioX.App.Services;
using StreamioX.App.ViewModels;
using System.Net.Http;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StreamioX.App.Views
{
    public sealed partial class DiscoverPage : Page
    {
        public DiscoverViewModel ViewModel { get; }

        public DiscoverPage()
        {
            InitializeComponent();

            var httpClient = new HttpClient();
            var stremioClient = new StremioClient(httpClient);
            var catalogService = new CatalogService(stremioClient);

            ViewModel = new DiscoverViewModel(catalogService);
            DataContext = ViewModel;

            Loaded += DiscoverPage_Loaded;
        }

        private void DiscoverPage_Loaded(object sender, RoutedEventArgs e)
        {
            FocusHelper.Restore(this, CatalogGrid);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.LoadCatalogAsync();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            FocusHelper.Capture(this, CatalogGrid);
            base.OnNavigatingFrom(e);
        }

        private void GridView_ItemClick(
            object sender,
            ItemClickEventArgs e)
        {
            if (e.ClickedItem is Movie selectedMovie)
            {
                Frame.Navigate(typeof(DetailsPage), selectedMovie);
            }
        }
    }
}