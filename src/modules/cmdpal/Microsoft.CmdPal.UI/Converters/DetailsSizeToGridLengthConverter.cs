// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.CmdPal.UI;

public partial class DetailsSizeToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is IDetailsSizeViewModel sizeViewModel)
        {
            var starValue = sizeViewModel switch
            {
                SmallDetailsViewModel => 3.0,
                MediumDetailsViewModel => 2.0,
                LargeDetailsViewModel => 1.0,
                _ => 3.0,
            };

            return new GridLength(starValue, GridUnitType.Star);
        }

        return new GridLength(3.0, GridUnitType.Star);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
