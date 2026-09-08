// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

using ManagedCommon;
using PowerOCR.Core.Geometry;
using PowerOCR.Core.Models;
using PowerOCR.Models;

using Native = PowerOCR.Helpers.NativeSelectionInterop;

namespace PowerOCR.Helpers;

/// <summary>
/// Native GDI selection surface with no WinUI content island. The native thread owns
/// its HWND, capture, cursor clip, and drawing resources; callbacks only notify the UI thread.
/// </summary>
internal sealed partial class NativeSelectionWindow : IDisposable
{
    private readonly object _lifecycleGate = new();
    private readonly ConcurrentQueue<Action> _commands = new();
    private readonly DisplayBounds _bounds;
    private readonly Bitmap _screenshot;
    private readonly Action<PixelSelection, bool> _completed;
    private readonly Action _cancelled;
    private readonly Action<int, bool> _keyPressed;
    private readonly Action<Windows.Foundation.Point> _contextRequested;
    private readonly Action<string> _failed;
    private readonly Native.WindowProcedure _windowProcedure;
    private nint _hwnd;
    private int _disposeRequested;
    private bool _started;

    // Accessed only on the native window's thread after Start.
    private PixelRect[] _exclusions = [];
    private IDisposable? _cursorClip;
    private DrawingSurface? _original;
    private DrawingSurface? _dimmed;
    private DrawingSurface? _frame;
    private nint _crossCursor;
    private bool _ready;
    private bool _visible = true;
    private bool _selecting;
    private bool _closing;
    private bool _failureReported;
    private bool _frameDirty;
    private bool _closeRequested;
    private double _anchorX;
    private double _anchorY;
    private double _selectionX;
    private double _selectionY;
    private double _selectionWidth;
    private double _selectionHeight;
    private double _lastX;
    private double _lastY;

    internal NativeSelectionWindow(
        DisplayCapture capture,
        Action<PixelSelection, bool> completed,
        Action cancelled,
        Action<int, bool> keyPressed,
        Action<Windows.Foundation.Point> contextRequested,
        Action<string> failed)
    {
        _bounds = capture.Bounds;

        // The overlay manager may dispose DisplayCapture immediately after it requests close.
        // Never read its bitmap from the native thread after returning from this constructor.
        // GDI screen copies do not define alpha; preserve their RGB and make the clone opaque
        // before GDI+ compositing or GetHbitmap can interpret a zero alpha as transparent.
        _screenshot = capture.Bitmap.Clone(
            new Rectangle(0, 0, capture.Bitmap.Width, capture.Bitmap.Height),
            PixelFormat.Format32bppRgb);
        _completed = completed;
        _cancelled = cancelled;
        _keyPressed = keyPressed;
        _contextRequested = contextRequested;
        _failed = failed;
        _windowProcedure = WindowProcedure;
    }

    private nint Hwnd => Interlocked.CompareExchange(ref _hwnd, 0, 0);

