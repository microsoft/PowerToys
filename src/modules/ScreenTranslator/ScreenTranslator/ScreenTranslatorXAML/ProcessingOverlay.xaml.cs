// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ScreenTranslator.Core.Layout;
using ScreenTranslator.Core.Translation;
using ScreenTranslator.Helpers;
using Windows.Graphics;
using WinUIEx;

namespace ScreenTranslator;

public sealed partial class ProcessingOverlay : TransparentWindow
{
    private readonly ScreenInfo _screenInfo;
    private readonly PhysicalRect _overlayBounds;
    private readonly PhysicalRect _capturedRegion;
    private readonly Action _cancelOperation;
    private readonly IntPtr _hwnd;
    private readonly OSInterop.SubclassProc _subclassProc;
    private bool _subclassed;
    private bool _cancellationRequested;
    private double _cardLeftDip;
    private double _cardTopDip;

    public ProcessingOverlay(
        ScreenInfo screenInfo,
        PhysicalRect capturedRegion,
        Action cancelOperation)
    {
        _screenInfo = screenInfo;
        _overlayBounds = screenInfo.WorkingArea;
        _capturedRegion = capturedRegion;
        _cancelOperation = cancelOperation;

        InitializeComponent();

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        int style = OSInterop.GetWindowLong(_hwnd, OSInterop.GwlStyle);
        _ = OSInterop.SetWindowLong(_hwnd, OSInterop.GwlStyle, style & ~OSInterop.WsCaption & ~OSInterop.WsThickFrame);

        int exStyle = OSInterop.GetWindowLong(_hwnd, OSInterop.GwlExStyle);
        _ = OSInterop.SetWindowLong(_hwnd, OSInterop.GwlExStyle, exStyle | OSInterop.WsExNoActivate | OSInterop.WsExToolWindow | OSInterop.WsExTopMost);

        AppWindow.MoveAndResize(new RectInt32(
            (int)_overlayBounds.X,
            (int)_overlayBounds.Y,
            (int)_overlayBounds.Width,
            (int)_overlayBounds.Height));
        _ = OSInterop.SetWindowPos(
            _hwnd,
            OSInterop.HwndTopMost,
            (int)_overlayBounds.X,
            (int)_overlayBounds.Y,
            (int)_overlayBounds.Width,
            (int)_overlayBounds.Height,
            OSInterop.SwpNoActivate | OSInterop.SwpNoOwnerZOrder | OSInterop.SwpShowWindow);

        try
        {
            this.SetIsShownInSwitchers(false);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"SetIsShownInSwitchers failed: {ex.Message}");
        }

        _subclassProc = HitTestSubclassProc;
        _subclassed = OSInterop.SetWindowSubclass(_hwnd, _subclassProc, UIntPtr.Zero, IntPtr.Zero);

        Closed += ProcessingOverlay_Closed;
        ProcessingCard.Loaded += ProcessingCard_Loaded;
    }

    public void UpdateStatus(string status)
    {
        if (!_cancellationRequested)
        {
            StatusText.Text = status;
        }
    }

    public void RequestCancellation()
    {
        if (_cancellationRequested)
        {
            return;
        }

        _cancellationRequested = true;
        StatusText.Text = "Cancelling...";
        CancelButton.IsEnabled = false;
        _cancelOperation();
    }

    private void ProcessingCard_Loaded(object sender, RoutedEventArgs e)
    {
        PositionCard();
    }

    private void PositionCard()
    {
        var (regionLeftDip, regionTopDip, regionWidthDip, regionHeightDip) = OverlayLayoutHelper.PhysicalToDip(
            _capturedRegion,
            _overlayBounds,
            _screenInfo.DpiScaleX,
            _screenInfo.DpiScaleY);

        double screenWidthDip = _overlayBounds.Width / Math.Max(1.0, _screenInfo.DpiScaleX);
        double screenHeightDip = _overlayBounds.Height / Math.Max(1.0, _screenInfo.DpiScaleY);
        double cardWidth = Math.Max(ProcessingCard.ActualWidth, ProcessingCard.MinWidth);
        double cardHeight = Math.Max(ProcessingCard.ActualHeight, 64);

        _cardLeftDip = Math.Clamp(
            regionLeftDip + ((regionWidthDip - cardWidth) / 2.0),
            16,
            Math.Max(16, screenWidthDip - cardWidth - 16));

        double preferredTop = regionTopDip - cardHeight - 12;
        if (preferredTop < 16)
        {
            preferredTop = regionTopDip + regionHeightDip + 12;
        }

        _cardTopDip = Math.Clamp(
            preferredTop,
            16,
            Math.Max(16, screenHeightDip - cardHeight - 16));

        Canvas.SetLeft(ProcessingCard, _cardLeftDip);
        Canvas.SetTop(ProcessingCard, _cardTopDip);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        RequestCancellation();
    }

    private void ProcessingOverlay_Closed(object sender, WindowEventArgs args)
    {
        if (_subclassed && _hwnd != IntPtr.Zero)
        {
            _ = OSInterop.RemoveWindowSubclass(_hwnd, _subclassProc, UIntPtr.Zero);
            _subclassed = false;
        }
    }

    private IntPtr HitTestSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == OSInterop.WM_NCHITTEST)
        {
            int x = (short)(lParam.ToInt64() & 0xFFFF);
            int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

            return IsPointInsideCard(x, y)
                ? (IntPtr)OSInterop.HTCLIENT
                : (IntPtr)OSInterop.HTTRANSPARENT;
        }

        return OSInterop.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private bool IsPointInsideCard(int screenX, int screenY)
    {
        if (ProcessingCard.ActualWidth <= 0 || ProcessingCard.ActualHeight <= 0)
        {
            return false;
        }

        OSInterop.POINT origin = default;
        if (!OSInterop.ClientToScreen(_hwnd, ref origin))
        {
            return false;
        }

        double left = origin.X + (_cardLeftDip * _screenInfo.DpiScaleX);
        double top = origin.Y + (_cardTopDip * _screenInfo.DpiScaleY);
        double right = left + (ProcessingCard.ActualWidth * _screenInfo.DpiScaleX);
        double bottom = top + (ProcessingCard.ActualHeight * _screenInfo.DpiScaleY);

        return screenX >= left && screenX <= right && screenY >= top && screenY <= bottom;
    }
}
