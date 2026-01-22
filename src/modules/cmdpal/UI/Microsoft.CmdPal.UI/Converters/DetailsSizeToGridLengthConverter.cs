// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.CmdPal.UI;

public partial class DetailsSizeToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is ContentSize size)
        {
            // This converter calculates the Star width for the LIST.
            //
            // The input 'size' (ContentSize) represents the TARGET WIDTH desired for the DETAILS PANEL.
            //
            // To ensure the Details Panel achieves its target size (e.g. ContentSize.Large),
            // we must shrink the List and let the Details fill the available space.
            // (e.g., A larger target size for Details results in a smaller Star value for the List).
            var starValue = size switch
            {
                ContentSize.Small => 3.0,
                ContentSize.Medium => 2.0,
                ContentSize.Large => 1.0,
                _ => 3.0,
            };

            return new GridLength(starValue, GridUnitType.Star);
        }

        return new GridLength(3.0, GridUnitType.Star);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
