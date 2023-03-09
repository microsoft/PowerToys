// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using MouseJumpUI.Drawing;
using MouseJumpUI.Drawing.Models;
using MouseJumpUI.Helpers;
using MouseJumpUI.NativeMethods.Core;
using MouseJumpUI.NativeWrappers;

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
        // dispose the existing image if there is one
        if (Thumbnail.Image != null)
        {
            Thumbnail.Image.Dispose();
            Thumbnail.Image = null;
        }

        this.Close();
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
            var desktopBounds = SystemInformation.VirtualScreen;
            Logger.LogInfo($"desktop bounds  = {desktopBounds}");

            var mouseEvent = (MouseEventArgs)e;

            var scaledLocation = new PointInfo(mouseEvent.X, mouseEvent.Y)
                    .Scale(new SizeInfo(this.Thumbnail.Size).ScaleToFitRatio(new(desktopBounds.Size)))
                    .ToPoint();
            Logger.LogInfo($"scaled location = {scaledLocation}");

            // set the new cursor position *twice* - the cursor sometimes end up in
            // the wrong place if we try to cross the dead space between non-aligned
            // monitors - e.g. when trying to move the cursor from (a) to (b) we can
            // *sometimes* - for no clear reason - end up at (c) instead.
            //
            //           +----------------+
            //           |(c)    (b)      |
            //           |                |
            //           |                |
            //           |                |
            // +---------+                |
            // |  (a)    |                |
            // +---------+----------------+
            //
            // setting the position a second time seems to fix this and moves the
            // cursor to the expected location (b) - for more details see
            // https://github.com/mikeclayton/FancyMouse/pull/3
            Cursor.Position = scaledLocation;
            Cursor.Position = scaledLocation;
            Microsoft.PowerToys.Telemetry.PowerToysTelemetry.Log.WriteEvent(new Telemetry.MouseJumpTeleportCursorEvent());
        }

        this.Close();

        if (this.Thumbnail.Image != null)
        {
            var tmp = this.Thumbnail.Image;
            this.Thumbnail.Image = null;
            tmp.Dispose();
        }
    }

    public void ShowThumbnail()
    {
        var screens = Screen.AllScreens;
        foreach (var i in Enumerable.Range(0, screens.Length))
        {
            var screen = screens[i];
            Logger.LogInfo(string.Join(
                '\n',
                $"screen[{i}] = \"{screen.DeviceName}\"",
                $"\tprimary      = {screen.Primary}",
                $"\tbounds       = {screen.Bounds}",
                $"\tworking area = {screen.WorkingArea}"));
        }

        // collect together some values that we need for calculating layout
        var activatedLocation = Cursor.Position;
        var layoutConfig = new LayoutConfig(
            virtualScreen: SystemInformation.VirtualScreen,
            screenBounds: Screen.AllScreens.Select(screen => screen.Bounds),
            activatedLocation: activatedLocation,
            activatedScreen: Array.IndexOf(Screen.AllScreens, Screen.FromPoint(activatedLocation)),
            maximumFormSize: new Size(1600, 1200),
            formPadding: this.panel1.Padding,
            previewPadding: new Padding(0));
        Logger.LogInfo(string.Join(
            '\n',
            $"Layout config",
            $"-------------",
            $"virtual screen     = {layoutConfig.VirtualScreen}",
            $"activated location = {layoutConfig.ActivatedLocation}",
            $"activated screen   = {layoutConfig.ActivatedScreen}",
            $"maximum form size  = {layoutConfig.MaximumFormSize}",
            $"form padding       = {layoutConfig.FormPadding}",
            $"preview padding    = {layoutConfig.PreviewPadding}"));

        // calculate the layout coordinates for everything
        var layoutCoords = PreviewImageComposer.CalculateCoords(layoutConfig);
        Logger.LogInfo(string.Join(
            '\n',
            $"Layout coords",
            $"-------------",
            $"form bounds      = {layoutCoords.FormBounds}",
            $"preview bounds   = {layoutCoords.PreviewBounds}",
            $"activated screen = {layoutCoords.ActivatedScreen}"));

        // resize and position the form
        // note - do this in two steps rather than "this.Bounds = formBounds" as there
        // appears to be an issue in WinForms with dpi scaling even when using PerMonitorV2,
        // where the form scaling uses either the *primary* screen scaling or the *previous*
        // screen's scaling when the form is moved to a different screen. i've got no idea
        // *why*, but the exact sequence of calls below seems to be a workaround...
        // see https://github.com/mikeclayton/FancyMouse/issues/2
        this.Location = layoutCoords.FormBounds.Location.ToPoint();
        _ = this.PointToScreen(Point.Empty);
        this.Size = layoutCoords.FormBounds.Size.ToSize();

        // initialize the preview image
        var preview = new Bitmap(
            (int)layoutCoords.PreviewBounds.Width,
            (int)layoutCoords.PreviewBounds.Height,
            PixelFormat.Format32bppArgb);
        this.Thumbnail.Image = preview;

        using var previewGraphics = Graphics.FromImage(preview);

        // draw the preview background
        using var backgroundBrush = new LinearGradientBrush(
            new Point(0, 0),
            new Point(preview.Width, preview.Height),
            Color.FromArgb(13, 87, 210),
            Color.FromArgb(3, 68, 192));
        previewGraphics.FillRectangle(backgroundBrush, layoutCoords.PreviewBounds.ToRectangle());

        var previewHdc = HDC.Null;
        var desktopHwnd = HWND.Null;
        var desktopHdc = HDC.Null;
        try
        {
            desktopHwnd = User32.GetDesktopWindow();
            desktopHdc = User32.GetWindowDC(desktopHwnd);

            // we have to capture the screen where we're going to show the form first
            // as the form will obscure the screen as soon as it's visible
            var stopwatch = Stopwatch.StartNew();
            previewHdc = new HDC(previewGraphics.GetHdc());
            PreviewImageComposer.CopyFromScreen(
                desktopHdc,
                previewHdc,
                layoutConfig.ScreenBounds.Where((_, idx) => idx == layoutConfig.ActivatedScreen).ToList(),
                layoutCoords.ScreenBounds.Where((_, idx) => idx == layoutConfig.ActivatedScreen).ToList());
            previewGraphics.ReleaseHdc(previewHdc.Value);
            previewHdc = HDC.Null;
            stopwatch.Stop();

            // show the placeholder image if it looks like it might take a while to capture
            // the remaining screenshot images
            if (stopwatch.ElapsedMilliseconds > 150)
            {
                var activatedArea = layoutConfig.ScreenBounds[layoutConfig.ActivatedScreen].Area;
                var totalArea = layoutConfig.ScreenBounds.Sum(screen => screen.Area);
                if ((activatedArea / totalArea) < 0.5M)
                {
                    var brush = Brushes.Black;
                    var bounds = layoutCoords.ScreenBounds
                        .Where((_, idx) => idx != layoutConfig.ActivatedScreen)
                        .Select(screen => screen.ToRectangle())
                        .ToArray();
                    if (bounds.Any())
                    {
                        previewGraphics.FillRectangles(brush, bounds);
                    }

                    this.Show();
                    this.Thumbnail.Refresh();
                }
            }

            // draw the remaining screen captures on the preview image
            previewHdc = new HDC(previewGraphics.GetHdc());
            PreviewImageComposer.CopyFromScreen(
                desktopHdc,
                previewHdc,
                layoutConfig.ScreenBounds.Where((_, idx) => idx != layoutConfig.ActivatedScreen).ToList(),
                layoutCoords.ScreenBounds.Where((_, idx) => idx != layoutConfig.ActivatedScreen).ToList());
            previewGraphics.ReleaseHdc(previewHdc.Value);
            previewHdc = HDC.Null;
            this.Thumbnail.Refresh();
        }
        finally
        {
            if (!desktopHwnd.IsNull && !desktopHdc.IsNull)
            {
                _ = User32.ReleaseDC(desktopHwnd, desktopHdc);
            }

            if (!previewHdc.IsNull)
            {
                previewGraphics.ReleaseHdc(previewHdc.Value);
            }
        }

        if (!this.Visible)
        {
            this.Show();
        }

        // we have to activate the form to make sure the deactivate event fires
        this.Activate();
    }
}
