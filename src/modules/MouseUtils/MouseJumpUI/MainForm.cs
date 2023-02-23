// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using MouseJumpUI.Helpers;

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
        Logger.LogInfo($"Reporting mouse event args \n\tbutton   = {mouseEventArgs.Button}\n\tlocation = {mouseEventArgs.Location} ");

        if (mouseEventArgs.Button == MouseButtons.Left)
        {
            // plain click - move mouse pointer
            var desktopBounds = LayoutHelper.CombineRegions(
                Screen.AllScreens.Select(
                    screen => screen.Bounds).ToList());
            Logger.LogInfo($"desktop bounds  = {desktopBounds}");

            var mouseEvent = (MouseEventArgs)e;

            var scaledLocation = LayoutHelper.ScaleLocation(
                originalBounds: Thumbnail.Bounds,
                originalLocation: new Point(mouseEvent.X, mouseEvent.Y),
                scaledBounds: desktopBounds);
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
    }

    public void ShowThumbnail()
    {
        if (this.Thumbnail.Image != null)
        {
            var tmp = this.Thumbnail.Image;
            this.Thumbnail.Image = null;
            tmp.Dispose();
        }

        var screens = Screen.AllScreens;
        foreach (var i in Enumerable.Range(0, screens.Length))
        {
            var screen = screens[i];
            Logger.LogInfo($"screen[{i}] = \"{screen.DeviceName}\"\n\tprimary      = {screen.Primary}\n\tbounds       = {screen.Bounds}\n\tworking area = {screen.WorkingArea}");
        }

        var desktopBounds = LayoutHelper.CombineRegions(
            screens.Select(screen => screen.Bounds).ToList());
        Logger.LogInfo(
            $"desktop bounds  = {desktopBounds}");

        var activatedPosition = Cursor.Position;
        Logger.LogInfo(
            $"activated position = {activatedPosition}");

        var previewImagePadding = new Size(
            panel1.Padding.Left + panel1.Padding.Right,
            panel1.Padding.Top + panel1.Padding.Bottom);
        Logger.LogInfo(
            $"image padding   = {previewImagePadding}");

        var maxThumbnailSize = new Size(1600, 1200);
        var formBounds = LayoutHelper.GetPreviewFormBounds(
            desktopBounds: desktopBounds,
            activatedPosition: activatedPosition,
            activatedMonitorBounds: Screen.FromPoint(activatedPosition).Bounds,
            maximumThumbnailImageSize: maxThumbnailSize,
            thumbnailImagePadding: previewImagePadding);
        Logger.LogInfo(
            $"form bounds     = {formBounds}");

        // take a screenshot of the entire desktop
        // see https://learn.microsoft.com/en-gb/windows/win32/gdi/the-virtual-screen
        var screenshot = new Bitmap(desktopBounds.Width, desktopBounds.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(screenshot))
        {
            // note - it *might* be faster to capture each monitor individually and assemble them into
            // a single image ourselves as we *may* not have to transfer all of the blank pixels
            // that are outside the desktop bounds - e.g. the *** in the ascii art below
            //
            // +----------------+********
            // |                |********
            // |       1        +-------+
            // |                |       |
            // +----------------+   0   |
            // *****************|       |
            // *****************+-------+
            //
            // for very irregular monitor layouts this *might* be a big percentage of the rectangle
            // containing the desktop bounds.
            //
            // then again, it might not make much difference at all - we'd need to do some perf tests
            graphics.CopyFromScreen(desktopBounds.Left, desktopBounds.Top, 0, 0, desktopBounds.Size);
        }

        // resize and position the form
        // note - do this in two steps rather than "this.Bounds = formBounds" as there
        // appears to be an issue in WinForms with dpi scaling even when using PerMonitorV2,
        // where the form scaling uses either the *primary* screen scaling or the *previous*
        // screen's scaling when the form is moved to a different screen. i've got no idea
        // *why*, but the exact sequence of calls below seems to be a workaround...
        // see https://github.com/mikeclayton/FancyMouse/issues/2
        this.Location = formBounds.Location;
        _ = this.PointToScreen(Point.Empty);
        this.Size = formBounds.Size;

        // update the preview image
        this.Thumbnail.Image = screenshot;

        this.Show();
        Microsoft.PowerToys.Telemetry.PowerToysTelemetry.Log.WriteEvent(new Telemetry.MouseJumpTeleportCursorEvent());

        // we have to activate the form to make sure the deactivate event fires
        this.Activate();
    }
}
