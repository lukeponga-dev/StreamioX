using StreamioX.App.Navigation;
using StreamioX.App.ViewModels;
using Windows.UI.Xaml.Controls;

namespace StreamioX.App.Views
{
    /// <summary>
    /// The root shell hosting the NavigationView and central ContentFrame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        /// <summary>
        /// The ViewModel managing navigation actions for this page.
        /// </summary>
        public MainViewModel ViewModel { get; }

        public MainPage()
        {
            this.InitializeComponent();

            // Initialize the navigation service with the main content frame
            var navigationService = new NavigationService();
            navigationService.Initialize(ContentFrame);

            // Bind ViewModel and DataContext
            ViewModel = new MainViewModel(navigationService);
            this.DataContext = ViewModel;

            // Ensure Xbox controller focus lands directly on the navigation shell on load
            this.Loaded += (s, e) => NavShell.Focus(Windows.UI.Xaml.FocusState.Programmatic);
        }

        private void NavShell_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer?.Tag is string tag)
            {
                switch (tag)
                {
                    case "Home":
                        ViewModel.NavigateToHomeCommand.Execute(null);
                        break;
                    case "Discover":
                        ViewModel.NavigateToDiscoverCommand.Execute(null);
                        break;
                    case "Library":
                        ViewModel.NavigateToLibraryCommand.Execute(null);
                        break;
                    case "Addons":
                        ViewModel.NavigateToAddonsCommand.Execute(null);
                        break;
                    case "Player":
                        ViewModel.NavigateToPlayerCommand.Execute(null);
                        break;
                    case "Settings":
                        ViewModel.NavigateToSettingsCommand.Execute(null);
                        break;
                    case "Diagnostic":
                        ViewModel.NavigateToDiagnosticCommand.Execute(null);
                        break;
                }
            }
        }
    }
}