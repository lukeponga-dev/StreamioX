using Microsoft.Extensions.DependencyInjection;
using StreamioX.App.Navigation;
using StreamioX.App.Services;
using StreamioX.App.ViewModels;
using System;
using System.Net.Http;

namespace StreamioX.App
{
    public static class AppServices
    {
        private static IServiceProvider? _provider;

        public static IServiceProvider Provider =>
            _provider ?? throw new InvalidOperationException("Services not configured. Call Configure() first.");

        public static void Configure()
        {
            if (_provider != null) return;

            var services = new ServiceCollection();

            services.AddHttpClient<StremioClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("StreamioX/1.0 (Xbox; UWP)");
            });

            // Shared HttpClient for auth / link API
            services.AddSingleton<HttpClient>(_ =>
            {
                var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                c.DefaultRequestHeaders.UserAgent.ParseAdd("StreamioX/1.0 (Xbox; UWP)");
                return c;
            });

            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IAuthService, AuthService>();
            services.AddSingleton<IAddonService, AddonService>();
            services.AddSingleton<ICatalogService, CatalogService>();
            services.AddSingleton<IStreamService, StreamService>();

            services.AddTransient<MainViewModel>();
            services.AddTransient<DiscoverViewModel>();
            services.AddTransient<DetailsViewModel>();
            services.AddTransient<HomeViewModel>();
            services.AddTransient<PlayerViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<LibraryViewModel>();
            services.AddTransient<AddonsViewModel>();
            services.AddTransient<SearchViewModel>();
            services.AddTransient<DiagnosticsViewModel>();

            _provider = services.BuildServiceProvider();
        }

        public static T GetRequiredService<T>() where T : notnull =>
            Provider.GetRequiredService<T>();
    }
}
