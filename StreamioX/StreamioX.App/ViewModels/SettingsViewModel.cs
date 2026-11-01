using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading;
using System.Threading.Tasks;
using StreamioX.App.Services;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace StreamioX.App.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
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
        private string? emailInput;

        [ObservableProperty]
        private string? passwordInput;

        [ObservableProperty]
        private string? authKeyInput;

        public SettingsViewModel(IAuthService authService)
        {
            _authService = authService;
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
