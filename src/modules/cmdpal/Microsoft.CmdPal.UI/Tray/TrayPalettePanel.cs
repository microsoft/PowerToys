// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Tray;

public sealed partial class TrayPalettePanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var bounds = GetItemBounds();
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Measure(new(bounds[i].Width, bounds[i].Height));
        }

        return new(TrayPaletteLayout.Width, bounds.LastOrDefault().Bottom);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var bounds = GetItemBounds();
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Arrange(bounds[i]);
        }

        return finalSize;
    }

    private Rect[] GetItemBounds() => TrayPaletteLayout.GetItemBounds(
        Children.Select(child => child is GridViewItem { Content: TrayPaletteItemViewModel { HasSmallContent: true } }));
}
