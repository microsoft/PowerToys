// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

using ManagedCommon;

using MouseJump.Common.Helpers;
using MouseJump.Common.Imaging;
using MouseJump.Models.Display;
using MouseJump.Models.Drawing;
using MouseJump.Models.ViewModel;

using MouseJumpUI.Helpers;

namespace MouseJumpUI.UI;

internal sealed partial class MainForm : Form
{
    public MainForm(SettingsHelper settingsHelper)
    {
        this.InitializeComponent();
        this.SettingsHelper = settingsHelper ?? throw new ArgumentNullException(nameof(settingsHelper));
    }

    private FormViewModel? FormLayout
    {
        get;
        set;
    }

    private SettingsHelper SettingsHelper
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

        var screens = ScreenHelper.GetAllScreens().ToList();
        if (screens.Count == 0)
        {
            return;
        }

        var currentLocation = MouseHelper.GetCursorPosition();
        var currentScreen = ScreenHelper.GetScreenFromPoint(screens, currentLocation);
        var currentScreenIndex = screens.IndexOf(currentScreen);
        var targetScreen = default(ScreenInfo?);

        switch (e.KeyCode)
        {
            case >= Keys.D1 and <= Keys.D9:
                {
                    // number keys 1-9 - move to the numbered screen
                    var screenNumber = e.KeyCode - Keys.D0;
                    /* note - screen *numbers* are 1-based, screen *indexes* are 0-based */
                    targetScreen = (screenNumber <= screens.Count)
                        ? targetScreen = screens[screenNumber - 1]
                        : null;
                    break;
                }

            case >= Keys.NumPad1 and <= Keys.NumPad9:
                {
                    // numpad keys 1-9 - move to the numbered screen
                    var screenNumber = e.KeyCode - Keys.NumPad0;
                    /* note - screen *numbers* are 1-based, screen *indexes* are 0-based */
                    targetScreen = (screenNumber <= screens.Count)
                        ? targetScreen = screens[screenNumber - 1]
                        : null;
                    break;
                }

            case Keys.P:
                // "P" - move to the primary screen
                targetScreen = screens.Single(screen => screen.Primary);
                break;
            case Keys.Left:
                // move to the previous screen, looping back to the end if needed
                var prevIndex = (currentScreenIndex - 1 + screens.Count) % screens.Count;
                targetScreen = screens[prevIndex];
                break;
            case Keys.Right:
                // move to the next screen, looping round to the start if needed
                var nextIndex = (currentScreenIndex + 1) % screens.Count;
                targetScreen = screens[nextIndex];
                break;
            case Keys.Home:
                // move to the first screen
                targetScreen = screens.First();
                break;
            case Keys.End:
                // move to the last screen
                targetScreen = screens.Last();
                break;
        }

