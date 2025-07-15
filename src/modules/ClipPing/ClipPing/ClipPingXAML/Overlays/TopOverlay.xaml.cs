// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;
using WinUIEx;

namespace ClipPing.Overlays;

public sealed partial class TopOverlay : WindowEx, IOverlay
{
    public TopOverlay()
    {
        InitializeComponent();

        // TODO: is it needed?
        // var handle = this.GetWindowHandle();
        this.SetWindowStyle(WindowStyle.Popup);
        this.SetExtendedWindowStyle(ExtendedWindowStyle.Transparent | ExtendedWindowStyle.Layered);
    }

    public void Show(Rect area)
    {
        Width = area.Width;
        Height = area.Height;

        this.Move((int)area.Left, (int)area.Top);
        this.SetWindowSize(area.Width, area.Height);

        AppWindow.Show(activateWindow: false);

        EnterStoryboard.Begin();
    }

    private void EnterStoryboard_Completed(object? sender, object e)
    {
        ExitStoryboard.Begin();
    }

    private void ExitStoryboard_Completed(object? sender, object e)
    {
        AppWindow.Hide();
    }
}
