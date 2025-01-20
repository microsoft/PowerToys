// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public sealed partial class ZoomItInitialZoomConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string targetValue = string.Empty;
            int zoomLevel = System.Convert.ToInt32((double)value);

            // Should match the zoom values expected by ZoomIt internal logic.
            switch (zoomLevel)
            {
                case 0: targetValue = "1.25"; break;
                case 1: targetValue = "1.5"; break;
                case 2: targetValue = "1.75"; break;
                case 3: targetValue = "2.0"; break;
                case 4: targetValue = "3.0"; break;
                case 5: targetValue = "4.0"; break;
            }

            return targetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}