    internal void Start()
    {
        lock (_lifecycleGate)
        {
            if (_started || Volatile.Read(ref _disposeRequested) != 0)
            {
                return;
            }

            var thread = new Thread(ThreadMain)
            {
                IsBackground = true,
                Name = "PowerOCR native selection",
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            _started = true;
        }
    }

    internal void SetExclusions(PixelRect[] localPixelRects)
    {
        var snapshot = (PixelRect[])localPixelRects.Clone();
        Enqueue(() =>
        {
            _exclusions = snapshot;
            ApplyExclusions();
        });
    }

    internal void SetVisible(bool visible)
    {
        Enqueue(() =>
        {
            _visible = visible;
            if (!visible)
            {
                EndSelection(notifyCancellation: true);
            }

            UpdateVisibility();
        });
    }

    internal void CancelSelection() => Enqueue(() => EndSelection(notifyCancellation: true));

    internal void BringToFront() => Enqueue(RestoreZOrder);

    public void Dispose()
    {
        lock (_lifecycleGate)
        {
            if (Interlocked.Exchange(ref _disposeRequested, 1) != 0)
            {
                return;
            }

            if (!_started)
            {
                _screenshot.Dispose();
                return;
            }
        }

        // Do not join: window activation/destruction can exchange synchronous messages with
        // other UI threads. The native message loop must be allowed to finish independently.
        Enqueue(CloseWindow, allowAfterDispose: true);
    }

    private void Enqueue(Action command, bool allowAfterDispose = false)
    {
        if (!allowAfterDispose && Volatile.Read(ref _disposeRequested) != 0)
        {
            return;
        }

        // Publish the command before reading the HWND. ThreadMain publishes the HWND before
        // draining this queue, so commands issued during startup cannot miss both paths.
        _commands.Enqueue(command);
        nint hwnd = Hwnd;
        if (hwnd != 0)
        {
            _ = Native.PostMessage(hwnd, Native.CommandMessage, 0, 0);
        }
    }

    private void ThreadMain()
    {
        nint className = 0;
        nint instance = 0;
        bool classRegistered = false;
        nint previousDpiContext = 0;
        try
        {
            if (Volatile.Read(ref _disposeRequested) != 0)
            {
                return;
            }

            // Win32 geometry and the screenshot are both physical pixels on every monitor.
            previousDpiContext = Native.SetThreadDpiAwarenessContext(-4); // PER_MONITOR_AWARE_V2
            if (previousDpiContext == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not set native selection DPI awareness.");
            }

            PrepareDrawing();
            _crossCursor = Native.LoadCursor(0, 32515); // IDC_CROSS, shared system cursor.
            if (_crossCursor == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not load the selection cursor.");
            }

            instance = Native.GetModuleHandle(null);
            className = Marshal.StringToHGlobalUni($"PowerOCR.NativeSelection.{Guid.NewGuid():N}");
            var windowClass = new Native.WindowClass
            {
                Size = (uint)Marshal.SizeOf<Native.WindowClass>(),
                Procedure = Marshal.GetFunctionPointerForDelegate(_windowProcedure),
                Instance = instance,
                Cursor = _crossCursor,
                ClassName = className,

                // No background brush: every visible pixel comes from the prepared frame.
                BackgroundBrush = 0,
            };
            if (Native.RegisterClassEx(in windowClass) == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the selection window class.");
            }

            classRegistered = true;
            if (Volatile.Read(ref _disposeRequested) != 0)
            {
                return;
            }

            // Cross-thread ownership implicitly attaches input queues. Keep this HWND
            // independent, as Screen Ruler does, and coordinate lifecycle from WinUI.
            nint hwnd = Native.CreateWindowEx(
                0x00000088, // WS_EX_TOOLWINDOW | WS_EX_TOPMOST
                className,
                "Text Extractor selection",
                0x80000000, // WS_POPUP; deliberately not visible yet.
                _bounds.X,
                _bounds.Y,
                _bounds.Width,
                _bounds.Height,
                0,
                0,
                instance,
                0);
            if (hwnd == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the native selection window.");
            }

            Interlocked.Exchange(ref _hwnd, hwnd);
            DrainCommands();
            if (Volatile.Read(ref _disposeRequested) != 0)
            {
                CloseWindow();
                return;
            }

            ApplyExclusions();
            _ready = true;
            UpdateVisibility();

            int result;
            while ((result = Native.GetMessage(out Native.Message message, 0, 0, 0)) > 0)
            {
                _ = Native.TranslateMessage(in message);
                _ = Native.DispatchMessage(in message);
            }

            if (result < 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "The native selection message loop failed.");
            }
        }
        catch (Exception ex)
        {
            if (Volatile.Read(ref _disposeRequested) == 0)
            {
                ReportFailure(ex);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _disposeRequested, 1);
            _ready = false;
            CloseWindow();
            ReleaseCursorClip();
            _frame?.Dispose();
            _dimmed?.Dispose();
            _original?.Dispose();
            _screenshot.Dispose();
            if (classRegistered)
            {
                _ = Native.UnregisterClass(className, instance);
            }

            if (className != 0)
            {
                Marshal.FreeHGlobal(className);
            }

            if (previousDpiContext != 0)
            {
                _ = Native.SetThreadDpiAwarenessContext(previousDpiContext);
            }

            while (_commands.TryDequeue(out _))
            {
            }
        }
    }

    private void PrepareDrawing()
    {
        _original = new DrawingSurface(_screenshot);
        using var dimmed = _screenshot.Clone(
            new Rectangle(0, 0, _screenshot.Width, _screenshot.Height),
            PixelFormat.Format32bppRgb);
        using (Graphics graphics = Graphics.FromImage(dimmed))
        using (var shade = new SolidBrush(Color.FromArgb(102, Color.Black)))
        {
            graphics.FillRectangle(shade, 0, 0, dimmed.Width, dimmed.Height);
        }

        _dimmed = new DrawingSurface(dimmed);
        _frame = new DrawingSurface(dimmed);
    }

