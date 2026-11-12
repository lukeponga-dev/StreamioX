// Purpose: Defines NavigationService navigation contracts and route resolution for the application.

using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;
using StreamioX.App.Views;

namespace StreamioX.App.Navigation
{
    public sealed class NavigationService : INavigationService
    {
        private Frame? _shellFrame;

        private readonly Dictionary<NavigationRoute, Type> _routeRegistry =
            new()
            {
                [NavigationRoute.Home] = typeof(HomePage),
                [NavigationRoute.Discover] = typeof(DiscoverPage),
                [NavigationRoute.Library] = typeof(LibraryPage),
                [NavigationRoute.Search] = typeof(SearchPage),
                [NavigationRoute.Addons] = typeof(AddonsPage),
                [NavigationRoute.Settings] = typeof(SettingsPage),
                [NavigationRoute.Details] = typeof(DetailsPage),
                [NavigationRoute.Player] = typeof(PlayerPage),
                [NavigationRoute.Diagnostics] = typeof(DiagnosticsPage)
            };

        public void Initialize(Frame shellFrame)
        {
            _shellFrame = shellFrame
                          ?? throw new ArgumentNullException(nameof(shellFrame));
        }

        /// <summary>Resolves a route to its page type and navigates the shell frame with optional navigation data.</summary>
        public bool NavigateTo(
            NavigationRoute route,
            object? parameter = null)
        {
            if (_shellFrame == null)
                return false;

            if (!_routeRegistry.TryGetValue(route, out Type? pageType))
                return false;

            if (_shellFrame.Content?.GetType() == pageType)
                return false;

            return _shellFrame.Navigate(pageType, parameter);
        }

        /// <summary>Navigates to the previous page when the frame has a back entry.</summary>
        public bool GoBack()
        {
            if (_shellFrame == null || !_shellFrame.CanGoBack)
                return false;

            _shellFrame.GoBack();
            return true;
        }
    }
}
