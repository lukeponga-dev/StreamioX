using System;
using StreamioX.App.Navigation;
using StreamioX.App.ViewModels;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace StreamioX.App.Views
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; }
        private readonly INavigationService _navigationService;

        public MainPage()
        {
            InitializeComponent();
            _navigationService = AppServices.GetRequiredService<INavigationService>();
            _navigationService.Initialize(ContentFrame);
            ViewModel = AppServices.GetRequiredService<MainViewModel>();
            DataContext = ViewModel;
            Loaded += MainPage_Loaded;
            Unloaded += MainPage_Unloaded;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().BackRequested += System_BackRequested;
            NavShell.SelectedItem = HomeNavItem;
            if (ContentFrame.Content == null) ViewModel.NavigateToHomeCommand.Execute(null);
            HomeNavItem.Focus(FocusState.Programmatic);
        }

        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().BackRequested -= System_BackRequested;
        }

        private void System_BackRequested(object sender, BackRequestedEventArgs e)
        {
            if (ContentFrame.CanGoBack) { e.Handled = true; ContentFrame.GoBack(); }
        }

        private void NavShell_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer?.Tag is not string tag) return;
            switch (tag)
            {
                case "Home": ViewModel.NavigateToHomeCommand.Execute(null); break;
                case "Discover": ViewModel.NavigateToDiscoverCommand.Execute(null); break;
                case "Search": ViewModel.NavigateToSearchCommand.Execute(null); break;
                case "Library": ViewModel.NavigateToLibraryCommand.Execute(null); break;
                case "Addons": ViewModel.NavigateToAddonsCommand.Execute(null); break;
            }
        }

        private void ProfileLibrary_Click(object sender, RoutedEventArgs e) =>
            ViewModel.NavigateToLibraryCommand.Execute(null);

        private void ProfileAddons_Click(object sender, RoutedEventArgs e) =>
            ViewModel.NavigateToAddonsCommand.Execute(null);

        private void ProfileSettings_Click(object sender, RoutedEventArgs e) =>
            ViewModel.NavigateToSettingsCommand.Execute(null);

        private void ProfileDiagnostics_Click(object sender, RoutedEventArgs e) =>
            ViewModel.NavigateToDiagnosticCommand.Execute(null);

        private async void ProfileAbout_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "About StreamioX",
                Content = "StreamioX is an Xbox-first media discovery and playback client. Catalogs, metadata and streams are supplied by your installed Stremio-compatible add-ons.",
                CloseButtonText = "Close",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}