    private nint WindowProcedure(nint hwnd, uint message, nuint wParam, nint lParam)
    {
        try
        {
            switch (message)
            {
                case Native.CommandMessage:
                    DrainCommands();
                    return 0;
                case 0x0020: // WM_SETCURSOR: select a real system cursor, without an input island.
                    _ = Native.SetCursor(_crossCursor);
                    return 1;
                case 0x0014: // WM_ERASEBKGND: the prepared opaque frame paints the entire area.
                    return 1;
                case 0x000F: // WM_PAINT
                    PaintWindow(hwnd);
                    return 0;
                case 0x0201: // WM_LBUTTONDOWN
                    BeginSelection(hwnd, lParam);
                    return 0;
                case 0x0200: // WM_MOUSEMOVE
                    if (_selecting)
                    {
                        ApplyPointerPosition(lParam, (wParam & 0x0004) != 0); // MK_SHIFT
                        RedrawFrame();
                    }

                    return 0;
                case 0x0202: // WM_LBUTTONUP
                    CompleteSelection(lParam, (wParam & 0x0004) != 0); // MK_SHIFT
                    return 0;
                case 0x0215: // WM_CAPTURECHANGED
                    if (lParam != hwnd)
                    {
                        EndSelection(notifyCancellation: true);
                    }

                    return 0;
                case 0x001F: // WM_CANCELMODE
                case 0x0008: // WM_KILLFOCUS
                    EndSelection(notifyCancellation: true);
                    return 0;
                case 0x0100: // WM_KEYDOWN
                    Notify(() => _keyPressed((int)wParam, ShiftDown()));
                    return 0;
                case 0x0104: // WM_SYSKEYDOWN
                    // VK_F10; preserve DefWindowProc handling of Alt+F4.
                    if (wParam == 0x79)
                    {
                        Notify(() => _keyPressed((int)wParam, ShiftDown()));
                        return 0;
                    }

                    break;
                case 0x0205: // WM_RBUTTONUP
                    EndSelection(notifyCancellation: true);
                    var position = PositionFromMessage(lParam);
                    Notify(() => _contextRequested(new Windows.Foundation.Point(position.X, position.Y)));
                    return 0;
                case 0x0010: // WM_CLOSE
                    if (Volatile.Read(ref _disposeRequested) == 0 && !_failureReported)
                    {
                        // Keep the screenshot in place until the UI thread has hidden all
                        // WinUI hosts. The manager then disposes this native surface.
                        if (!_closeRequested)
                        {
                            _closeRequested = true;
                            Notify(() => _keyPressed(0x1B, false)); // VK_ESCAPE
                        }

                        return 0;
                    }

                    CloseWindow();
                    return 0;
                case 0x0002: // WM_DESTROY
                    _closing = true;
                    EndSelection(notifyCancellation: true);
                    Native.PostQuitMessage(0);
                    return 0;
                case 0x0082: // WM_NCDESTROY
                    _ = Interlocked.CompareExchange(ref _hwnd, 0, hwnd);
                    break;
            }

            return Native.DefWindowProc(hwnd, message, wParam, lParam);
        }
        catch (Exception ex)
        {
            // Exceptions must never cross the unmanaged window-procedure boundary.
            ReportFailure(ex);
            ReleaseCursorClip();
            _selecting = false;
            _ = Native.PostMessage(hwnd, 0x0010, 0, 0); // WM_CLOSE
            return 0;
        }
    }

    private void BeginSelection(nint hwnd, nint lParam)
    {
        if (_selecting || _closeRequested || Volatile.Read(ref _disposeRequested) != 0)
        {
            return;
        }

        _ = Native.SetFocus(hwnd);
        _ = Native.SetCapture(hwnd);
        if (Native.GetCapture() != hwnd)
        {
            return;
        }

        var position = PositionFromMessage(lParam);
        _anchorX = _lastX = Math.Clamp(position.X, 0, _bounds.Width);
        _anchorY = _lastY = Math.Clamp(position.Y, 0, _bounds.Height);
        _selectionX = _anchorX;
        _selectionY = _anchorY;
        _selectionWidth = _selectionHeight = 0;
        _selecting = true;
        _cursorClip = CursorClipper.TryAcquire(_bounds);
        ApplyExclusions();
        RedrawFrame();
    }

