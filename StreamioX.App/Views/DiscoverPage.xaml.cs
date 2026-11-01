using StreamioX.App.Models;
using StreamioX.App.Services;
using StreamioX.App.ViewModels;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StreamioX.App.Views
{
    public sealed partial class DiscoverPage : Page
    {
        public DiscoverViewModel ViewModel { get; }

        private bool _catalogRendered;

        public DiscoverPage()
        {
            InitializeComponent();

            var httpClient = new HttpClient();
            var stremioClient = new StremioClient(httpClient);
            var catalogService = new CatalogService(stremioClient);

            ViewModel = new DiscoverViewModel(catalogService);

            // Let XAML binding manage ItemsSource.
            DataContext = ViewModel;

            Debug.WriteLine("[Discover] Page initialized.");
            Debug.WriteLine(
                $"[Discover] ViewModel created: {ViewModel != null}");
            Debug.WriteLine(
                $"[Discover] Movies collection: {ViewModel.Movies != null}");
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            try
            {
                await ViewModel.LoadCatalogAsync(
                    "https://v3-cinemeta.strem.io");

                Debug.WriteLine(
                    $"[Discover] Loaded movies: {ViewModel.Movies.Count}");

                Debug.WriteLine(
                    $"[Discover] UI thread: {Window.Current.Dispatcher.HasThreadAccess}");

                // The page/control may be revisited. Do not clear the WinRT
                // ItemCollection here; clearing it can throw during projection.
                // Populate only once for this page instance.
                if (!_catalogRendered)
                {
                    foreach (Movie movie in ViewModel.Movies)
                    {
                        CatalogTestControl.Items.Add(movie);
                    }

                    _catalogRendered = true;
                }

                Debug.WriteLine(
                    $"[Discover] Render items added: {CatalogTestControl.Items.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Discover] Loading failed");
                Debug.WriteLine(ex.ToString());
            }
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