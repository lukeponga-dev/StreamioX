using StreamioX.App.Models;
using StreamioX.App.Views;
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
            StreamioX.App.Services.PlaybackDiagnostics.Write("[PlaybackTest] Big Buck Bunny button selected; navigating to PlayerPage.");
            Frame.Navigate(typeof(PlayerPage),
                new PlaybackRequest(BigBuckBunnyUrl, "Big Buck Bunny — FFmpeg test"));
        }
    }
}