    private void ApplyPointerPosition(nint lParam, bool shiftDown)
    {
        var position = PositionFromMessage(lParam);
        double currentX = position.X;
        double currentY = position.Y;
        if (shiftDown)
        {
            double nextX = Math.Clamp(_selectionX + currentX - _lastX, 0, Math.Max(0, _bounds.Width - _selectionWidth));
            double nextY = Math.Clamp(_selectionY + currentY - _lastY, 0, Math.Max(0, _bounds.Height - _selectionHeight));
            _anchorX += nextX - _selectionX;
            _anchorY += nextY - _selectionY;
            _selectionX = nextX;
            _selectionY = nextY;
        }
        else
        {
            _selectionX = Math.Clamp(Math.Min(_anchorX, currentX), 0, _bounds.Width);
            _selectionY = Math.Clamp(Math.Min(_anchorY, currentY), 0, _bounds.Height);
            _selectionWidth = Math.Clamp(Math.Max(_anchorX, currentX) - _selectionX, 0, _bounds.Width - _selectionX);
            _selectionHeight = Math.Clamp(Math.Max(_anchorY, currentY) - _selectionY, 0, _bounds.Height - _selectionY);
        }

        _lastX = currentX;
        _lastY = currentY;
    }

    private PixelSelection CurrentSelection() => SelectionGeometry.ToPixels(
        new OcrPoint(_selectionX, _selectionY),
        new OcrPoint(_selectionX + _selectionWidth, _selectionY + _selectionHeight),
        1,
        _bounds);

    private void CompleteSelection(nint lParam, bool shiftDown)
    {
        if (!_selecting)
        {
            return;
        }

        ApplyPointerPosition(lParam, shiftDown);
        PixelSelection selection = CurrentSelection();
        bool isClick = selection.Local.Width < 3 || selection.Local.Height < 3;
        EndSelection(notifyCancellation: false);
        Notify(() => _completed(selection, isClick));
    }

    private void EndSelection(bool notifyCancellation)
    {
        bool wasSelecting = _selecting;
        _selecting = false;
        if (Native.GetCapture() == Hwnd && Hwnd != 0)
        {
            _ = Native.ReleaseCapture();
        }

        ReleaseCursorClip();
        if (wasSelecting)
        {
            _selectionWidth = _selectionHeight = 0;
            if (!_closing)
            {
                ApplyExclusions();
                RedrawFrame();
            }

            if (notifyCancellation)
            {
                Notify(_cancelled);
            }
        }
    }

    private void ReleaseCursorClip()
    {
        IDisposable? clip = _cursorClip;
        _cursorClip = null;
        try
        {
            clip?.Dispose();
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
        }
    }

    private void ApplyExclusions()
    {
        nint hwnd = Hwnd;
        if (hwnd == 0)
        {
            return;
        }

        nint region = Native.CreateRectRgn(0, 0, _bounds.Width, _bounds.Height);
        if (region == 0)
        {
            throw new Win32Exception("Could not create the selection window region.");
        }

        try
        {
            if (!_selecting)
            {
                foreach (PixelRect exclusion in _exclusions)
                {
                    int left = (int)Math.Clamp((long)exclusion.X, 0, _bounds.Width);
                    int top = (int)Math.Clamp((long)exclusion.Y, 0, _bounds.Height);
                    int right = (int)Math.Clamp((long)exclusion.X + exclusion.Width, left, _bounds.Width);
                    int bottom = (int)Math.Clamp((long)exclusion.Y + exclusion.Height, top, _bounds.Height);
                    nint hole = Native.CreateRectRgn(left, top, right, bottom);
                    if (hole == 0)
                    {
                        throw new Win32Exception("Could not create a toolbar exclusion region.");
                    }

                    try
                    {
                        // RGN_DIFF
                        if (Native.CombineRgn(region, region, hole, 4) == 0)
                        {
                            throw new Win32Exception("Could not subtract the toolbar exclusion region.");
                        }
                    }
                    finally
                    {
                        _ = Native.DeleteObject(hole);
                    }
                }
            }

            if (Native.SetWindowRgn(hwnd, region, 1) == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not apply the selection window region.");
            }

            // On success the window manager owns this HRGN.
            region = 0;
        }
        finally
        {
            if (region != 0)
            {
                _ = Native.DeleteObject(region);
            }
        }
    }

    private void RedrawFrame()
    {
        _frameDirty = true;
        nint hwnd = Hwnd;
        if (hwnd != 0)
        {
            // Let WM_PAINT coalesce updates instead of copying a full screenshot for every mouse message.
            _ = Native.InvalidateRect(hwnd, 0, 0);
        }
    }

    private void ComposeFrame()
    {
        if (!_frameDirty || _frame is null || _dimmed is null || _original is null)
        {
            return;
        }

        _ = Native.BitBlt(_frame.Dc, 0, 0, _bounds.Width, _bounds.Height, _dimmed.Dc, 0, 0, Native.CopyPixels);
        if (_selecting)
        {
            PixelRect selection = CurrentSelection().Local;
            if (selection.Width > 0 && selection.Height > 0)
            {
                _ = Native.BitBlt(_frame.Dc, selection.X, selection.Y, selection.Width, selection.Height, _original.Dc, selection.X, selection.Y, Native.CopyPixels);
            }

            var border = new Native.Rect
            {
                Left = selection.X - 1,
                Top = selection.Y - 1,
                Right = selection.X + selection.Width + 1,
                Bottom = selection.Y + selection.Height + 1,
            };
            _ = Native.FrameRect(_frame.Dc, in border, Native.GetStockObject(0)); // WHITE_BRUSH
        }

        _frameDirty = false;
    }

