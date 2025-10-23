// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.CmdPal.UI.Dock;

public sealed partial class IconInfoVisibilityConverter : IValueConverter
{
    private static bool IsVisible(IconInfoViewModel iconInfoViewModel, ElementTheme requestedTheme) =>
        iconInfoViewModel?.HasIcon(requestedTheme == Microsoft.UI.Xaml.ElementTheme.Light) ?? false;

    private static bool IsVisible(IconInfoViewModel iconInfoViewModel, ApplicationTheme requestedTheme) =>
        iconInfoViewModel?.HasIcon(requestedTheme == ApplicationTheme.Light) ?? false;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is IconInfoViewModel iconInfoVM)
        {
            return IsVisible(iconInfoVM, Application.Current.RequestedTheme) ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();

    public IconInfoVisibilityConverter()
    {
    }
}
