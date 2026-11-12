// Purpose: Implements reusable MediaCardControl.xaml control behavior and interaction handling.

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace StreamioX.App.Controls
{
    public sealed partial class MediaCardControl : UserControl
    {
        public MediaCardControl()
        {
            InitializeComponent();
        }

        private void MediaCardControl_GotFocus(
            object sender,
            RoutedEventArgs e)
        {
            CardScale.ScaleX = 1.05;
            CardScale.ScaleY = 1.05;

            GlowBorder.Visibility = Visibility.Visible;
        }

        private void MediaCardControl_LostFocus(
            object sender,
            RoutedEventArgs e)
        {
            CardScale.ScaleX = 1.0;
            CardScale.ScaleY = 1.0;

            GlowBorder.Visibility = Visibility.Collapsed;
        }
    }
}
