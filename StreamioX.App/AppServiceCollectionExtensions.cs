// Purpose: Keeps application DI registration independently testable without starting the Xbox app.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StreamioX.App.Navigation;
using StreamioX.App.Services;
using StreamioX.App.ViewModels;
using System;
using System.Net.Http;

namespace StreamioX.App
{
    /// <summary>Configuration shared by the app's HTTP-backed services.</summary>
    public sealed class StreamioXOptions
    {
        public int RequestTimeoutSeconds { get; set; } = 30;
        public string UserAgent { get; set; } = "StreamioX/1.0 (Xbox; UWP)";
    }

    /// <summary>Registers application services without constructing platform or native resources.</summary>
    public static class AppServiceCollectionExtensions
    {
        public static IServiceCollection AddStreamioXServices(
            this IServiceCollection services,
            IConfiguration? configuration = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddOptions<StreamioXOptions>();
            if (configuration != null)
            {
                services.Configure<StreamioXOptions>(options =>
                {
                    var section = configuration.GetSection("StreamioX");
                    if (int.TryParse(section["RequestTimeoutSeconds"], out var timeout) && timeout > 0)
                        options.RequestTimeoutSeconds = timeout;
                    if (!string.IsNullOrWhiteSpace(section["UserAgent"]))
                        options.UserAgent = section["UserAgent"]!;
                });
            }

            services.AddHttpClient<StremioClient>((provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<StreamioXOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
            });

            // Auth and device-link APIs share a single configured client.
            services.AddSingleton(provider =>
            {
                var options = provider.GetRequiredService<IOptions<StreamioXOptions>>().Value;
                var client = new HttpClient { Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
                return client;
            });

            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IAuthService, AuthService>();
            services.AddSingleton<IAddonService, AddonService>();
            services.AddSingleton<ILibraryService, LibraryService>();
            services.AddSingleton<ICatalogService, CatalogService>();
            services.AddSingleton<IMetadataService, MetadataService>();
            services.AddSingleton<IStreamService, StreamService>();
            services.AddSingleton<ITorrentBridgeService, TorrentBridgeService>();
            services.AddSingleton<FFmpegPlaybackService>();
            services.AddSingleton<PlaybackSessionService>();

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

            return services;
        }
    }
}
