
using System;
using System.Diagnostics;
using StreamioX.App.Views;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StreamioX.App
{
    /// <summary>
    /// Provides application-specific behavior to supplement
    /// the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();

            // Use directional navigation for Xbox instead of mouse/keyboard.
            this.RequiresPointerMode =
                ApplicationRequiresPointerMode.WhenRequested;

            this.Suspending += OnSuspending;

            // Log unhandled exceptions for debugging.
            this.UnhandledException += OnUnhandledException;
        }

        /// <summary>
        /// Invoked when the application is launched normally.
        /// </summary>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // Reuse the existing frame when possible.
            if (rootFrame == null)
            {
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState ==
                    ApplicationExecutionState.Terminated)
                {
                    // TODO: Restore previously saved application state.
                }

                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                rootFrame.Navigate(typeof(MainPage), e.Arguments);
            }

            Window.Current.Activate();
        }

        private void OnNavigationFailed(
            object sender,
            NavigationFailedEventArgs e)
        {
            Debug.WriteLine("[NAVIGATION FAILED]");
            Debug.WriteLine($"Page: {e.SourcePageType.FullName}");
            Debug.WriteLine($"Exception: {e.Exception}");

            throw new Exception(
                "Failed to load Page " + e.SourcePageType.FullName,
                e.Exception
            );
        }

        private void OnUnhandledException(
            object sender,
            Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Debug.WriteLine("==================================");
            Debug.WriteLine("[APPLICATION UNHANDLED EXCEPTION]");
            Debug.WriteLine($"Message: {e.Message}");
            Debug.WriteLine($"Exception: {e.Exception}");
            Debug.WriteLine($"Stack trace: {e.Exception?.StackTrace}");
            Debug.WriteLine($"Inner exception: {e.Exception?.InnerException}");
            Debug.WriteLine("==================================");

            // Leave e.Handled false while diagnosing.
        }

        private void OnSuspending(
            object sender,
            SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            try
            {
                // TODO: Save application state and stop background activity.
            }
            finally
            {
                deferral.Complete();
            }
        }
    }
}
