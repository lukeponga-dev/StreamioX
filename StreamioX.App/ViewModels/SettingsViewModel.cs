// Purpose: Owns SettingsViewModel screen state and user actions, keeping presentation logic separate from the XAML view.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Services;
using StreamioX.App.Navigation;
using System.Net;
using System.Net.Http;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace StreamioX.App.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly ITorrentBridgeService _torrentBridge;
        private readonly INavigationService _navigation;
        private readonly HttpClient _httpClient;
        private DispatcherTimer? _telemetryTimer;
        private CancellationTokenSource? _pollCts;
        private bool _isInitialized;

        // Partial properties allow CsWinRT to generate the required WinRT marshalling
        // metadata for XAML-bound properties in NativeAOT builds.
        [ObservableProperty] public partial string AppVersionDisplay { get; set; } = "Version 1.0.0-alpha";
        [ObservableProperty] public partial string CurrentMemoryUsage { get; set; } = "—";
        [ObservableProperty] public partial string SystemHardLimit { get; set; } = "—";
        [ObservableProperty] public partial double MemoryPercentage { get; set; }
        [ObservableProperty] public partial bool HardwareAccelerationEnabled { get; set; } = true;
        [ObservableProperty] public partial bool IsLoggedIn { get; set; }
        [ObservableProperty] public partial string AccountLabel { get; set; } = "Not signed in";
        [ObservableProperty] public partial string? LinkCode { get; set; }
        [ObservableProperty] public partial string? LinkUrl { get; set; }
        [ObservableProperty] public partial BitmapImage? QrImage { get; set; }
        [ObservableProperty] public partial bool IsLinking { get; set; }
        [ObservableProperty] public partial string? AuthStatusMessage { get; set; }
        [ObservableProperty] public partial string ExtensionsSyncStatus { get; set; } = "Sign in to synchronize your extensions across devices.";
        [ObservableProperty] public partial bool IsTorrentServerRunning { get; set; }
        [ObservableProperty] public partial string TorrentServerAddress { get; set; } = "Not running";
        [ObservableProperty] public partial string TorrentServerStatus { get; set; } = "The local HTTP server is stopped.";
        [ObservableProperty] public partial string PlaybackServerAddress { get; set; } = string.Empty;
        [ObservableProperty] public partial string PlaybackServerStatus { get; set; } = "Enter a direct media URL from your server.";
        [ObservableProperty] public partial bool IsConnectingToPlaybackServer { get; set; }
        [ObservableProperty] public partial string? EmailInput { get; set; }
        [ObservableProperty] public partial string? PasswordInput { get; set; }
        [ObservableProperty] public partial string? AuthKeyInput { get; set; }

        /// <summary>Initializes a new instance of <see cref="SettingsViewModel"/> with required services.</summary>
        public SettingsViewModel(IAuthService authService, ITorrentBridgeService torrentBridge, INavigationService navigation, HttpClient httpClient)
        {
            _authService = authService;
            _torrentBridge = torrentBridge;
            _navigation = navigation;
            _httpClient = httpClient;
        }

        /// <summary>Initializes platform-backed settings and telemetry after the page enters the app lifecycle.</summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            PlaybackServerAddress = ApplicationData.Current.LocalSettings.Values["PlaybackServerAddress"] as string ?? string.Empty;
            RefreshTorrentServerState();
            StartTelemetry();
            await InitAuthAsync();
        }

        /// <summary>Stops telemetry and cancels active device-link polling when leaving Settings without affecting playback.</summary>
        public void Deactivate()
        {
            if (_telemetryTimer is not null)
            {
                _telemetryTimer.Stop();
                _telemetryTimer = null;
            }

            var pollCts = _pollCts;
            _pollCts = null;
            if (pollCts is not null)
            {
                try
                {
                    if (!pollCts.IsCancellationRequested)
                    {
                        pollCts.Cancel();
                    }
                    pollCts.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Safe to ignore if already disposed when polling finishes.
                }
            }

            IsLinking = false;
        }

        private async Task InitAuthAsync()
        {
            await _authService.InitializeAsync();
            UpdateAccountUi();
        }

        private void UpdateAccountUi()
        {
            IsLoggedIn = _authService.IsLoggedIn;
            AccountLabel = IsLoggedIn
                ? (_authService.User?.Email ?? "Signed in")
                : "Not signed in";
            ExtensionsSyncStatus = _authService.LastExtensionSyncMessage;
        }

        [RelayCommand]
        /// <summary>Runs the QR or browser device-link flow, polls approval, and refreshes account UI state.</summary>
        private Task StartQrLoginAsync() => RunDeviceLinkLoginAsync(openBrowser: false);

        [RelayCommand]
        /// <summary>Runs the QR or browser device-link flow, polls approval, and refreshes account UI state.</summary>
        private Task LoginWithFacebookAsync() => RunDeviceLinkLoginAsync(openBrowser: true);

        // Authentication is completed through an external account provider.
        // The app only receives a device authorization token.
        private async Task RunDeviceLinkLoginAsync(bool openBrowser)
        {
            _pollCts?.Cancel();
            var pollCts = new CancellationTokenSource();
            _pollCts = pollCts;
            var token = pollCts.Token;

            IsLinking = true;
            AuthStatusMessage = openBrowser
                ? "Preparing secure sign-in…"
                : "Creating link code…";
            QrImage = null;
            LinkCode = null;

            try
            {
                var session = await _authService.StartDeviceLinkAsync(token);
                LinkCode = session.Code;
                LinkUrl = session.LinkUrl;
                QrImage = new BitmapImage(new Uri(session.QrImageUrl));
                AuthStatusMessage = openBrowser
                    ? "Complete Facebook sign-in in the browser. Return here when finished."
                    : $"Scan the QR code or open the displayed link and enter code {session.Code}";
                if (openBrowser)
                {
                    var launched = await Launcher.LaunchUriAsync(new Uri(session.LinkUrl));
                    if (!launched)
                        AuthStatusMessage = "Could not open the sign-in page. Scan the QR code or open the link manually.";
                }

                // The same device-link poll handles email, Facebook and other methods
                // offered by the external authentication provider.
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(2500, token);
                    var ok = await _authService.PollDeviceLinkAsync(session.Code, token);
                    if (ok)
                    {
                        AuthStatusMessage = "Signed in successfully.";
                        UpdateAccountUi();
                        IsLinking = false;
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                AuthStatusMessage = "Link cancelled.";
            }
            catch (Exception ex)
            {
                AuthStatusMessage = $"Sign-in failed: {ex.Message}";
            }
            finally
            {
                IsLinking = false;
                if (ReferenceEquals(_pollCts, pollCts))
                    _pollCts = null;
                pollCts.Dispose();
            }
        }

        [RelayCommand]
        private void CancelQrLogin()
        {
            _pollCts?.Cancel();
            IsLinking = false;
            AuthStatusMessage = "Cancelled.";
        }

        [RelayCommand]
        private async Task LoginEmailAsync()
        {
            if (string.IsNullOrWhiteSpace(EmailInput) || string.IsNullOrWhiteSpace(PasswordInput))
            {
                AuthStatusMessage = "Enter email and password.";
                return;
            }

            AuthStatusMessage = "Signing in…";
            try
            {
                await _authService.LoginWithEmailAsync(EmailInput.Trim(), PasswordInput);
                PasswordInput = string.Empty;
                AuthStatusMessage = "Signed in.";
                UpdateAccountUi();
            }
            catch (Exception ex)
            {
                AuthStatusMessage = $"Login failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task LoginAuthKeyAsync()
        {
            if (string.IsNullOrWhiteSpace(AuthKeyInput))
            {
                AuthStatusMessage = "Paste an auth key.";
                return;
            }

            try
            {
                await _authService.LoginWithAuthKeyAsync(AuthKeyInput.Trim());
                AuthKeyInput = string.Empty;
                AuthStatusMessage = "Signed in with auth key.";
                UpdateAccountUi();
            }
            catch (Exception ex)
            {
                AuthStatusMessage = $"Auth key failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task SyncExtensionsAsync()
        {
            if (!_authService.IsLoggedIn)
            {
                ExtensionsSyncStatus = "Sign in before synchronizing account extensions.";
                return;
            }
            ExtensionsSyncStatus = "Synchronizing account extensions…";
            try
            {
                var result = await _authService.SyncExtensionsCollectionAsync();
                ExtensionsSyncStatus = result.Message;
            }
            catch (Exception ex)
            {
                ExtensionsSyncStatus = $"Extension synchronization failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task LogoutAsync()
        {
            var pollCts = _pollCts;
            _pollCts = null;

            pollCts?.Cancel();
            pollCts?.Dispose();
            await _authService.LogoutAsync();
            UpdateAccountUi();
            QrImage = null;
            LinkCode = null;
            AuthStatusMessage = "Signed out.";
        }

        [RelayCommand]
        /// <summary>Starts the local playback bridge and updates the connection status shown in settings.</summary>
        private async Task ConnectPlaybackServerAsync()
        {
            var address = PlaybackServerAddress?.Trim();
            if (string.IsNullOrWhiteSpace(address) || !Uri.TryCreate(address, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                PlaybackServerStatus = "Enter a valid HTTP or HTTPS media URL, including the file or stream path.";
                return;
            }

            IsConnectingToPlaybackServer = true;
            PlaybackServerStatus = "Checking server and media response…";
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 1);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if (response.StatusCode == HttpStatusCode.PartialContent || response.IsSuccessStatusCode)
                {
                    ApplicationData.Current.LocalSettings.Values["PlaybackServerAddress"] = uri.AbsoluteUri;
                    PlaybackServerAddress = uri.AbsoluteUri;
                    PlaybackServerStatus = $"Connected ({(int)response.StatusCode} {response.ReasonPhrase}). Opening player…";
                    _navigation.NavigateTo(NavigationRoute.Player, uri.AbsoluteUri);
                }
                else
                {
                    PlaybackServerStatus = $"Server responded with {(int)response.StatusCode} {response.ReasonPhrase}. Check the URL and server access.";
                }
            }
            catch (Exception ex)
            {
                PlaybackServerStatus = $"Connection failed: {ex.Message}";
            }
            finally
            {
                IsConnectingToPlaybackServer = false;
            }
        }

        private void RefreshTorrentServerState()
        {
            IsTorrentServerRunning = _torrentBridge.IsRunning;
            TorrentServerAddress = _torrentBridge.ServerAddress ?? "Not running";
            TorrentServerStatus = IsTorrentServerRunning
                ? "HTTP range streaming is ready. Video seeks are translated into torrent piece reads."
                : "Start the local server to enable torrent-backed HTTP playback.";
        }

        [RelayCommand]
        private async Task StartTorrentServerAsync()
        {
            TorrentServerStatus = "Starting embedded torrent engine…";
            try
            {
                var address = await _torrentBridge.StartServerAsync();
                IsTorrentServerRunning = true;
                TorrentServerAddress = address;
                TorrentServerStatus = "Running. Supports HTTP byte ranges (206 Partial Content), seeking and on-demand torrent piece delivery.";
            }
            catch (Exception ex)
            {
                IsTorrentServerRunning = false;
                TorrentServerAddress = "Not running";
                TorrentServerStatus = $"Could not start HTTP torrent server: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task StopTorrentServerAsync()
        {
            try
            {
                await _torrentBridge.StopAsync();
                IsTorrentServerRunning = false;
                TorrentServerAddress = "Not running";
                TorrentServerStatus = "HTTP torrent server stopped.";
            }
            catch (Exception ex)
            {
                TorrentServerStatus = $"Could not stop HTTP torrent server: {ex.Message}";
            }
        }

        private void StartTelemetry()
        {
            _telemetryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _telemetryTimer.Tick += (_, _) => UpdateMemoryStats();
            _telemetryTimer.Start();
            UpdateMemoryStats();
        }

        private void UpdateMemoryStats()
        {
            double usageMB = MemoryManager.AppMemoryUsage / 1024.0 / 1024.0;
            double limitMB = MemoryManager.AppMemoryUsageLimit / 1024.0 / 1024.0;
            CurrentMemoryUsage = $"{usageMB:F2} MB";
            SystemHardLimit = $"{limitMB:F2} MB";
            MemoryPercentage = limitMB > 0 ? (usageMB / limitMB) * 100 : 0;
        }

        /// <summary>
        /// Deletes cached image files from the local ImageCache folder.
        /// </summary>
        [RelayCommand]
        private async Task ClearImageCacheAsync()
        {
            try
            {
                var cacheFolder = await ApplicationData.Current.LocalFolder
                    .CreateFolderAsync("ImageCache", CreationCollisionOption.OpenIfExists);

                var files = await cacheFolder.GetFilesAsync();

                foreach (var file in files)
                {
                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }

                AuthStatusMessage =
                    $"Cleared {files.Count} cached image{(files.Count == 1 ? string.Empty : "s")}.";
            }
            catch (Exception ex)
            {
                AuthStatusMessage = $"Failed to clear image cache: {ex.Message}";
            }
        }
    }
}