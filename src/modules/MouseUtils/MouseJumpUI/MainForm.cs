// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using MouseJumpUI.Helpers;
using NLog;

namespace MouseJumpUI;

internal partial class MainForm : Form
{
    public MainForm()
    {
        this.Logger = LogManager.CreateNullLogger();

        // var factory = LogManager.LoadConfiguration(".\\NLog.config");
        // this.Logger = factory.GetCurrentClassLogger();
        this.InitializeComponent();
        this.ShowThumbnail();
    }

    private Logger Logger
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
        var logger = this.Logger;

        logger.Debug("-----------");
        logger.Debug(nameof(MainForm.Thumbnail_Click));
        logger.Debug("-----------");

        var mouseEventArgs = (MouseEventArgs)e;
        logger.Debug($"mouse event args = ");
        logger.Debug($"    button   = {mouseEventArgs.Button} ");
        logger.Debug($"    location = {mouseEventArgs.Location} ");

        if (mouseEventArgs.Button == MouseButtons.Left)
        {
            // plain click - move mouse pointer
            var desktopBounds = LayoutHelper.CombineRegions(
                Screen.AllScreens.Select(
                    screen => screen.Bounds).ToList());
            logger.Debug(
                $"desktop bounds  = {desktopBounds}");

            var mouseEvent = (MouseEventArgs)e;

            var scaledLocation = LayoutHelper.ScaleLocation(
                originalBounds: Thumbnail.Bounds,
                originalLocation: new Point(mouseEvent.X, mouseEvent.Y),
                scaledBounds: desktopBounds);
            logger.Debug(
                $"scaled location = {scaledLocation}");

            Cursor.Position = cursorPosition;
        }

        this.Close();
    }

    public void ShowThumbnail()
    {
        var logger = this.Logger;

        logger.Debug("-----------");
        logger.Debug(nameof(MainForm.ShowThumbnail));
        logger.Debug("-----------");

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
            logger.Debug($"screen[{i}] = \"{screen.DeviceName}\"");
            logger.Debug($"    primary      = {screen.Primary}");
            logger.Debug($"    bounds       = {screen.Bounds}");
            logger.Debug($"    working area = {screen.WorkingArea}");
        }

        var desktopBounds = LayoutHelper.CombineRegions(
            screens.Select(screen => screen.Bounds).ToList());
        logger.Debug(
            $"desktop bounds  = {desktopBounds}");

        var activatedPosition = Cursor.Position;
        logger.Debug(
            $"activated position = {activatedPosition}");

        var previewImagePadding = new Size(
            panel1.Padding.Left + panel1.Padding.Right,
            panel1.Padding.Top + panel1.Padding.Bottom);
        logger.Debug(
            $"image padding   = {previewImagePadding}");

        var maxThumbnailSize = new Size(1600, 1200);
        var formBounds = LayoutHelper.GetPreviewFormBounds(
            desktopBounds: desktopBounds,
            activatedPosition: activatedPosition,
            activatedMonitorBounds: Screen.FromPoint(activatedPosition).Bounds,
            maximumThumbnailImageSize: maxThumbnailSize,
            thumbnailImagePadding: previewImagePadding);
        logger.Debug(
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

        // we have to activate the form to make sure the deactivate event fires
        this.Activate();
    }
}
