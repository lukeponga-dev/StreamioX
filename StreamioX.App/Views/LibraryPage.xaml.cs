using StreamioX.App.Models;
using StreamioX.App.ViewModels;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StreamioX.App.Views
{
    public sealed partial class LibraryPage : Page
    {
        public LibraryViewModel ViewModel { get; }

        public LibraryPage()
        {
            InitializeComponent();
            ViewModel = AppServices.GetRequiredService<LibraryViewModel>();
            DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.LoadAsync();
        }

        private void OpenEntry_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is LibraryEntry entry)
                ViewModel.OpenEntryCommand.Execute(entry);
        }

        private void RemoveEntry_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is LibraryEntry entry)
                ViewModel.RemoveEntryCommand.Execute(entry);
        }
    }
}