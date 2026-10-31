using StreamioX.App.ViewModels;
using Windows.Media.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StreamioX.App.Views
{
    public sealed partial class PlayerPage : Page
    {
        public PlayerViewModel ViewModel { get; }

        public PlayerPage()
        {
            this.InitializeComponent();
            ViewModel = new PlayerViewModel();
            this.DataContext = ViewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is string streamUrl)
            {
                ViewModel.LoadStream(streamUrl, "Stremio Stream");

                // Correctly assign source and invoke player commands via MediaPlayerElement.MediaPlayer
                PlayerWidget.Source = MediaSource.CreateFromUri(new System.Uri(streamUrl));
                PlayerWidget.MediaPlayer.Play();
            }
        }
    }
}