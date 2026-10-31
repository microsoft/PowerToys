// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

using MouseJump.Common.Helpers;

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
        this.PreviewPane.NavigateTo += this.PreviewPane_NavigateTo;
        this.PreviewPane.Cancel += this.PreviewPane_Cancel;
        this.RootGrid.PointerPressed += this.RootGrid_PointerPressed;
    }

    private void PreviewWindow_Activated(object sender, WindowActivatedEventArgs e)
    {
        switch (e.WindowActivationState)
        {
            case WindowActivationState.CodeActivated:
            case WindowActivationState.PointerActivated:
                this.FocusPreviewPane();
                break;
            case WindowActivationState.Deactivated:
                this.DismissWindow();
                break;
            default:
                throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Handles a navigation signal from <see cref="PreviewPane"/> - a screenshot click or the
    /// equivalent keyboard shortcut (see <see cref="PreviewPane.NavigateTo"/>).
    /// </summary>
    private void PreviewPane_NavigateTo(object? sender, NavigateToEventArgs e)
    {
        Logger.LogInfo(string.Join(
            '\n',
            "-----------",
            nameof(PreviewWindow.PreviewPane_NavigateTo),
            "-----------",
            $"device   = {e.Device.Hostname}",
            $"location = {e.Location}"));

        MouseHelper.SetCursorPosition(e.Location);
        this.DismissWindow();
    }

    private void PreviewPane_Cancel(object? sender, EventArgs e)
    {
        this.DismissWindow();
    }

    /// <summary>
    /// Dismisses the window on a right-click anywhere outside <see cref="PreviewPane"/> - i.e. on
    /// the border ring around it, which <see cref="PreviewPane"/> doesn't cover. Right-clicks
    /// inside the pane are handled (and marked as handled) by the pane itself, so they never
    /// bubble up to here.
    /// </summary>
    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.Pointer.PointerDeviceType.Equals(PointerDeviceType.Mouse))
        {
            // not a mouse click
            return;
        }

        if (e.GetCurrentPoint(this.RootGrid).Properties.IsRightButtonPressed)
        {
            e.Handled = true;
            this.DismissWindow();
        }
    }
}