        if (targetScreen is not null)
        {
            MouseHelper.SetCursorPosition(targetScreen.DisplayArea.Midpoint);
            this.OnDeactivate(EventArgs.Empty);
        }
    }

    private void MainForm_Deactivate(object sender, EventArgs e)
    {
        this.Hide();
        this.ClearPreview();
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
            if (this.FormLayout is null)
            {
                // there's no layout data so we can't work out what screen was clicked
                throw new InvalidOperationException();
            }

            // get the *scaled* pointer location
            var pointerLocation = new PointInfo(mouseEventArgs.Location);

            // work out which screenshot was clicked
            var clickedScreen = this.FormLayout.CanvasLayout.DeviceLayouts
                .SelectMany(deviceLayout => deviceLayout.ScreenLayouts)
                .FirstOrDefault(
                    screenLayout => screenLayout.ScreenBounds.OuterBounds.Contains(pointerLocation));
            if (clickedScreen is null)
            {
                return;
            }

            // find the device the clicked screenshot belongs to
            var clickedDevice = this.FormLayout.CanvasLayout.DeviceLayouts
                .FirstOrDefault(
                    deviceLayout => deviceLayout.ScreenLayouts.Contains(clickedScreen));
            if (clickedDevice is null)
            {
                return;
            }

            // scale up the click onto the physical screen - the aspect ratio of the screenshot
            // might be distorted compared to the physical screen due to the borders around the
            // screenshot, so we need to work out the target location on the physical screen first
            var clickedDisplayArea = clickedScreen.ScreenInfo.DisplayArea;
            var clickedLocation = pointerLocation
                .Stretch(
                    source: clickedScreen.ScreenBounds.ContentBounds,
                    target: clickedDisplayArea)
                .Clamp(
                    new(
                        x: clickedDisplayArea.X + 1,
                        y: clickedDisplayArea.Y + 1,
                        width: clickedDisplayArea.Width - 1,
                        height: clickedDisplayArea.Height - 1
                    ))
                .Truncate();

            // move mouse pointer
            Logger.LogInfo($"clicked location = {clickedLocation}");
            Microsoft.PowerToys.Telemetry.PowerToysTelemetry.Log.WriteEvent(new Telemetry.MouseJumpTeleportCursorEvent());
            MouseHelper.SetCursorPosition(clickedLocation);
        }

        this.OnDeactivate(EventArgs.Empty);
    }

    public async Task ShowPreviewAsync()
    {
        // hide the form while we redraw it...
        this.Visible = false;

        var stopwatch = Stopwatch.StartNew();

        // capture this first so we get an accurate mouse location
        // (in case the user moves it a few pixels while the form is rendered)
        var activatedLocation = MouseHelper.GetCursorPosition();

        var appSettings = this.SettingsHelper.CurrentSettings ?? throw new InvalidOperationException();
        var displayInfo = DeviceHelper.GetDisplayInfo();

        var activatedScreen = DeviceHelper.GetActivatedScreen(displayInfo.Devices[0], activatedLocation);

        var previewStyle = SettingsHelper.GetActivePreviewStyle(appSettings);
        var formLayout = LayoutHelper.GetFormLayout(
            previewStyle,
            displayInfo,
            activatedScreen: activatedScreen,
            activatedLocation: activatedLocation);

        // remember the layout so we can map the mouse clicks back to
        // the appropriate device and screen location
        this.FormLayout = formLayout;

        await this.PositionFormAsync(formLayout.FormBounds);

        var imageCopyServices = displayInfo.Devices
            .Select(
                deviceInfo => (IImageRegionCopyService)new DesktopImageRegionCopyService())
            .ToList();

        await DrawingHelper.RenderPreviewAsync(
                this.FormLayout.CanvasLayout,
                activatedScreen,
                imageCopyServices,
                this.OnPreviewImageCreatedAsync,
                this.OnPreviewImageUpdatedAsync)
            .ConfigureAwait(false);

        stopwatch.Stop();

        Microsoft.PowerToys.Telemetry.PowerToysTelemetry.Log.WriteEvent(new Telemetry.MouseJumpShowEvent());

        // we have to activate the form to make sure the deactivate event fires
        await this.ActivateAsync();
    }

    private void ClearPreview()
    {
        if (this.Thumbnail.Image is null)
        {
            return;
        }

        var tmp = this.Thumbnail.Image;
        this.Thumbnail.Image = null;
        tmp.Dispose();

        // force preview image memory to be released - otherwise
        // all the disposed images can pile up without being GC'ed
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    /// <summary>
    /// Resize and position the specified form.
    /// </summary>
    private async Task PositionFormAsync(RectangleInfo bounds)
    {
        await this.InvokeAsync(
            () =>
            {
                // note - do this in two steps rather than "this.Bounds = formBounds" as there
                // appears to be an issue in WinForms with dpi scaling even when using PerMonitorV2,
                // where the form scaling uses either the *primary* screen scaling or the *previous*
                // screen's scaling when the form is moved to a different screen. i've got no idea
                // *why*, but the exact sequence of calls below seems to be a workaround...
                // see https://github.com/mikeclayton/FancyMouse/issues/2
                var rect = bounds.ToRectangle();
                this.Location = rect.Location;
                _ = this.PointToScreen(Point.Empty);
                this.Size = rect.Size;
            });
    }

    private async Task ActivateAsync()
    {
        await this.InvokeAsync(
            () =>
            {
                this.Activate();
            });
    }

    private async Task OnPreviewImageCreatedAsync(Bitmap preview)
    {
        await this.InvokeAsync(
            () =>
            {
                this.ClearPreview();
                this.Thumbnail.Image = preview;
            });
    }

    private async Task OnPreviewImageUpdatedAsync(Bitmap preview)
    {
        await this.InvokeAsync(
            () =>
            {
                if (!this.Visible)
                {
                    // we seem to need to turn off topmost and then re-enable it again
                    // when we show the form, otherwise it doesn't always get shown topmost...
                    this.TopMost = false;
                    this.TopMost = true;
                    this.Show();
                }

                this.Thumbnail.Refresh();
            });
    }
}
