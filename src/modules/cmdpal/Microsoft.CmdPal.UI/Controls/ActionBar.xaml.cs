// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ActionBar : UserControl, ICurrentPageAware
{
    public ActionBarViewModel ViewModel { get; set; } = new();

    public PageViewModel? CurrentPageViewModel
    {
        get => (PageViewModel?)GetValue(CurrentPageViewModelProperty);
        set => SetValue(CurrentPageViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for CurrentPage.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty CurrentPageViewModelProperty =
        DependencyProperty.Register(nameof(CurrentPageViewModel), typeof(PageViewModel), typeof(ActionBar), new PropertyMetadata(null));

    public ActionBar()
    {
        this.InitializeComponent();
    }

    private void ActionListViewItem_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // TODO
    }

    private void ActionListViewItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        MoreCommandsButton.Flyout.Hide();

        if (sender is not ListViewItem listItem)
        {
            return;
        }

        if (listItem.DataContext is CommandContextItemViewModel item)
        {
            ViewModel?.InvokeItemCommand.Execute(item);
        }
    }
}
