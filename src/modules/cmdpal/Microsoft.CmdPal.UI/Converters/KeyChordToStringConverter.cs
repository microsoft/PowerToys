// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.UI.Xaml.Data;
using Windows.System;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI;

public partial class KeyChordToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is KeyChord shortcut && (VirtualKey)shortcut.Vkey != VirtualKey.None)
        {
            var result = string.Empty;

            if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Control))
            {
                result += "Ctrl+";
            }

            if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Shift))
            {
                result += "Shift+";
            }

            if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Menu))
            {
                result += "Alt+";
            }

            result += (VirtualKey)shortcut.Vkey;

            return result;
        }

        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
