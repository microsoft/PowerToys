// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Microsoft.UI.Xaml.Data;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI;

public sealed partial class QuickAccessShortcutTextConverter : IValueConverter
{
    private static readonly CompositeFormat _shortcutFormat =
        CompositeFormat.Parse(RS_.GetString("QuickAccessShelfShortcutTextFormat"));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is string shortcutDigit && !string.IsNullOrEmpty(shortcutDigit)
            ? string.Format(CultureInfo.CurrentCulture, _shortcutFormat, shortcutDigit)
            : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
