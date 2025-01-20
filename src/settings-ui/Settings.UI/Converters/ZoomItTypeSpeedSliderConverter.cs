// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public sealed partial class ZoomItTypeSpeedSliderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string targetValue = string.Empty;
            int zoomLevel = System.Convert.ToInt32((double)value);
            string explanation = ResourceLoaderInstance.ResourceLoader.GetString("ZoomIt_DemoType_SpeedSlider_Thumbnail_Explanation");

            targetValue = $"{zoomLevel} ({explanation})";

            return targetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}
