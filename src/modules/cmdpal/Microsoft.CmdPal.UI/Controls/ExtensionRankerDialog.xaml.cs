// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ExtensionRankerDialog : UserControl
{
    public ExtensionRankerDialog()
    {
        InitializeComponent();
    }

    public IAsyncOperation<ContentDialogResult> ShowAsync()
    {
        return ExtensionRankerContentDialog!.ShowAsync()!;
    }
}
