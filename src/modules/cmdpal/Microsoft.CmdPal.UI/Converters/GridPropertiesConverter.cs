// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.CmdPal.UI.Converters;

public class GridPropertiesToTileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is IGridProperties gridProperties)
        {
            return gridProperties.TileSize;
        }

        // Default tile size if no grid properties
        return new Windows.Foundation.Size(120, 120);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}