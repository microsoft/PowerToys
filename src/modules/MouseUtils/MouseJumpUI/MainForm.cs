// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using ManagedCommon;
using MouseJumpUI.Helpers;
using MouseJumpUI.Models.Drawing;
using MouseJumpUI.Models.Layout;
using static MouseJumpUI.NativeMethods.Core;

namespace MouseJumpUI;

internal partial class MainForm : Form
{
    public MainForm()
    {
        this.InitializeComponent();
        this.ShowThumbnail();
    }

    private void MainForm_Load(object sender, EventArgs e)
    {
    }

    private void MainForm_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            this.OnDeactivate(EventArgs.Empty);
        }
    }

    private void MainForm_Deactivate(object sender, EventArgs e)
    {
        this.Close();

        if (this.Thumbnail.Image is not null)
        {
            var tmp = this.Thumbnail.Image;
            this.Thumbnail.Image = null;
            tmp.Dispose();
        }
    }

    private void Thumbnail_Click(object sender, EventArgs e)
    {
        var mouseEventArgs = (MouseEventArgs)e;
        Logger.LogInfo(string.Join(
            '\n',
            $"Reporting mouse event args",
            $"\tbutton   = {mouseEventArgs.Button}",
            $"\tlocation = {mouseEventArgs.Location}"));

        if (mouseEventArgs.Button == MouseButtons.Left)
        {
            // plain click - move mouse pointer
            var virtualScreen = ScreenHelper.GetVirtualScreen();
            var scaledLocation = MouseHelper.GetJumpLocation(
                new PointInfo(mouseEventArgs.X, mouseEventArgs.Y),
                new SizeInfo(this.Thumbnail.Size),
                virtualScreen);
            Logger.LogInfo($"scaled location = {scaledLocation}");
            MouseHelper.SetCursorPosition(scaledLocation);
            Microsoft.PowerToys.Telemetry.PowerToysTelemetry.Log.WriteEvent(new Telemetry.MouseJumpTeleportCursorEvent());
        }

        this.OnDeactivate(EventArgs.Empty);
    }

    public void ShowThumbnail()
    {
        var stopwatch = Stopwatch.StartNew();
        var layoutInfo = MainForm.GetLayoutInfo(this);
        LayoutHelper.PositionForm(this, layoutInfo.FormBounds);
        MainForm.RenderPreview(this, layoutInfo);
        stopwatch.Stop();

        // we have to activate the form to make sure the deactivate event fires
        Microsoft.PowerToys.Telemetry.PowerToysTelemetry.Log.WriteEvent(new Telemetry.MouseJumpTeleportCursorEvent());
        this.Activate();
    }

    private static LayoutInfo GetLayoutInfo(MainForm form)
    {
        // map screens to their screen number in "System > Display"
        var screens = ScreenHelper.GetAllScreens()
            .Select((screen, index) => new { Screen = screen, Index = index, Number = index + 1 })
            .ToList();
        foreach (var screen in screens)
        {
            Logger.LogInfo(string.Join(
                '\n',
                $"screen[{screen.Number}]",
                $"\tprimary      = {screen.Screen.Primary}",
                $"\tdisplay area = {screen.Screen.DisplayArea}",
                $"\tworking area = {screen.Screen.WorkingArea}"));
        }

        // collect together some values that we need for calculating layout
        var activatedLocation = MouseHelper.GetCursorPosition();
        var activatedScreenHandle = ScreenHelper.MonitorFromPoint(activatedLocation);
        var activatedScreenIndex = screens
            .Single(item => item.Screen.Handle == activatedScreenHandle.Value)
            .Index;

        var layoutConfig = new LayoutConfig(
            virtualScreenBounds: ScreenHelper.GetVirtualScreen(),
            screens: screens.Select(item => item.Screen).ToList(),
            activatedLocation: activatedLocation,
            activatedScreenIndex: activatedScreenIndex,
            activatedScreenNumber: activatedScreenIndex + 1,
            maximumFormSize: new(1600, 1200),
            formPadding: new(
                form.panel1.Padding.Left,
                form.panel1.Padding.Top,
                form.panel1.Padding.Right,
                form.panel1.Padding.Bottom),
            previewPadding: new(0));
        Logger.LogInfo(string.Join(
            '\n',
            $"Layout config",
            $"-------------",
            $"virtual screen          = {layoutConfig.VirtualScreenBounds}",
            $"activated location      = {layoutConfig.ActivatedLocation}",
            $"activated screen index  = {layoutConfig.ActivatedScreenIndex}",
            $"activated screen number = {layoutConfig.ActivatedScreenNumber}",
            $"maximum form size       = {layoutConfig.MaximumFormSize}",
            $"form padding            = {layoutConfig.FormPadding}",
            $"preview padding         = {layoutConfig.PreviewPadding}"));

        // calculate the layout coordinates for everything
        var layoutInfo = LayoutHelper.CalculateLayoutInfo(layoutConfig);
        Logger.LogInfo(string.Join(
            '\n',
            $"Layout info",
            $"-----------",
            $"form bounds      = {layoutInfo.FormBounds}",
            $"preview bounds   = {layoutInfo.PreviewBounds}",
            $"activated screen = {layoutInfo.ActivatedScreenBounds}"));

        return layoutInfo;
    }

    private static void RenderPreview(
        MainForm form, LayoutInfo layoutInfo)
    {
        var layoutConfig = layoutInfo.LayoutConfig;

        // initialize the preview image
        var preview = new Bitmap(
            (int)layoutInfo.PreviewBounds.Width,
            (int)layoutInfo.PreviewBounds.Height,
            PixelFormat.Format32bppArgb);
        form.Thumbnail.Image = preview;

        using var previewGraphics = Graphics.FromImage(preview);

        DrawingHelper.DrawPreviewBackground(previewGraphics, layoutInfo.PreviewBounds, layoutInfo.ScreenBounds);

        var desktopHwnd = HWND.Null;
        var desktopHdc = HDC.Null;
        var previewHdc = HDC.Null;
        try
        {
            DrawingHelper.EnsureDesktopDeviceContext(ref desktopHwnd, ref desktopHdc);

            // we have to capture the screen where we're going to show the form first
            // as the form will obscure the screen as soon as it's visible
            var activatedStopwatch = Stopwatch.StartNew();
            DrawingHelper.EnsurePreviewDeviceContext(previewGraphics, ref previewHdc);
            DrawingHelper.DrawPreviewScreen(
                desktopHdc,
                previewHdc,
                layoutConfig.Screens[layoutConfig.ActivatedScreenIndex].Bounds,
                layoutInfo.ScreenBounds[layoutConfig.ActivatedScreenIndex]);
            activatedStopwatch.Stop();

            // show the placeholder images if it looks like it might take a while
            // to capture the remaining screenshot images
            if (activatedStopwatch.ElapsedMilliseconds > 250)
            {
                var activatedArea = layoutConfig.Screens[layoutConfig.ActivatedScreenIndex].Bounds.Area;
                var totalArea = layoutConfig.Screens.Sum(screen => screen.Bounds.Area);
                if ((activatedArea / totalArea) < 0.5M)
                {
                    // we need to release the device context handle before we can draw the placeholders
                    // using the Graphics object otherwise we'll get an error from GDI saying
                    // "Object is currently in use elsewhere"
                    DrawingHelper.FreePreviewDeviceContext(previewGraphics, ref previewHdc);
                    DrawingHelper.DrawPreviewScreenPlaceholders(
                        previewGraphics,
                        layoutInfo.ScreenBounds.Where((_, idx) => idx != layoutConfig.ActivatedScreenIndex));
                    MainForm.RefreshPreview(form);
                }
            }

            // draw the remaining screen captures (if any) on the preview image
            var sourceScreens = layoutConfig.Screens
                .Where((_, idx) => idx != layoutConfig.ActivatedScreenIndex)
                .Select(screen => screen.Bounds)
                .ToList();
            if (sourceScreens.Any())
            {
                DrawingHelper.EnsurePreviewDeviceContext(previewGraphics, ref previewHdc);
                DrawingHelper.DrawPreviewScreens(
                    desktopHdc,
                    previewHdc,
                    sourceScreens,
                    layoutInfo.ScreenBounds.Where((_, idx) => idx != layoutConfig.ActivatedScreenIndex).ToList());
                DrawingHelper.FreePreviewDeviceContext(previewGraphics, ref previewHdc);
                MainForm.RefreshPreview(form);
            }
        }
        finally
        {
            DrawingHelper.FreeDesktopDeviceContext(ref desktopHwnd, ref desktopHdc);
            DrawingHelper.FreePreviewDeviceContext(previewGraphics, ref previewHdc);
        }
    }

    private static void RefreshPreview(MainForm form)
    {
        if (!form.Visible)
        {
            form.Show();
        }

        form.Thumbnail.Refresh();
    }
}
