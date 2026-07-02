// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;

using MouseJump.Common.Helpers;
using MouseJump.Common.Imaging;
using MouseJump.Common.Interop;
using MouseJump.Models.Display;
using MouseJump.Models.Drawing;
using MouseJump.Models.ViewModel;
using MouseJump.WinUI3.Helpers;

using Windows.Graphics;
using Windows.System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace MouseJump.WinUI3.UI;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
internal sealed partial class PreviewWindow : Window
{
    internal PreviewWindow(SettingsHelper settingsHelper)
    {
        this.SettingsHelper = settingsHelper ?? throw new ArgumentNullException(nameof(settingsHelper));
        this.InitializeComponent();
        this.InitializeWindow();
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

    /// <summary>
    /// Initializes some settings on the application window.
    /// </summary>
    private void InitializeWindow()
    {
        var appWindow = this.AppWindow;
        var presenter = appWindow.Presenter as OverlappedPresenter;
        if (presenter != null)
        {
            var hWnd = (HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);

            // get the current window style
            PInvoke.SetLastError(0);
            var result = PInvoke.GetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
            if (result == 0)
            {
                var lastError = Marshal.GetLastPInvokeError();
                ResultHandler.HandleResult(result, success: lastError == 0, lastError, nameof(PInvoke.GetWindowLong));
            }

            // set the window to be borderless, with no title bar, and hide all of the max / min / close buttons
            var style = (WINDOW_STYLE)result;
            style &= ~WINDOW_STYLE.WS_OVERLAPPEDWINDOW;
            style |= WINDOW_STYLE.WS_POPUP;
            PInvoke.SetLastError(0);
            result = PInvoke.SetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, (int)style);
            if (result == 0)
            {
                var lastError = Marshal.GetLastPInvokeError();
                ResultHandler.HandleResult(result, success: lastError == 0, lastError, nameof(PInvoke.SetWindowLong));
            }

            // get the current extended window style
            PInvoke.SetLastError(0);
            result = PInvoke.GetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            if (result == 0)
            {
                var lastError = Marshal.GetLastPInvokeError();
                ResultHandler.HandleResult(result, success: lastError == 0, lastError, nameof(PInvoke.GetWindowLong));
            }

            // set the window to be borderless, with no title bar, and hide all of the max / min / close buttons
            var exStyle = (WINDOW_EX_STYLE)result;
            exStyle |= WINDOW_EX_STYLE.WS_EX_TOOLWINDOW; // hide the taskbar icon
            exStyle |= WINDOW_EX_STYLE.WS_EX_TOPMOST;    // make topmost
            PInvoke.SetLastError(0);
            result = PInvoke.SetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)exStyle);
            if (result == 0)
            {
                var lastError = Marshal.GetLastPInvokeError();
                ResultHandler.HandleResult(result, success: lastError == 0, lastError, nameof(PInvoke.SetWindowLong));
            }
        }

        this.Activated += this.PreviewWindow_Activated;
        this.StackPanel.PreviewKeyDown += this.PreviewWindow_PreviewKeyDown;
        this.PreviewImage.PreviewKeyDown += this.PreviewWindow_PreviewKeyDown;
        this.PreviewImage.PointerPressed += this.PreviewImage_PointerPressed;
    }

    private void PreviewWindow_Activated(object sender, WindowActivatedEventArgs e)
    {
        switch (e.WindowActivationState)
        {
            case WindowActivationState.CodeActivated:
                this.PreviewImage.Focus(FocusState.Programmatic);
                break;
            case WindowActivationState.Deactivated:
                this.HideWindow();
                break;
            default:
                throw new InvalidOperationException();
        }
    }

    private void PreviewWindow_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            this.HideWindow();
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

        switch (e.Key)
        {
            case >= VirtualKey.Number1 and <= VirtualKey.Number9:
                {
                    // number keys 1-9 - move to the numbered screen
                    var screenNumber = e.Key - VirtualKey.Number0;
                    /* note - screen *numbers* are 1-based, screen *indexes* are 0-based */
                    targetScreen = (screenNumber <= screens.Count)
                        ? targetScreen = screens[screenNumber - 1]
                        : null;
                    break;
                }

            case >= VirtualKey.NumberPad1 and <= VirtualKey.NumberPad9:
                {
                    // numpad keys 1-9 - move to the numbered screen
                    var screenNumber = e.Key - VirtualKey.NumberPad0;
                    /* note - screen *numbers* are 1-based, screen *indexes* are 0-based */
                    targetScreen = (screenNumber <= screens.Count)
                        ? targetScreen = screens[screenNumber - 1]
                        : null;
                    break;
                }

            case VirtualKey.P:
                // "P" - move to the primary screen
                targetScreen = screens.Single(screen => screen.Primary);
                break;
            case VirtualKey.Left:
                // move to the previous screen, looping back to the end if needed
                var prevIndex = (currentScreenIndex - 1 + screens.Count) % screens.Count;
                targetScreen = screens[prevIndex];
                break;
            case VirtualKey.Right:
                // move to the next screen, looping round to the start if needed
                var nextIndex = (currentScreenIndex + 1) % screens.Count;
                targetScreen = screens[nextIndex];
                break;
            case VirtualKey.Home:
                // move to the first screen
                targetScreen = screens.First();
                break;
            case VirtualKey.End:
                // move to the last screen
                targetScreen = screens.Last();
                break;
        }

        if (targetScreen is not null)
        {
            MouseHelper.SetCursorPosition(targetScreen.DisplayArea.Midpoint);
            this.HideWindow();
        }
    }

    private void PreviewImage_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        Logger.LogInfo(string.Join(
            '\n',
            "-----------",
            nameof(PreviewWindow.PreviewImage_PointerPressed),
            "-----------"));

        if (!e.Pointer.PointerDeviceType.Equals(PointerDeviceType.Mouse))
        {
            // not a mouse click
            return;
        }

        var pointerPoint = e.GetCurrentPoint((UIElement)sender);
        Logger.LogInfo(string.Join(
            '\n',
            "Reporting mouse event args",
            $"\tleft button = {pointerPoint.Properties.IsLeftButtonPressed}",
            $"\right button = {pointerPoint.Properties.IsRightButtonPressed}",
            $"\tlocation = {pointerPoint.Position}"));

        if (pointerPoint.Properties.IsLeftButtonPressed)
        {
            if (this.FormLayout is null)
            {
                // there's no layout data so we can't work out what screen was clicked
                throw new InvalidOperationException();
            }

            // get the *scaled* pointer location
            var pointerLocation = new PointInfo((decimal)pointerPoint.Position.X, (decimal)pointerPoint.Position.Y);

            // we need to apply the high-dpi scaling ratio for the current monitor to the pointer location
            var highDpiScalingRatio = this.GetHighDpiScalingRatio();
            pointerLocation = pointerLocation.Scale(1 / (decimal)highDpiScalingRatio);

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
            MouseHelper.SetCursorPosition(clickedLocation);
        }

        this.HideWindow();
    }

    public async Task ShowPreviewAsync()
    {
        Logger.LogInfo(string.Join(
            '\n',
            "-----------",
            nameof(PreviewWindow.ShowPreviewAsync),
            "-----------"));

        // hide the form while we redraw it...
        await this.HideWindowAsync()
            .ConfigureAwait(false);

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

        await this.PositionWindowAsync(formLayout.FormBounds)
            .ConfigureAwait(false);

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

        PowerToysTelemetry.Log.WriteEvent(new Telemetry.MouseJumpShowEvent());

        await this.ShowWindowAsync()
            .ConfigureAwait(false);
    }

    private void ClearPreview()
    {
        if (this.PreviewImage.Source is null)
        {
            return;
        }

        this.PreviewImage.Source = null;

        // force preview image memory to be released - otherwise
        // all the disposed images can pile up without being GC'ed
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    /// <summary>
    /// Calculates the high-dpi scaling ratio based on the current monitor's display settings.
    /// </summary>
    private double GetHighDpiScalingRatio()
    {
        var hWnd = (HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowDpi = PInvoke.GetDpiForWindow(hWnd);
        ResultHandler.ThrowIfZero((int)windowDpi, getLastError: true, nameof(PInvoke.GetDpiForWindow));
        var scalingRatio = (double)PInvoke.USER_DEFAULT_SCREEN_DPI / windowDpi;
        return scalingRatio;
    }

    private async Task InvokeOnUiThreadAsync(Action action)
    {
        // this might be called from a task that we're awaiting
        // so we need to make sure we use the UI thread
        var tcs = new TaskCompletionSource<bool>();

        this.DispatcherQueue.TryEnqueue(
            () =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

        // wait for the task to complete
        await tcs.Task.ConfigureAwait(false);
    }

    private void HideWindow()
    {
        this.AppWindow.Hide();
        this.ClearPreview();
    }

    private async Task HideWindowAsync()
    {
        await this.InvokeOnUiThreadAsync(
            () =>
            {
                this.HideWindow();
            }).ConfigureAwait(false);
    }

    private async Task ShowWindowAsync()
    {
        await this.InvokeOnUiThreadAsync(
            () =>
            {
                var presenter = this.AppWindow.Presenter as OverlappedPresenter
                    ?? throw new InvalidOperationException();

                if (!this.Visible)
                {
                    // we seem to need to turn off topmost and then re-enable it again
                    // when we show the form - otherwise it doesn't always get shown topmost...
                    presenter.IsAlwaysOnTop = false;
                    presenter.IsAlwaysOnTop = true;
                }

                this.AppWindow.Show();

                // we have to activate the window to make sure the deactivate event fires
                this.Activate();
                this.PreviewImage.Focus(FocusState.Programmatic);
            }).ConfigureAwait(false);
    }

    /// <summary>
    /// Resize and position the form.
    /// </summary>
    private async Task PositionWindowAsync(RectangleInfo bounds)
    {
        await this.InvokeOnUiThreadAsync(
            () =>
            {
                // note - do this with two calls to MoveAndResize rather than one as there appears to
                // be an issue with dpi scaling even when using PerMonitorV2, where if the window is
                // resized *and* moved in one call the resize uses the scaling of the *current*
                // monitor before it's moved.
                //
                // If the move then happens to be to a different monitor, *and* the monitor has a
                // different dpi scaling configured, the window size is then wrong for the dpi scaling
                // of the new monitor.
                //
                // the workaround seems to be to call MoveAndResize twice - the first call might
                // resize it incorrectly, but it moves the window to the correct monitor, and the
                // second call then resizes it correctly.
                //
                // see https://github.com/mikeclayton/FancyMouse/issues/2 for more details
                var windowBounds = new RectInt32((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height);
                this.AppWindow.MoveAndResize(windowBounds);
                this.AppWindow.MoveAndResize(windowBounds);
            }).ConfigureAwait(false);
    }

    private async Task OnPreviewImageCreatedAsync(Bitmap preview)
    {
        await this.InvokeOnUiThreadAsync(
            () =>
            {
                this.ClearPreview();

                // we need to apply the high-dpi scaling ratio for the current monitor to the image control size
                var highDpiScalingRatio = this.GetHighDpiScalingRatio();

                this.PreviewImage.Width = preview.Width * highDpiScalingRatio;
                this.PreviewImage.Height = preview.Height * highDpiScalingRatio;
            }).ConfigureAwait(false);
    }

    private async Task OnPreviewImageUpdatedAsync(Bitmap preview)
    {
        await this.InvokeOnUiThreadAsync(
            () =>
            {
                this.ClearPreview();

                var bitmapImage = new BitmapImage();
                using (var stream = new MemoryStream())
                {
                    preview.Save(stream, ImageFormat.Png);
                    stream.Position = 0;
                    bitmapImage.SetSource(stream.AsRandomAccessStream());
                }

                this.PreviewImage.Source = bitmapImage;
            }).ConfigureAwait(false);
    }
}
