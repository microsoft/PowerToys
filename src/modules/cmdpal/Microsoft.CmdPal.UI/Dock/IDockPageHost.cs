// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Microsoft.CmdPal.UI.Dock;

internal interface IDockPageHost : IDisposable
{
    event EventHandler? Opened;

    event EventHandler? Closed;

    bool IsOpen { get; }

    DockPageControl? Content { get; set; }

    void Show(FrameworkElement anchor, FlyoutPlacementMode placement);

    void Hide();
}
