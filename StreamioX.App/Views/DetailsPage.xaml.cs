using StreamioX.App.Models;
using StreamioX.App.Navigation;
using StreamioX.App.Services;
using StreamioX.App.ViewModels;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StreamioX.App.Views
{
    public sealed partial class DetailsPage : Page
    {
        public DetailsViewModel ViewModel { get; }

        public DetailsPage()
        {
            this.InitializeComponent();

            var httpClient = new System.Net.Http.HttpClient();
            var stremioClient = new StremioClient(httpClient);
            var streamService = new StreamService(stremioClient);
            var navigationService = new NavigationService();

            ViewModel = new DetailsViewModel(streamService, navigationService);
            this.DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is Movie selectedMovie)
            {
                await ViewModel.InitializeAsync(selectedMovie);
            }
        }
    }
}