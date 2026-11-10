using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace StreamioX.App.Helpers
{
    /// <summary>Converts a boolean view-model state to UWP visibility.</summary>
    public sealed partial class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var isVisible = value is bool flag && flag;
            if (parameter is string option &&
                string.Equals(option, "Inverse", StringComparison.OrdinalIgnoreCase))
            {
                isVisible = !isVisible;
            }

            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is not Visibility visibility)
                return false;

            var result = visibility == Visibility.Visible;
            return parameter is string option &&
                   string.Equals(option, "Inverse", StringComparison.OrdinalIgnoreCase)
                ? !result
                : result;
        }
    }
}
