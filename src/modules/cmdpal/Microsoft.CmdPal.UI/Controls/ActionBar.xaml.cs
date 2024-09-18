// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Microsoft.CmdPal.UI.Controls;

[ObservableObject]
public sealed partial class ActionBar : UserControl
{
    [ObservableProperty]
    private string _actionName = string.Empty;

    [ObservableProperty]
    private bool _moreCommandsAvailable = false;

    private ObservableCollection<string> _contextActions = [];

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
        // TODO
    }

    private void ActionBar_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // TODO Just in here for testing
        ActionName = "My Action";
        MoreCommandsAvailable = true;
    }
}
