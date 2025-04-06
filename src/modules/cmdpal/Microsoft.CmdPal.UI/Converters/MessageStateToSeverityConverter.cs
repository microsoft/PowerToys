// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.CmdPal.UI;

public partial class MessageStateToSeverityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is MessageState state)
        {
            switch (state)
            {
                case MessageState.Info:
                    return InfoBarSeverity.Informational;
                case MessageState.Success:
                    return InfoBarSeverity.Success;
                case MessageState.Warning:
                    return InfoBarSeverity.Warning;
                case MessageState.Error:
                    return InfoBarSeverity.Error;
            }
        }

        return InfoBarSeverity.Informational;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
