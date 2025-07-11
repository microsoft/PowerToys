// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Data;
using RS_ = Microsoft.CmdPal.Core.Control.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.Core.Control;

public partial class PlaceholderTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is string placeholder && !string.IsNullOrEmpty(placeholder)
            ? placeholder
            : (object)RS_.GetString("DefaultSearchPlaceholderText");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
