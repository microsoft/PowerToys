// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI;

internal sealed partial class ListItemContainerStyleSelector : StyleSelector
{
    public Style? Default { get; set; }

    public Style? Section { get; set; }

    public Style? Separator { get; set; }

    protected override Style? SelectStyleCore(object item, DependencyObject container)
    {
        return item switch
        {
            ListItemViewModel { IsSectionOrSeparator: true } listItemViewModel when string.IsNullOrWhiteSpace(listItemViewModel.Title) => Separator!,
            ListItemViewModel { IsSectionOrSeparator: true } => Section,
            _ => Default,
        };
    }
}
