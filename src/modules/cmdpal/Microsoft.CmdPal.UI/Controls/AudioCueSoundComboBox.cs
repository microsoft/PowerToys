// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// ComboBox that reports which sound option the user is browsing in the open dropdown, so it
/// can be auditioned before committing. The dropdown lives in a popup (a separate visual tree
/// root), so item events never bubble to the ComboBox itself; handlers must be attached to
/// each item container directly.
/// </summary>
public sealed partial class AudioCueSoundComboBox : ComboBox
{
    public event EventHandler<AudioCueSoundOption>? OptionHighlighted;

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is ComboBoxItem container)
        {
            container.GotFocus -= Container_GotFocus;
            container.GotFocus += Container_GotFocus;
            container.PointerEntered -= Container_PointerEntered;
            container.PointerEntered += Container_PointerEntered;
        }
    }

    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is ComboBoxItem container)
        {
            container.GotFocus -= Container_GotFocus;
            container.PointerEntered -= Container_PointerEntered;
        }

        base.ClearContainerForItemOverride(element, item);
    }

    private void Container_GotFocus(object sender, RoutedEventArgs e) => ReportHighlight(sender);

    private void Container_PointerEntered(object sender, PointerRoutedEventArgs e) => ReportHighlight(sender);

    private void ReportHighlight(object container)
    {
        if (IsDropDownOpen && container is ComboBoxItem { Content: AudioCueSoundOption option })
        {
            OptionHighlighted?.Invoke(this, option);
        }
    }
}
