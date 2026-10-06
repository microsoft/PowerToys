// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

using MouseJump.Common.Helpers;
using MouseJump.Models.Display;

using Windows.System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace MouseJump.WinUI3.UI;

internal sealed partial class PreviewWindow
{
    /// <summary>
    /// Wires up every event handler this window subscribes to.
    /// Called from <see cref="InitializeWindow"/>.
    /// </summary>
    private void InitializeEvents()
    {
        this.Activated += this.PreviewWindow_Activated;
    }

    private void PreviewWindow_Activated(object sender, WindowActivatedEventArgs e)
    {
        switch (e.WindowActivationState)
        {
            case WindowActivationState.CodeActivated:
                this.PreviewImage.Focus(FocusState.Programmatic);
                break;
            case WindowActivationState.Deactivated:
                this.HideWindow();
                break;
            case WindowActivationState.PointerActivated:
                this.PreviewImage.Focus(FocusState.Pointer);
                break;
            default:
                throw new InvalidOperationException();
        }
    }
}
