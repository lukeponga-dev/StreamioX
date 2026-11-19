// Purpose: Bootstraps the UWP application, configures services, and owns application-level lifecycle events.

using StreamioX.App.Services;
using StreamioX.App.Views;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.System.Profile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StreamioX.App
{
    sealed partial class App : Application
    {
        public App()
        {
            AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                StartupDiagnostics.Write(
                    "UNOBSERVED TASK EXCEPTION",
                    e.Exception);

                e.SetObserved();
            };

            try
            {
                StartupDiagnostics.Write("App constructor: before InitializeComponent");
                InitializeComponent();

                UnhandledException += OnUnhandledException;

                StartupDiagnostics.Write(
                    "App constructor: InitializeComponent completed; exception diagnostics attached");
                // RequiresPointerMode is only supported by the Xbox device family.
                // Setting it on desktop Windows can fail during UWP activation with E_NOINTERFACE.
                if (AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Xbox")
                {
                    RequiresPointerMode = ApplicationRequiresPointerMode.WhenRequested;
                }
                Suspending += OnSuspending;
                Resuming += OnResuming;

                StartupDiagnostics.Write("App constructor: before AppServices.Configure");
                AppServices.Configure();
                StartupDiagnostics.Write("App constructor: AppServices.Configure completed");
            }
            catch (Exception ex)
            {
                StartupDiagnostics.Write("App constructor FAILED", ex);
                throw;
            }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            try
            {
                StartupDiagnostics.Write("OnLaunched: entered");
                Frame? rootFrame = Window.Current.Content as Frame;

                if (rootFrame == null)
                {
                    StartupDiagnostics.Write("OnLaunched: creating root Frame");
                    rootFrame = new Frame();
                    rootFrame.NavigationFailed += OnNavigationFailed;
                    Window.Current.Content = rootFrame;
                }

                if (rootFrame.Content == null)
                {
                    StartupDiagnostics.Write("OnLaunched: before MainPage navigation");
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                    StartupDiagnostics.Write("OnLaunched: MainPage navigation returned");
                }

                Window.Current.Activate();
                StartupDiagnostics.Write("OnLaunched: Window activated");
            }
            catch (Exception ex)
            {
                StartupDiagnostics.Write("OnLaunched FAILED", ex);
                throw;
            }
        }

        private void OnFirstChanceException(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            // Keep the log focused: first-chance events can be very noisy in XAML apps.
            if (e.Exception.HResult == unchecked((int)0x80004002) ||
                e.Exception is System.Runtime.InteropServices.COMException)
            {
                StartupDiagnostics.Write(
                    $"FIRST-CHANCE COM/WinRT exception (0x{e.Exception.HResult:X8})",
                    e.Exception);
            }
        }

        private void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // Observe only. Do not mark the exception handled or hide the crash.
            StartupDiagnostics.Write(
                $"XAML UNHANDLED EXCEPTION (0x{e.Exception.HResult:X8}; handled={e.Handled})",
                e.Exception);
        }

        private void OnResuming(object? sender, object e)
        {
            // Playback starts the torrent bridge on demand; resume needs no background work.
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            StartupDiagnostics.Write("Root frame navigation FAILED",
                new Exception($"Failed to load Page {e.SourcePageType?.FullName}", e.Exception));
            throw new Exception("Failed to load Page " + (e.SourcePageType?.FullName ?? "<unknown>"), e.Exception);
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
