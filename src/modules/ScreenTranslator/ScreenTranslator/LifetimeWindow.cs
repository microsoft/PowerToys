// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Xaml.Controls;

namespace ScreenTranslator;

internal sealed class LifetimeWindow : TransparentWindow
{
    public LifetimeWindow()
    {
        Content = new Grid();
        EnableCloakedHide();
        Show();
    }
}
