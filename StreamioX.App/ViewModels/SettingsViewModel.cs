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

        [ObservableProperty]
        private string appVersionDisplay = "Version 1.0.0-alpha";

        [ObservableProperty]
        private string currentMemoryUsage = "—";

        [ObservableProperty]
        private string systemHardLimit = "—";

        [ObservableProperty]
        private double memoryPercentage;

        [ObservableProperty]
        private bool hardwareAccelerationEnabled = true;

        [ObservableProperty]
        private bool isLoggedIn;

        [ObservableProperty]
        private string accountLabel = "Not signed in";

        [ObservableProperty]
        private string? linkCode;

        [ObservableProperty]
        private string? linkUrl;

        [ObservableProperty]
        private BitmapImage? qrImage;

        [ObservableProperty]
        private bool isLinking;

        [ObservableProperty]
        private string? authStatusMessage;

        [ObservableProperty]
        private bool isTorrentServerRunning;

        [ObservableProperty]
        private string torrentServerAddress = "Not running";

        [ObservableProperty]
        private string torrentServerStatus = "The local HTTP server is stopped.";

        [ObservableProperty]
        private string playbackServerAddress = string.Empty;

        [ObservableProperty]
        private string playbackServerStatus = "Enter a direct media URL from your server.";

        [ObservableProperty]
        private bool isConnectingToPlaybackServer;

        [ObservableProperty]
        private string? emailInput;

        [ObservableProperty]
        private string? passwordInput;

        [ObservableProperty]
        private string? authKeyInput;

        public SettingsViewModel(IAuthService authService, ITorrentBridgeService torrentBridge, INavigationService navigation, HttpClient httpClient)
        {
            _authService = authService;
            _torrentBridge = torrentBridge;
            _navigation = navigation;
            _httpClient = httpClient;
            PlaybackServerAddress = ApplicationData.Current.LocalSettings.Values["PlaybackServerAddress"] as string ?? string.Empty;
            RefreshTorrentServerState();
            StartTelemetry();
            _ = InitAuthAsync();
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
        }

        [RelayCommand]
        private async Task StartQrLoginAsync()
        {
            _pollCts?.Cancel();
            _pollCts = new CancellationTokenSource();
            var token = _pollCts.Token;

            IsLinking = true;
            AuthStatusMessage = "Creating link code…";
            QrImage = null;
            LinkCode = null;

            try
            {
                var session = await _authService.StartDeviceLinkAsync(token);
                LinkCode = session.Code;
                LinkUrl = session.LinkUrl;
                QrImage = new BitmapImage(new Uri(session.QrImageUrl));
                AuthStatusMessage = $"Scan QR or open link.stremio.com and enter code {session.Code}";

                // Poll until linked or cancelled
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
                AuthStatusMessage = $"Link failed: {ex.Message}";
            }
            finally
            {
                IsLinking = false;
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
        private async Task LogoutAsync()
        {
            _pollCts?.Cancel();
            await _authService.LogoutAsync();
            UpdateAccountUi();
            QrImage = null;
            LinkCode = null;
            AuthStatusMessage = "Signed out.";
        }

        [RelayCommand]
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

        [RelayCommand]
        private void ClearImageCache()
        {
            // TODO
        }
    }
}
