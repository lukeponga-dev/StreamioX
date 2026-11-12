// Purpose: Composes the dependency-injection container and registers app services, view models, and navigation.

using Microsoft.Extensions.DependencyInjection;
using StreamioX.App.Navigation;
using StreamioX.App.Services;
using StreamioX.App.ViewModels;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Storage;

namespace StreamioX.App
{
    /// <summary>
    /// P/Invoke surface for the native bridge ABI currently exported by
    /// streamio_torrent_bridge.dll. The native library exports start/stop/last_error;
    /// it does not currently export the proposed init_engine/free_string symbols.
    /// </summary>
    internal static unsafe partial class NativeBridge
    {
        private const string NativeLibrary = "streamio_torrent_bridge";

        [LibraryImport(NativeLibrary, EntryPoint = "streamio_torrent_bridge_start")]
        internal static partial int Start(byte* configPath, byte* cachePath, ushort requestedPort, ushort* actualPort);

        [LibraryImport(NativeLibrary, EntryPoint = "streamio_torrent_bridge_stop")]
        internal static partial int Stop();

        [LibraryImport(NativeLibrary, EntryPoint = "streamio_torrent_bridge_last_error")]
        private static partial nuint LastError(byte* buffer, nuint capacity);

        internal static string ReadLastError()
        {
            var buffer = new byte[2048];
            fixed (byte* pointer = buffer)
                LastError(pointer, (nuint)buffer.Length);

            var length = Array.IndexOf(buffer, (byte)0);
            if (length < 0)
                length = buffer.Length;

            return Encoding.UTF8.GetString(buffer, 0, length);
        }
    }

    public static class AppServices
    {
        private static IServiceProvider? _provider;

        public static IServiceProvider Provider =>
            _provider ?? throw new InvalidOperationException("Services not configured. Call Configure() first.");

        /// <summary>Registers application services and view models with the dependency-injection container.</summary>
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

            _provider = services.BuildServiceProvider();
        }

        /// <summary>
        /// Creates the AppContainer-safe DHT folder and starts the in-process
        /// torrent HTTP bridge. The native bridge receives this LocalFolder path
        /// through TorrentBridgeService; it must never discover a project directory.
        /// </summary>
        public static async Task<string> InitializeEngineAsync(
            CancellationToken cancellationToken = default)
        {
            var localFolder = ApplicationData.Current.LocalFolder;
            await localFolder.CreateFolderAsync(
                "rqbit_dht",
                CreationCollisionOption.OpenIfExists);

            var bridge = GetRequiredService<ITorrentBridgeService>();
            return await bridge.StartServerAsync(cancellationToken).ConfigureAwait(false);
        }

        public static T GetRequiredService<T>() where T : notnull =>
            Provider.GetRequiredService<T>();
    }
}
