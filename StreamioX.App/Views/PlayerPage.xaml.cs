using StreamioX.App.ViewModels;
using System;
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
            InitializeComponent();
            ViewModel = AppServices.GetRequiredService<PlayerViewModel>();
            DataContext = ViewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is string streamUrl && !string.IsNullOrWhiteSpace(streamUrl))
            {
                ViewModel.LoadStream(streamUrl, "Stremio Stream");
                PlayerWidget.Source = MediaSource.CreateFromUri(new Uri(streamUrl));
                PlayerWidget.MediaPlayer.Play();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            if (PlayerWidget.MediaPlayer != null)
            {
                PlayerWidget.MediaPlayer.Pause();
                PlayerWidget.Source = null;
            }
        }
    }
}
