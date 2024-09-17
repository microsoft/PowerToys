// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class SearchBar : UserControl
{
    public bool Nested { get; set; }

    public SearchBar()
    {
        this.InitializeComponent();
    }

    private void BackButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // TODO
    }

    private void FilterBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // TODO
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // TODO
    }
}
