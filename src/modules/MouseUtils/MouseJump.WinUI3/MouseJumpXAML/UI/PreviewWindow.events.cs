// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.UI.Xaml;

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
    }

    private void PreviewWindow_Activated(object sender, WindowActivatedEventArgs e)
    {
        switch (e.WindowActivationState)
        {
            case WindowActivationState.CodeActivated:
                this.FocusPreviewPane();
                break;
            case WindowActivationState.Deactivated:
                this.HideWindow();
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
        this.HideWindow();
    }

    private void PreviewPane_Cancel(object? sender, EventArgs e)
    {
        this.HideWindow();
    }
}
