using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace StreamioX.App.Helpers
{
    public static class FocusHelper
    {
        private static readonly Dictionary<Type, string>
            LastFocusedElements = new Dictionary<Type, string>();

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