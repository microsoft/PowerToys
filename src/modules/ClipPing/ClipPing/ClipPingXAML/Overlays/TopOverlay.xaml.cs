// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;
using Windows.UI;
using WinUIEx;

namespace ClipPing.Overlays;

public sealed partial class TopOverlay : WindowEx, IOverlay
{
    public TopOverlay()
    {
        InitializeComponent();

        this.SetWindowStyle(WindowStyle.Popup);
        this.SetExtendedWindowStyle(ExtendedWindowStyle.Transparent | ExtendedWindowStyle.Layered);
    }

    public void Show(Rect area, Color color)
    {
        OverlayBrush.GradientStops[0].Color = Color.FromArgb(70, color.R, color.G, color.B);
        OverlayBrush.GradientStops[1].Color = Color.FromArgb(0, color.R, color.G, color.B);

        Width = area.Width;
        Height = area.Height;

        this.Move((int)area.Left, (int)area.Top);
        this.SetWindowSize(area.Width, area.Height);

        // Not sure why it's needed, but after relaunching ClipPing the overlay sometimes loses its always-on-top state.
        IsAlwaysOnTop = false;
        IsAlwaysOnTop = true;

        AppWindow.Show(activateWindow: false);

        EnterStoryboard.Begin();
    }

    public void Dispose() => Close();

    private void EnterStoryboard_Completed(object? sender, object e)
    {
        AppWindow.Hide();
    }
}
