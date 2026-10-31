// Navigation/NavigationService.cs
using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using StreamioX.App.Views;

namespace StreamioX.App.Navigation
{
    public class NavigationService : INavigationService
    {
        private Frame _shellFrame;
        private readonly Dictionary<NavigationRoute, Type> _routeRegistry;

        public NavigationService()
        {
            _routeRegistry = new Dictionary<NavigationRoute, Type>
            {
                { NavigationRoute.Home, typeof(HomePage) },
                { NavigationRoute.Discover, typeof(DiscoverPage) },
                { NavigationRoute.Library, typeof(LibraryPage) },
                { NavigationRoute.Search, typeof(SearchPage) },
                { NavigationRoute.Addons, typeof(AddonsPage) },
                { NavigationRoute.Settings, typeof(SettingsPage) },
                { NavigationRoute.Details, typeof(DetailsPage) },
                { NavigationRoute.Player, typeof(PlayerPage) },
                { NavigationRoute.Diagnostics, typeof(DiagnosticsPage) }
            };
        }

        public void Initialize(Frame shellFrame)
        {
            _shellFrame = shellFrame ?? throw new ArgumentNullException(nameof(shellFrame));
        }

        public bool NavigateTo(NavigationRoute route, object parameter = null)
        {
            if (_shellFrame == null) return false;

            if (_routeRegistry.TryGetValue(route, out Type pageType))
            {
                if (_shellFrame.Content?.GetType() == pageType) return false;
                return _shellFrame.Navigate(pageType, parameter);
            }
            return false;
        }

        public bool Navigate<TViewModel>(object parameter = null)
        {
            throw new NotImplementedException("Use NavigateTo(NavigationRoute) for V1 architecture.");
        }

        public bool GoBack()
        {
            if (_shellFrame != null && _shellFrame.CanGoBack)
            {
                _shellFrame.GoBack();
                return true;
            }
            return false;
        }
    }
}