    private void PaintWindow(nint hwnd)
    {
        nint target = Native.BeginPaint(hwnd, out Native.Paint paint);
        try
        {
            ComposeFrame();
            if (_frame is not null && target != 0)
            {
                Native.Rect area = paint.Rect;
                _ = Native.BitBlt(target, area.Left, area.Top, area.Right - area.Left, area.Bottom - area.Top, _frame.Dc, area.Left, area.Top, Native.CopyPixels);
            }
        }
        finally
        {
            _ = Native.EndPaint(hwnd, in paint);
        }
    }

    private void UpdateVisibility()
    {
        nint hwnd = Hwnd;
        if (_ready && hwnd != 0)
        {
            _ = Native.ShowWindow(hwnd, _visible ? 4 : 0); // SW_SHOWNOACTIVATE / SW_HIDE
            if (_visible)
            {
                RestoreZOrder();
                _ = Native.UpdateWindow(hwnd);
            }
        }
    }

    private void RestoreZOrder()
    {
        nint hwnd = Hwnd;
        if (_ready && _visible && hwnd != 0 && Volatile.Read(ref _disposeRequested) == 0)
        {
            // A click on the WinUI toolbar raises its full-screen window. Keep the native
            // surface above it, with region holes for controls, without changing focus.
            _ = Native.SetWindowPos(hwnd, -1, 0, 0, 0, 0, 0x0013); // HWND_TOPMOST, NOSIZE | NOMOVE | NOACTIVATE
        }
    }

    private void DrainCommands()
    {
        while (_commands.TryDequeue(out Action? command))
        {
            command();
        }
    }

    private void CloseWindow()
    {
        _closing = true;
        _ready = false;
        _visible = false;
        nint hwnd = Hwnd;
        if (hwnd != 0)
        {
            _ = Native.ShowWindow(hwnd, 0); // SW_HIDE before destroying the HWND or drawing resources.
        }

        EndSelection(notifyCancellation: true);
        if (hwnd != 0)
        {
            _ = Native.DestroyWindow(hwnd);
        }
    }

    private void ReportFailure(Exception exception)
    {
        if (_failureReported)
        {
            return;
        }

        _failureReported = true;
        LogError("PowerOCR native selection failed.", exception);
        Notify(() => _failed(exception.Message));
    }

    private static void Notify(Action callback)
    {
        try
        {
            callback();
        }
        catch (Exception ex)
        {
            LogError("PowerOCR native selection callback failed.", ex);
        }
    }

    private static void LogError(string message, Exception exception)
    {
        try
        {
            Logger.LogError(message, exception);
        }
        catch (Exception)
        {
            // No logging failure may escape an unmanaged callback or a thread's finally.
        }
    }

    private static bool ShiftDown() => Native.GetKeyState(0x10) < 0;

    private static Native.Point PositionFromMessage(nint lParam) => new()
    {
        X = unchecked((short)(long)lParam),
        Y = unchecked((short)((long)lParam >> 16)),
    };

    private sealed partial class DrawingSurface : IDisposable
    {
        private nint _bitmap;
        private nint _previousBitmap;

        internal DrawingSurface(Bitmap bitmap)
        {
            try
            {
                _bitmap = bitmap.GetHbitmap(Color.Black);
                Dc = Native.CreateCompatibleDC(0);
                if (_bitmap == 0 || Dc == 0)
                {
                    throw new Win32Exception("Could not allocate a selection drawing surface.");
                }

                _previousBitmap = Native.SelectObject(Dc, _bitmap);
                if (_previousBitmap == 0 || _previousBitmap == -1)
                {
                    throw new Win32Exception("Could not select the captured image into the drawing surface.");
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal nint Dc { get; private set; }

        public void Dispose()
        {
            if (Dc != 0)
            {
                if (_previousBitmap != 0 && _previousBitmap != -1)
                {
                    _ = Native.SelectObject(Dc, _previousBitmap);
                }

                _ = Native.DeleteDC(Dc);
                Dc = 0;
            }

            if (_bitmap != 0)
            {
                _ = Native.DeleteObject(_bitmap);
                _bitmap = 0;
            }
        }
    }
}
