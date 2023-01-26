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
        this.InitializeComponent();
        this.ShowPreview();
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
            this.DesktopBounds = Rectangle.Empty;
        }

        this.Hide();
    }

    private void Thumbnail_Click(object sender, EventArgs e)
    {
        this.Logger.Debug("-----------");
        this.Logger.Debug(nameof(MainForm.Thumbnail_Click));
        this.Logger.Debug("-----------");

        var mouseEventArgs = (MouseEventArgs)e;
        this.Logger.Debug($"mouse event args = ");
        this.Logger.Debug($"    button   = {mouseEventArgs.Button} ");
        this.Logger.Debug($"    location = {mouseEventArgs.Location} ");

        if (mouseEventArgs.Button == MouseButtons.Left)
        {
            // plain click - move mouse pointer
            var desktopBounds = LayoutHelper.CombineRegions(
                Screen.AllScreens.Select(
                    screen => screen.Bounds).ToList());
            this.Logger.Debug(
                $"desktop bounds  = {desktopBounds}");

            var mouseEvent = (MouseEventArgs)e;

            var cursorPosition = LayoutHelper.ScaleLocation(
                originalBounds: Thumbnail.Bounds,
                originalLocation: new Point(mouseEvent.X, mouseEvent.Y),
                scaledBounds: desktopBounds);
            this.Logger.Debug(
                $"cursor position = {cursorPosition}");

            Cursor.Position = cursorPosition;
        }

        this.Hide();
    }

    public void ShowPreview()
    {
        this.Logger.Debug("-----------");
        this.Logger.Debug(nameof(MainForm.ShowPreview));
        this.Logger.Debug("-----------");

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
            this.Logger.Debug($"screen[{i}] = \"{screen.DeviceName}\"");
            this.Logger.Debug($"    primary      = {screen.Primary}");
            this.Logger.Debug($"    bounds       = {screen.Bounds}");
            this.Logger.Debug($"    working area = {screen.WorkingArea}");
        }

        var desktopBounds = LayoutHelper.CombineRegions(
            screens.Select(screen => screen.Bounds).ToList());
        this.Logger.Debug(
            $"desktop bounds  = {desktopBounds}");

        var cursorPosition = Cursor.Position;
        this.Logger.Debug(
            $"cursor position = {cursorPosition}");

        var previewImagePadding = new Size(
            panel1.Padding.Left + panel1.Padding.Right,
            panel1.Padding.Top + panel1.Padding.Bottom);
        this.Logger.Debug(
            $"image padding   = {previewImagePadding}");

        var formBounds = LayoutHelper.GetPreviewFormBounds(
            desktopBounds: desktopBounds,
            cursorPosition: cursorPosition,
            currentMonitorBounds: Screen.FromPoint(cursorPosition).Bounds,
            maximumPreviewImageSize: new Size(1600, 1200),
            previewImagePadding: previewImagePadding);
        this.Logger.Debug(
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

        // resize and position the form, and update the preview image
        this.Bounds = formBounds;
        this.Thumbnail.Image = screenshot;

        this.Show();

        // we have to activate the form to make sure the deactivate event fires
        this.Activate();
    }
}
