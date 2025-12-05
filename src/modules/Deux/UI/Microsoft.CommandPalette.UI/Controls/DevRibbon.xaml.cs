// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Microsoft.CommandPalette.UI.Controls;

internal sealed partial class DevRibbon : UserControl
{
    public DevRibbonViewModel ViewModel { get; }

    public DevRibbon()
    {
        InitializeComponent();
        ViewModel = new DevRibbonViewModel();

        if (FlyoutContent != null)
        {
            FlyoutContent.DataContext = ViewModel;
        }
    }

    private void DevRibbonButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "PointerOver", true);
    }

    private void DevRibbonButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "Normal", true);
    }

    private Visibility VisibleIfGreaterThanZero(int value)
    {
        return value > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
