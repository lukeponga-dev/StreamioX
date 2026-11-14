// Purpose: Provides shared StringToVisibilityConverter utility behavior used by multiple screens or controls.

using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace StreamioX.App.Helpers
{
    public sealed partial class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(
            object value,
            Type targetType,
            object parameter,
            string language)
        {
            return !string.IsNullOrWhiteSpace(value as string)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            string language)
        {
            // This converter is intentionally one-way; returning UnsetValue is
            // safer than throwing if a binding is ever configured as TwoWay.
            return DependencyProperty.UnsetValue;
        }
    }
}
