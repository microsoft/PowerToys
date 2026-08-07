// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

using Microsoft.UI.Windowing;
using PowerOCR.Models;
using PowerOCR.Services;
using PowerOCR.ViewModels;
using Windows.Graphics;
using WinUIEx;

namespace PowerOCR;

/// <summary>
/// Borderless topmost overlay window that covers a single display.
/// One instance is created per captured display.
/// </summary>
public sealed partial class OCROverlay : WindowEx
{
    private const uint DwmwaColorNone = 0xFFFFFFFE;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwcpDoNotRound = 1;

    private readonly DisplayCapture _capture;
    private readonly IOverlayManager _manager;
    private bool _closingFromManager;
    private bool _closeAllQueued;

    public OCROverlay(
        DisplayCapture capture,
        OverlaySessionViewModel viewModel,
        IOverlayManager manager,
        SettingsDeepLink settingsDeepLink)
    {
        _capture = capture;
        _manager = manager;

        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

        nint hwnd = this.GetWindowHandle();
        HwndExtensions.ToggleWindowStyle(hwnd, false, WindowStyle.TiledWindow);

        uint borderColor = DwmwaColorNone;
        _ = SetDwmUInt32Attribute(hwnd, DwmwaBorderColor, ref borderColor, sizeof(uint));

        int cornerPreference = DwmwcpDoNotRound;
        _ = SetDwmInt32Attribute(hwnd, DwmwaWindowCornerPreference, ref cornerPreference, sizeof(int));

        SystemBackdrop = new TransparentTintBackdrop();

        OverlayContent.Initialize(this, capture, manager, viewModel, settingsDeepLink);

        PositionOnDisplay();

        AppWindow.Closing += OnAppWindowClosing;
    }

    public void PositionOnDisplay()
    {
        var bounds = _capture.Bounds;
        AppWindow.MoveAndResize(new RectInt32(bounds.X, bounds.Y, bounds.Width, bounds.Height));
    }

    /// <summary>
    /// Shows an error message in the overlay InfoBar.
    /// </summary>
    public void ShowError(string message)
    {
        OverlayContent.ShowError(message);
    }

    /// <summary>
    /// Closes this window without re-triggering CloseAll from the manager.
    /// Called by OverlayManager during session teardown.
    /// </summary>
    public void CloseFromManager()
    {
        _closingFromManager = true;
        Close();
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closingFromManager)
        {
            return;
        }

        // Let the native closing callback return before closing this and the other overlays.
        // Closing the current Window synchronously from AppWindow.Closing would re-enter
        // destruction of the same HWND.
        args.Cancel = true;
        if (_closeAllQueued)
        {
            return;
        }

        _closeAllQueued = true;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            _closeAllQueued = false;
            if (!_closingFromManager)
            {
                _manager.CloseAll(cancelled: true);
            }
        }))
        {
            // A dispatcher that rejects new work is shutting down. Allow the native close
            // instead of leaving a window that can no longer be serviced.
            _closeAllQueued = false;
            args.Cancel = false;
        }
    }

    [LibraryImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
    private static partial int SetDwmUInt32Attribute(nint hwnd, int attribute, ref uint value, int size);

    [LibraryImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
    private static partial int SetDwmInt32Attribute(nint hwnd, int attribute, ref int value, int size);
}
