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
using Microsoft.PowerToys.Settings.UI.Library;
using MouseJumpUI.Helpers;
using MouseJumpUI.Models.Drawing;
using MouseJumpUI.Models.Layout;
using static MouseJumpUI.NativeMethods.Core;

namespace MouseJumpUI;

internal partial class MainForm : Form
{
    public MainForm(MouseJumpSettings settings)
    {
        this.InitializeComponent();
        this.Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.ShowThumbnail();
    }

    public MouseJumpSettings Settings
    {
        get;
    }

    private void MainForm_Load(object sender, EventArgs e)
    {
    }

    private void MainForm_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            this.OnDeactivate(EventArgs.Empty);
            return;
        }

        // map screens to their screen number in "System > Display"
        var screens = ScreenHelper.GetAllScreens()
            .Select((screen, index) => new { Screen = screen, Index = index, Number = index + 1 })
            .ToList();

        var currentLocation = MouseHelper.GetCursorPosition();
        var currentScreenHandle = ScreenHelper.MonitorFromPoint(currentLocation);
        var currentScreen = screens
            .Single(item => item.Screen.Handle == currentScreenHandle.Value);
        var targetScreenNumber = default(int?);

        if (((e.KeyCode >= Keys.D1) && (e.KeyCode <= Keys.D9))
            || ((e.KeyCode >= Keys.NumPad1) && (e.KeyCode <= Keys.NumPad9)))
        {
            // number keys 1-9 or numpad keys 1-9 - move to the numbered screen
            var screenNumber = e.KeyCode - Keys.D0;
            if (screenNumber <= screens.Count)
            {
                targetScreenNumber = screenNumber;
            }
        }
        else if (e.KeyCode == Keys.P)
        {
            // "P" - move to the primary screen
            targetScreenNumber = screens.Single(item => item.Screen.Primary).Number;
        }
        else if (e.KeyCode == Keys.Left)
        {
            // move to the previous screen
            targetScreenNumber = currentScreen.Number == 1
                ? screens.Count
                : currentScreen.Number - 1;
        }
        else if (e.KeyCode == Keys.Right)
        {
            // move to the next screen
            targetScreenNumber = currentScreen.Number == screens.Count
                ? 1
                : currentScreen.Number + 1;
        }
        else if (e.KeyCode == Keys.Home)
        {
            // move to the first screen
            targetScreenNumber = 1;
        }
        else if (e.KeyCode == Keys.End)
        {
            // move to the last screen
            targetScreenNumber = screens.Count;
        }

        if (targetScreenNumber.HasValue)
        {
            MouseHelper.SetCursorPosition(
                screens[targetScreenNumber.Value - 1].Screen.Bounds.Midpoint);
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
            maximumFormSize: new(
                form.Settings.Properties.ThumbnailSize.Width,
                form.Settings.Properties.ThumbnailSize.Height),
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

        var stopwatch = Stopwatch.StartNew();

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
            // sort the source and target screen areas, putting the activated screen first
            // (we need to capture and draw the activated screen before we show the form
            // because otherwise we'll capture the form as part of the screenshot!)
            var sourceScreens = layoutConfig.Screens
                .Where((_, idx) => idx == layoutConfig.ActivatedScreenIndex)
                .Union(layoutConfig.Screens.Where((_, idx) => idx != layoutConfig.ActivatedScreenIndex))
                .Select(screen => screen.Bounds)
                .ToList();
            var targetScreens = layoutInfo.ScreenBounds
                .Where((_, idx) => idx == layoutConfig.ActivatedScreenIndex)
                .Union(layoutInfo.ScreenBounds.Where((_, idx) => idx != layoutConfig.ActivatedScreenIndex))
                .ToList();

            DrawingHelper.EnsureDesktopDeviceContext(ref desktopHwnd, ref desktopHdc);
            DrawingHelper.EnsurePreviewDeviceContext(previewGraphics, ref previewHdc);

            var placeholdersDrawn = false;
            for (var i = 0; i < sourceScreens.Count; i++)
            {
                DrawingHelper.DrawPreviewScreen(
                    desktopHdc, previewHdc, sourceScreens[i], targetScreens[i]);

                // show the placeholder images and show the form if it looks like it might take
                // a while to capture the remaining screenshot images (but only if there are any)
                if ((i < (sourceScreens.Count - 1)) && (stopwatch.ElapsedMilliseconds > 250))
                {
                    // we need to release the device context handle before we draw the placeholders
                    // using the Graphics object otherwise we'll get an error from GDI saying
                    // "Object is currently in use elsewhere"
                    DrawingHelper.FreePreviewDeviceContext(previewGraphics, ref previewHdc);

                    if (!placeholdersDrawn)
                    {
                        // draw placeholders for any undrawn screens
                        DrawingHelper.DrawPreviewScreenPlaceholders(
                            previewGraphics,
                            targetScreens.Where((_, idx) => idx > i));
                        placeholdersDrawn = true;
                    }

                    MainForm.RefreshPreview(form);

                    // we've still got more screens to draw so open the device context again
                    DrawingHelper.EnsurePreviewDeviceContext(previewGraphics, ref previewHdc);
                }
            }
        }
        finally
        {
            DrawingHelper.FreeDesktopDeviceContext(ref desktopHwnd, ref desktopHdc);
            DrawingHelper.FreePreviewDeviceContext(previewGraphics, ref previewHdc);
        }

        MainForm.RefreshPreview(form);
        stopwatch.Stop();
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
