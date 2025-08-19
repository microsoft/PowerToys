// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.CmdPal.UI;

public partial class GalleryGridLayoutToHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var height = 160;
        var heightIncrease = 0;

        if (value is IGalleryGridLayout gridLayout)
        {
            if (gridLayout.ShowTitle)
            {
                heightIncrease += 20;
            }

            if (gridLayout.ShowSubtitle)
            {
                heightIncrease += 20;
            }
        }

        return height + heightIncrease;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
