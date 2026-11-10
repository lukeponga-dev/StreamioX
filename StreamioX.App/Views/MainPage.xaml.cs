using System;
using System.Threading.Tasks;
using StreamioX.App.Navigation;
using StreamioX.App.ViewModels;
using StreamioX.App.Services;
using StreamioX.App.Layout;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace StreamioX.App.Views
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; }
        private readonly INavigationService _navigationService;
        private readonly IAuthService _authService;

        public MainPage()
        {
            InitializeComponent();
            _navigationService = AppServices.GetRequiredService<INavigationService>();
            _authService = AppServices.GetRequiredService<IAuthService>();
            _navigationService.Initialize(ContentFrame);
            ContentFrame.NavigationFailed += ContentFrame_NavigationFailed;
            ContentFrame.Navigated += ContentFrame_Navigated;
            ViewModel = AppServices.GetRequiredService<MainViewModel>();
            DataContext = ViewModel;
            Loaded += MainPage_Loaded;
            Unloaded += MainPage_Unloaded;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().BackRequested += System_BackRequested;
            _authService.SessionChanged -= AuthService_SessionChanged;
            _authService.SessionChanged += AuthService_SessionChanged;
            NavShell.SelectedItem = HomeNavItem;
            if (ContentFrame.Content == null) ViewModel.NavigateToHomeCommand.Execute(null);
            UpdatePlaybackChrome(ContentFrame.Content is PlayerPage);
            _ = RefreshProfileAsync();
            if (ContentFrame.Content is not PlayerPage)
                HomeNavItem.Focus(FocusState.Programmatic);
        }

        private void ContentFrame_Navigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            // PlayerPage is hosted in the same Frame, but takes over the entire
            // app viewport: hide navigation/profile chrome and remove safe-area padding.
            UpdatePlaybackChrome(e.Content is PlayerPage);
        }

        private void UpdatePlaybackChrome(bool isPlayingPage)
        {
            NavShell.Visibility = isPlayingPage ? Visibility.Collapsed : Visibility.Visible;
            ProfileButton.Visibility = isPlayingPage ? Visibility.Collapsed : Visibility.Visible;
            RootLayout.Padding = isPlayingPage ? new Thickness(0) : XboxConstants.SafeArea;
        }

        private void ContentFrame_NavigationFailed(object sender, Windows.UI.Xaml.Navigation.NavigationFailedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[StreamioX] Page navigation failed: {e.SourcePageType?.FullName}: {e.Exception}");
            ContentFrame.Content = new Grid
            {
                Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 5, 9, 12)),
                Children =
                {
                    new StackPanel
                    {
                        Margin = new Thickness(36),
                        Spacing = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            new TextBlock { Text = "This page couldn't be opened", FontSize = 26, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White) },
                            new TextBlock { Text = e.Exception?.Message ?? "Unknown navigation error", FontSize = 16, TextWrapping = TextWrapping.Wrap, Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 210, 220, 225)) },
                            new TextBlock { Text = e.SourcePageType?.FullName ?? "Unknown page", FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 120, 140, 150)) }
                        }
                    }
                }
            };
            e.Handled = true;
        }

        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().BackRequested -= System_BackRequested;
            _authService.SessionChanged -= AuthService_SessionChanged;
            ContentFrame.Navigated -= ContentFrame_Navigated;
        }

        private async Task RefreshProfileAsync()
        {
            await _authService.InitializeAsync();
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, UpdateProfileUi);
        }

        private async void AuthService_SessionChanged(object? sender, EventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, UpdateProfileUi);
        }

        private void UpdateProfileUi()
        {
            var displayName = "Sign In";
            if (_authService.IsLoggedIn)
            {
                displayName = _authService.User?.Name?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    var email = _authService.User?.Email?.Trim();
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        var separator = email.IndexOf('@');
                        displayName = separator > 0 ? email.Substring(0, separator) : email;
                    }
                    else
                    {
                        displayName = "Stremio";
                    }
                }
            }

            ProfileNameText.Text = displayName;
            FlyoutProfileNameText.Text = displayName;
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
