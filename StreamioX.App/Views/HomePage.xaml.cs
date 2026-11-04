using StreamioX.App.Models;
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
            ViewModel = AppServices.GetRequiredService<HomeViewModel>();
            DataContext = ViewModel;
            Loaded += HomePage_Loaded;
        }

        private async void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();
        }

        private void Poster_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Movie movie) ViewModel.OpenMovie(movie);
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is LibraryEntry entry) ViewModel.OpenLibraryEntry(entry);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
        }
    }
}