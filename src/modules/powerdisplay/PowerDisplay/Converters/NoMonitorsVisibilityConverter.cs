// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PowerDisplay.Converters
{
    /// <summary>
    /// Special converter for "No monitors" visibility
    /// Shows when initialized but has no monitors
    /// </summary>
    public partial class NoMonitorsVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // This would need access to both IsInitialized and HasMonitors
            // For simplicity, we'll handle this in the ViewModel
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
