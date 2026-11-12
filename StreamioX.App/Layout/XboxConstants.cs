// Layout/XboxConstants.cs
using Windows.UI.Xaml;

namespace StreamioX.App.Layout
{
    public static class XboxConstants
    {
        public const double SafeMarginLeft = 48;
        public const double SafeMarginRight = 48;
        public const double SafeMarginTop = 27;
        public const double SafeMarginBottom = 27;

        /// <summary>
        /// Combines safe margins into a Thickness object for XAML padding bindings.
        /// </summary>
        public static Thickness SafeArea => new Thickness(SafeMarginLeft, SafeMarginTop, SafeMarginRight, SafeMarginBottom);
    }
}
