// Purpose: Bootstraps the UWP application, configures services, and owns application-level lifecycle events.

using StreamioX.App.Services;
using StreamioX.App.Views;
using System;
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
            // Defer media-pipeline and add-on initialization until those features are
            // actually opened. Starting them here competes with first-page rendering.
            Frame? rootFrame = Window.Current.Content as Frame;

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

            // TorrentBridgeService starts on demand when a torrent stream is selected.
        }

        private void OnResuming(object? sender, object e)
        {
            // Playback starts the torrent bridge on demand; resume needs no background work.
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
