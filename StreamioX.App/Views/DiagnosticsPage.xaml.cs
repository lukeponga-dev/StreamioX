using StreamioX.App.Models;
using StreamioX.App.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace StreamioX.App.Views
{
    public sealed partial class DiagnosticsPage : Page
    {
        private const string BigBuckBunnyUrl =
            "https://test-videos.co.uk/vids/bigbuckbunny/mp4/h264/720/Big_Buck_Bunny_720_10s_1MB.mp4";

        public DiagnosticsPage()
        {
            InitializeComponent();
        }

        private void PlayBigBuckBunny_Click(object sender, RoutedEventArgs e)
        {
            PlaybackDiagnostics.Write("[PlaybackTest] Direct Big Buck Bunny button selected; navigating to PlayerPage.");
            Frame.Navigate(typeof(PlayerPage),
                new PlaybackRequest(BigBuckBunnyUrl, "Big Buck Bunny — FFmpeg test"));
        }

        private async void PlayBigBuckBunnyTorrent_Click(object sender, RoutedEventArgs e)
        {
            // This diagnostic torrent is generated from the same 10-second
            // Big Buck Bunny MP4 used by the direct FFmpeg baseline. A local
            // librqbit seeder listens on TCP 50000; x.pe keeps the test
            // deterministic and independent of public tracker availability.
            const string infoHash = "c8a92e226f3c2f70391b9ec819d275bef9fa67a3";
            const string magnet =
                "magnet:?xt=urn:btih:c8a92e226f3c2f70391b9ec819d275bef9fa67a3" +
                "&dn=Big+Buck+Bunny" +
                "&x.pe=127.0.0.1%3A50000";
            var stream = new StreamSource
            {
                Name = "Big Buck Bunny local torrent test",
                Title = "Big Buck Bunny — local torrent bridge test",
                InfoHash = infoHash,
                Url = magnet,
                FileIdx = -1,
                Sources = new List<string>()
            };

            try
            {
                PlaybackDiagnostics.Write("[TorrentTest] Starting local Big Buck Bunny torrent test against seeder 127.0.0.1:50000.");
                var bridge = AppServices.GetRequiredService<ITorrentBridgeService>();
                var streamUrl = await bridge.GetStreamUrlAsync(stream, CancellationToken.None);
                PlaybackDiagnostics.Write("[TorrentTest] Local torrent stream URL prepared; navigating to PlayerPage.");
                Frame.Navigate(typeof(PlayerPage),
                    new PlaybackRequest(streamUrl, stream.Title!));
            }
            catch (System.Exception ex)
            {
                PlaybackDiagnostics.Write($"[TorrentTest] Failed before PlayerPage: 0x{ex.HResult:X8} {ex}");
            }
        }

        private async void PlayAuthenticatedAddon_Click(object sender, RoutedEventArgs e)
        {
            const string contentId = "tt0133093"; // The Matrix (1999)
            try
            {
                PlaybackDiagnostics.Write("[AddonPlaybackTest] Requesting a real movie stream from the installed Jackettio add-on.");
                var addons = AppServices.GetRequiredService<IAddonService>();
                var streamService = AppServices.GetRequiredService<IStreamService>();
                await addons.InitializeAsync(CancellationToken.None);

                var addon = addons.GetStreamProviders("movie", contentId)
                    .FirstOrDefault(item => item.Name != null
                        && item.Name.IndexOf("Jackettio", System.StringComparison.OrdinalIgnoreCase) >= 0);
                if (addon == null)
                {
                    PlaybackDiagnostics.Write("[AddonPlaybackTest] Jackettio is not installed or does not support this movie ID.");
                    return;
                }

                var streams = await streamService.GetStreamsAsync(addon.BaseUrl, "movie", contentId, CancellationToken.None);
                PlaybackDiagnostics.Write($"[AddonPlaybackTest] Add-on '{addon.Name}' returned {streams.Count} stream(s).");
                var selected = streams.FirstOrDefault(item => Uri.TryCreate(item.Url, UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
                if (selected?.Url == null)
                {
                    PlaybackDiagnostics.Write("[AddonPlaybackTest] No direct HTTP(S) stream URL was returned.");
                    return;
                }

                var streamUri = new Uri(selected.Url);
                var headerCount = selected.BehaviorHints?.ProxyHeaders?.Request?.Count ?? 0;
                PlaybackDiagnostics.Write($"[AddonPlaybackTest] Selected stream host '{streamUri.Host}'; provider request header count: {headerCount}. Navigating to PlayerPage.");
                Frame.Navigate(typeof(PlayerPage), new PlaybackRequest(
                    selected.Url,
                    "The Matrix — authenticated Jackettio add-on test",
                    requestHeaders: selected.BehaviorHints?.ProxyHeaders?.Request));
            }
            catch (Exception ex)
            {
                PlaybackDiagnostics.Write($"[AddonPlaybackTest] Failed: 0x{ex.HResult:X8} {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
