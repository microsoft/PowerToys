// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// Converts a boolean to a <see cref="GridLength"/>: <c>true</c> yields a star (*) row that
/// fills the available space, while <c>false</c> yields an Auto row that sizes to its content.
/// This lets the expandable content row collapse to zero in compact mode so the card can
/// shrink to just the search box (a star row would otherwise reserve space during measure
/// even when its only child is collapsed).
/// </summary>
public partial class BoolToStarOrAutoGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var expanded = value is bool b && b;
        return expanded ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
