// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;

namespace WorkspacesEditor.Helpers
{
    internal static class ThemeHelper
    {
        /// <summary>
        /// Returns true if the current app theme is dark.
        /// Uses WinUI Application.RequestedTheme which respects system settings.
        /// </summary>
        internal static bool IsDarkTheme()
        {
            if (Application.Current?.RequestedTheme == ApplicationTheme.Dark)
            {
                return true;
            }

            return false;
        }
    }
}
