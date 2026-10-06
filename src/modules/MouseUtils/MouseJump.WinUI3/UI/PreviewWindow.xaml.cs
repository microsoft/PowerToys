// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using ManagedCommon;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;

using MouseJump.Common.Helpers;
using MouseJump.Common.Interop;
using MouseJump.Models.Display;
using MouseJump.Models.Drawing;
using MouseJump.WinUI3.Helpers;

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

    private FormLayout? FormLayout
    {
        get;
        set;
    }

    private SettingsHelper SettingsHelper
    {
        get;
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
            $"\tright button = {pointerPoint.Properties.IsRightButtonPressed}",
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
