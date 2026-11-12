using System;
using System.Threading.Tasks;
using StreamioX.App.Navigation;
using StreamioX.App.ViewModels;
using StreamioX.App.Services;
using StreamioX.App.Layout;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace StreamioX.App.Views
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; }
        private readonly INavigationService _navigationService;
        private readonly IAuthService _authService;

        private static readonly SolidColorBrush NavSelectedBackground =
            new SolidColorBrush(Color.FromArgb(255, 35, 27, 57));
        private static readonly SolidColorBrush NavIdleBackground =
            new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        private static readonly SolidColorBrush NavSelectedForeground =
            new SolidColorBrush(Color.FromArgb(255, 224, 211, 255));
        private static readonly SolidColorBrush NavIdleForeground =
            new SolidColorBrush(Color.FromArgb(255, 167, 169, 184));

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
            SetActiveNavigationItem("Home");
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().BackRequested += System_BackRequested;
            _authService.SessionChanged -= AuthService_SessionChanged;
            _authService.SessionChanged += AuthService_SessionChanged;
            if (ContentFrame.Content == null) ViewModel.NavigateToHomeCommand.Execute(null);
            UpdatePlaybackChrome(ContentFrame.Content is PlayerPage);
            _ = RefreshProfileAsync();
            if (ContentFrame.Content is not PlayerPage)
                HomeNavButton.Focus(FocusState.Programmatic);
        }

        private void ContentFrame_Navigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            UpdatePlaybackChrome(e.Content is PlayerPage);
            var navigationTag = GetNavigationTag(e.Content);
            if (navigationTag != null) SetActiveNavigationItem(navigationTag);
        }

        private static string? GetNavigationTag(object? page)
        {
            if (page is HomePage) return "Home";
            if (page is DiscoverPage) return "Discover";
            if (page is LibraryPage) return "Library";
            if (page is SearchPage) return "Search";
            if (page is AddonsPage) return "Addons";
            return null;
        }

        private void SetActiveNavigationItem(string? activeTag)
        {
            SetNavigationButton(HomeNavButton, HomeNavIndicator, activeTag == "Home");
            SetNavigationButton(DiscoverNavButton, DiscoverNavIndicator, activeTag == "Discover");
            SetNavigationButton(LibraryNavButton, LibraryNavIndicator, activeTag == "Library");
            SetNavigationButton(SearchNavButton, SearchNavIndicator, activeTag == "Search");
            SetNavigationButton(AddonsNavButton, AddonsNavIndicator, activeTag == "Addons");
        }

        private static void SetNavigationButton(Button button, Border indicator, bool isActive)
        {
            button.Background = isActive ? NavSelectedBackground : NavIdleBackground;
            button.Foreground = isActive ? NavSelectedForeground : NavIdleForeground;
            indicator.Opacity = isActive ? 1 : 0;
        }

        private void UpdatePlaybackChrome(bool isPlayingPage)
        {
            TopBar.Visibility = isPlayingPage ? Visibility.Collapsed : Visibility.Visible;
            RootLayout.Padding = isPlayingPage ? new Thickness(0) : XboxConstants.SafeArea;
        }

        private void ContentFrame_NavigationFailed(object sender, Windows.UI.Xaml.Navigation.NavigationFailedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[StreamioX] Page navigation failed: {e.SourcePageType?.FullName}: {e.Exception}");
            ContentFrame.Content = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 5, 6, 10)),
                Children =
                {
                    new StackPanel
                    {
                        Margin = new Thickness(36),
                        Spacing = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            new TextBlock { Text = "This page couldn't be opened", FontSize = 26, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White) },
                            new TextBlock { Text = e.Exception?.Message ?? "Unknown navigation error", FontSize = 16, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromArgb(255, 210, 220, 225)) },
                            new TextBlock { Text = e.SourcePageType?.FullName ?? "Unknown page", FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromArgb(255, 120, 140, 150)) }
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
            ContentFrame.NavigationFailed -= ContentFrame_NavigationFailed;
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
            ProfileSignInButton.Content = _authService.IsLoggedIn ? "Sign Out" : "Sign In";
        }

        private void System_BackRequested(object sender, BackRequestedEventArgs e)
        {
            if (ContentFrame.CanGoBack) { e.Handled = true; ContentFrame.GoBack(); }
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string tag) return;
            switch (tag)
            {
                case "Home": ViewModel.NavigateToHomeCommand.Execute(null); break;
                case "Discover": ViewModel.NavigateToDiscoverCommand.Execute(null); break;
                case "Search": ViewModel.NavigateToSearchCommand.Execute(null); break;
                case "Library": ViewModel.NavigateToLibraryCommand.Execute(null); break;
                case "Addons": ViewModel.NavigateToAddonsCommand.Execute(null); break;
            }
        }

        private async void ProfileSignIn_Click(object sender, RoutedEventArgs e)
        {
            if (_authService.IsLoggedIn)
            {
                await _authService.LogoutAsync();
                UpdateProfileUi();
                return;
            }

            // SettingsPage contains the existing Stremio device-link and account sign-in UI.
            ViewModel.NavigateToSettingsCommand.Execute(null);
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
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}