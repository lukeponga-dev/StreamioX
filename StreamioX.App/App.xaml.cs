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

            AppServices.Configure();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
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
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}
