// Purpose: Provides shared FocusHelper utility behavior used by multiple screens or controls.

using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI;

namespace StreamioX.App.Helpers
{
    public static class FocusHelper
    {
        private static readonly Dictionary<Type, string>
            LastFocusedElements = new Dictionary<Type, string>();

        /// <summary>Remembers the focused control so it can be restored when the page becomes active again.</summary>
        public static void Capture(Page page, Control fallback)
        {
            var focusedElement =
                FocusManager.GetFocusedElement() as Control;

            string elementName =
                focusedElement != null &&
                !string.IsNullOrWhiteSpace(focusedElement.Name)
                    ? focusedElement.Name
                    : fallback.Name;

            if (!string.IsNullOrWhiteSpace(elementName))
            {
                LastFocusedElements[page.GetType()] = elementName;
            }
        }

        /// <summary>
        /// Applies the Xbox controller focus scale and green border treatment.
        /// The focus visual remains available as a fallback for non-Button controls.
        /// </summary>
        public static void AttachXboxFocus(Control control)
        {
            if (control == null)
                throw new ArgumentNullException(nameof(control));

            var scale = new ScaleTransform { ScaleX = 1.0, ScaleY = 1.0 };
            control.RenderTransform = scale;

            var originalBrush = (control as Button)?.BorderBrush;
            var originalThickness = (control as Button)?.BorderThickness ?? new Thickness(0);

            control.GotFocus += (_, _) =>
            {
                scale.CenterX = control.ActualWidth / 2;
                scale.CenterY = control.ActualHeight / 2;
                scale.ScaleX = 1.05;
                scale.ScaleY = 1.05;

                if (control is Button button)
                {
                    button.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 22, 198, 12));
                    button.BorderThickness = new Thickness(2);
                }
            };

            control.LostFocus += (_, _) =>
            {
                scale.ScaleX = 1.0;
                scale.ScaleY = 1.0;

                if (control is Button button)
                {
                    button.BorderBrush = originalBrush;
                    button.BorderThickness = originalThickness;
                }
            };
        }

        /// <summary>Restores the previously focused control, falling back to the supplied default when needed.</summary>
        public static void Restore(Page page, Control fallback)
        {
            if (LastFocusedElements.TryGetValue(
                    page.GetType(), out string elementName))
            {
                var control =
                    page.FindName(elementName) as Control;

                if (control != null &&
                    control.Focus(FocusState.Programmatic))
                {
                    return;
                }
            }

            fallback.Focus(FocusState.Programmatic);
        }
    }
}
