// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class FallbackRankerDialog : UserControl
{
    public FallbackRankerDialog(FallbackRanker fallbackRanker)
    {
        InitializeComponent();

        fallbackRanker.Margin = new Thickness(-24, 0, -24, 0);
        Grid.SetRow(fallbackRanker, 2);
        RankerGrid.Children.Add(fallbackRanker);
    }

    public IAsyncOperation<ContentDialogResult> ShowAsync()
    {
        return FallbackRankerContentDialog!.ShowAsync()!;
    }
}
