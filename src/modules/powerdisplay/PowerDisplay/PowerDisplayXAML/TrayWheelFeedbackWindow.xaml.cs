// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using PowerDisplay.Common.Services;
using Windows.Foundation;
using Windows.Graphics;
using WinRT.Interop;

namespace PowerDisplay.PowerDisplayXAML;

/// <summary>
/// No-activate, click-through visual host for tray hover and wheel feedback.
/// </summary>
public sealed partial class TrayWheelFeedbackWindow : TransparentWindow, IDisposable
{
    private const int GwlExStyle = -20;
    private const int GwlWndProc = -4;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExTransparent = 0x00000020;
    private const uint WmNcHitTest = 0x0084;
    private static readonly nint HtTransparent = -1;

    private readonly nint _hwnd;
    private nint _originalWndProc;
    private WndProcDelegate? _wndProcDelegate;
    private string? _currentText;
    private TrayIconBounds? _currentIconBounds;
    private bool _isVisible;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayWheelFeedbackWindow"/> class.
    /// </summary>
    public TrayWheelFeedbackWindow()
    {
        InitializeComponent();
        IsAlwaysOnTop = true;
        _hwnd = WindowNative.GetWindowHandle(this);
        ApplyExtendedStyles();

        // Keep the delegate alive in a field so the GC does not collect it before we
        // restore the original procedure.
        _wndProcDelegate = WndProc;
        Marshal.SetLastPInvokeError(0);
        _originalWndProc = SetWindowLongPtrNative(
            _hwnd,
            GwlWndProc,
            Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
        var error = Marshal.GetLastPInvokeError();
        if (_originalWndProc == 0 && error != 0)
        {
            throw new Win32Exception(error);
        }

        Closed += (_, _) => Dispose();
    }

    private delegate nint WndProcDelegate(
        nint hwnd,
        uint message,
        nuint wParam,
        nint lParam);

    /// <summary>
    /// Shows or updates feedback text at the supplied notification-icon rectangle.
    /// </summary>
    /// <param name="text">The text to display.</param>
    /// <param name="iconBounds">The screen rectangle of the tray icon (physical pixels).</param>
    /// <returns><see langword="true"/> when the window was placed; <see langword="false"/> when
    /// no display area could be resolved.</returns>
    public bool ShowText(string text, TrayIconBounds iconBounds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var center = new PointInt32(
            iconBounds.Left + ((iconBounds.Right - iconBounds.Left) / 2),
            iconBounds.Top + ((iconBounds.Bottom - iconBounds.Top) / 2));
        var displayArea = DisplayArea.GetFromPoint(center, DisplayAreaFallback.Nearest);
        if (displayArea is null)
        {
            return false;
        }

        var textChanged = !string.Equals(_currentText, text, StringComparison.Ordinal);
        if (_isVisible &&
            !textChanged &&
            _currentIconBounds == iconBounds)
        {
            return true;
        }

        FeedbackText.Text = text;
        FeedbackRoot.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var widthDip = Math.Clamp(
            (int)Math.Ceiling(FeedbackRoot.DesiredSize.Width),
            120,
            436);
        var heightDip = Math.Max(
            1,
            (int)Math.Ceiling(FeedbackRoot.DesiredSize.Height));
        var dpiScale = FlyoutWindowHelper.GetDpiScale(displayArea);
        var width = FlyoutWindowHelper.ScaleToPhysicalPixels(widthDip, dpiScale);
        var height = FlyoutWindowHelper.ScaleToPhysicalPixels(heightDip, dpiScale);

        // FeedbackRoot's 8-DIP shadow padding supplies the visible Border-to-icon gap.
        var rect = TrayWheelFeedbackPlacement.Calculate(
            iconBounds,
            displayArea.OuterBounds,
            displayArea.WorkArea,
            width,
            height,
            gap: 0);

        FlyoutWindowHelper.MoveAndResizeOnDisplay(this, displayArea, rect);
        if (!_isVisible)
        {
            Show();
        }

        _currentText = text;
        _currentIconBounds = iconBounds;
        _isVisible = true;

        if (textChanged)
        {
            DispatcherQueue.TryEnqueue(() => Announce(text));
        }

        return true;
    }

    /// <summary>
    /// Hides feedback without closing the reusable window.
    /// </summary>
    public void HideFeedback()
    {
        _currentText = null;
        _currentIconBounds = null;
        if (_isVisible)
        {
            _isVisible = false;
            Hide();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_originalWndProc != 0 && IsWindowNative(_hwnd))
        {
            _ = SetWindowLongPtrNative(_hwnd, GwlWndProc, _originalWndProc);
            _originalWndProc = 0;
        }

        _wndProcDelegate = null;
        GC.SuppressFinalize(this);
    }

    private nint WndProc(
        nint hwnd,
        uint message,
        nuint wParam,
        nint lParam)
    {
        if (message == WmNcHitTest)
        {
            return HtTransparent;
        }

        return CallWindowProcNative(
            _originalWndProc,
            hwnd,
            message,
            wParam,
            lParam);
    }

    private void ApplyExtendedStyles()
    {
        var current = GetWindowLongPtrNative(_hwnd, GwlExStyle);
        _ = SetWindowLongPtrNative(
            _hwnd,
            GwlExStyle,
            current | WsExNoActivate | WsExTransparent);
    }

    private void Announce(string text)
    {
        if (_disposed || !_isVisible)
        {
            return;
        }

        var peer = FrameworkElementAutomationPeer.FromElement(FeedbackText) ??
            FrameworkElementAutomationPeer.CreatePeerForElement(FeedbackText);
        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.Other,
            AutomationNotificationProcessing.MostRecent,
            text,
            "PowerDisplayTrayFeedback");
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtrNative(nint hwnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial nint SetWindowLongPtrNative(
        nint hwnd,
        int index,
        nint newValue);

    [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static partial nint CallWindowProcNative(
        nint previous,
        nint hwnd,
        uint message,
        nuint wParam,
        nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "IsWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowNative(nint hwnd);
}
