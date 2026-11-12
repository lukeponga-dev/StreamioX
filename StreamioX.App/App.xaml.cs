// Purpose: Bootstraps the UWP application, configures services, and owns application-level lifecycle events.

using StreamioX.App.Services;
using StreamioX.App.Views;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StreamioX.App
{
    sealed partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();

            this.RequiresPointerMode = ApplicationRequiresPointerMode.WhenRequested;
            this.Suspending += OnSuspending;
            this.Resuming += OnResuming;

            AppServices.Configure();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            // Create and touch the shared MediaPlayer before navigating to content.
            // MediaPlayer/XAML setup stays on the UI thread; network startup remains asynchronous.
            AppServices.GetRequiredService<PlaybackSessionService>().WarmUp();

            // Warm up Stremio addon registry (installs Cinemeta on first run)
            _ = AppServices.GetRequiredService<IAuthService>().InitializeAsync();
            _ = AppServices.GetRequiredService<IAddonService>().InitializeAsync();

            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    // TODO: Load state from previously suspended application
                }

                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                rootFrame.Navigate(typeof(MainPage), e.Arguments);
            }

            Window.Current.Activate();

            // Start the embedded torrent HTTP server in the background so it is
            // ready before the user selects a torrent stream.
            _ = StartTorrentServerAsync("app launch");
        }

        /// <summary>Starts the local torrent HTTP bridge and records startup failures for diagnostics.</summary>
        private async Task StartTorrentServerAsync(string reason)
        {
            try
            {
                var address = await AppServices.GetRequiredService<ITorrentBridgeService>()
                    .StartServerAsync();
                PlaybackDiagnostics.Write($"[Startup] Torrent HTTP server ready at {address} ({reason}).");
            }
            catch (Exception ex)
            {
                // Keep the app usable if the native bridge is unavailable. A later
                // playback request can retry startup and Settings can show diagnostics.
                PlaybackDiagnostics.Write(
                    $"[Startup] Torrent HTTP server failed to start ({reason}): {ex.GetType().Name}, HRESULT 0x{ex.HResult:X8}: {ex.Message}");
            }
        }

        private void OnResuming(object sender, object e)
        {
            // The server is stopped during suspension, so bring it back on resume.
            _ = StartTorrentServerAsync("app resume");
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>Persists application state and releases resources before the app is suspended.</summary>
        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            try
            {
                await AppServices.GetRequiredService<ITorrentBridgeService>().StopAsync();
            }
            catch
            {
                // The app is suspending; native server cleanup is best-effort.
            }
            finally
            {
                deferral.Complete();
            }
        }
    }
}
