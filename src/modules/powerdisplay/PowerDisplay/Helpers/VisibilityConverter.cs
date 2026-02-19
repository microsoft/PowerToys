// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;

namespace PowerDisplay.Helpers
{
    /// <summary>
    /// Provides conversion utilities for Visibility binding in x:Bind scenarios.
    /// AOT-compatible alternative to IValueConverter implementations.
    /// </summary>
    public static class VisibilityConverter
    {
        /// <summary>
        /// Converts a boolean value to a Visibility value.
        /// </summary>
        /// <param name="value">The boolean value to convert.</param>
        /// <returns>Visibility.Visible if true, Visibility.Collapsed if false.</returns>
        public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    }
}
