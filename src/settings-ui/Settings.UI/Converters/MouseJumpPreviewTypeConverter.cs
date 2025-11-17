// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml.Data;
using MouseJump.Common.Models.Settings;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public sealed partial class MouseJumpPreviewTypeConverter : IValueConverter
    {
        private static readonly PreviewType[] PreviewTypeOrder =
        [
            PreviewType.Compact, PreviewType.Bezelled, PreviewType.Custom,
        ];

        private static readonly PreviewType DefaultPreviewType = PreviewType.Bezelled;

        // Receives a string as a parameter and returns an int representing the index
        // to select in the Segmented control on the Mouse Jump settings page
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var previewType = MouseJumpPreviewTypeConverter.DefaultPreviewType;

            if (value is not string previewTypeName)
            {
                // the value isn't a string so just use the default preview type
            }
            else if (Enum.IsDefined(typeof(PreviewType), previewTypeName))
            {
                // there's a case-sensitive match for the value
                previewType = Enum.Parse<PreviewType>(previewTypeName);
            }
            else if (Enum.TryParse<PreviewType>(previewTypeName, true, out var previewTypeResult))
            {
                // there's a case-insensitive match for the value
                previewType = previewTypeResult;
            }

            return Array.IndexOf(
                MouseJumpPreviewTypeConverter.PreviewTypeOrder,
                previewType);
        }

        // Receives an int as a parameter that represents the selected index in the Segmented
        // control on the Mouse Jump settings page, and returns the name of the PreviewType enum
        // for that index.
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            var previewType = MouseJumpPreviewTypeConverter.DefaultPreviewType;

            if (value is not int segmentedIndex)
            {
                // the value isn't an int so just use the default preview type
            }
            else if ((segmentedIndex < 0) || (segmentedIndex > MouseJumpPreviewTypeConverter.PreviewTypeOrder.Length))
            {
                // not a valid selected index so just use the default preview type
            }
            else
            {
                previewType = MouseJumpPreviewTypeConverter.PreviewTypeOrder[segmentedIndex];
            }

            return previewType.ToString();
        }
    }
}
