// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Dock;

internal sealed partial class DockBandTemplateSelector : DataTemplateSelector
{
    public DockControl? Control { get; set; }

    public DataTemplate? HorizontalTemplate { get; set; }

    public DataTemplate? VerticalTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        if (Control is null)
        {
            return HorizontalTemplate;
        }

        return Control.ItemsOrientation == Orientation.Horizontal
            ? HorizontalTemplate
            : VerticalTemplate;
    }
}
