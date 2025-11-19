// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;
using WinUIEx;

namespace Microsoft.PowerToys.QuickAccess;

public sealed partial class MainWindow : WindowEx
{
    private readonly QuickAccessLaunchContext _launchContext;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly IntPtr _hwnd;
    private readonly AppWindow? _appWindow;
    private EventWaitHandle? _showEvent;
    private EventWaitHandle? _exitEvent;
    private ManualResetEventSlim? _listenerShutdownEvent;
    private Thread? _showListenerThread;
    private Thread? _exitListenerThread;
    private bool _isWindowCloaked;
    private bool _initialActivationHandled;
    private bool _isPrimed;
    private MemoryMappedFile? _positionMap;
    private MemoryMappedViewAccessor? _positionView;
    private PointInt32? _lastRequestedPosition;

    private const int DefaultWidth = 320;
    private const int DefaultHeight = 480;
    private const int DwmWaCloak = 13;
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const int SwHide = 0;
    private const int SwShow = 5;
    private const int SwShowNoActivate = 8;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpNoZorder = 0x0004;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const long WsSysmenu = 0x00080000L;
    private const long WsMinimizeBox = 0x00020000L;
    private const long WsMaximizeBox = 0x00010000L;
    private const long WsExToolWindow = 0x00000080L;
    private const uint MonitorDefaulttonearest = 0x00000002;
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndBottom = new(1);

    public MainWindow(QuickAccessLaunchContext launchContext)
    {
        InitializeComponent();
        _launchContext = launchContext;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _hwnd = WindowNative.GetWindowHandle(this);
        _appWindow = InitializeAppWindow(_hwnd);
        Title = "PowerToys Quick Access (Preview)";

        CustomizeWindowChrome();
        HideFromTaskbar();
        HideWindow();
        InitializeEventListeners();
        Closed += OnClosed;
        Activated += OnActivated;
    }

    private AppWindow? InitializeAppWindow(IntPtr hwnd)
    {
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(windowId);
    }

    private void HideWindow()
    {
        if (_hwnd != IntPtr.Zero)
        {
            var cloaked = CloakWindow();

            if (!ShowWindowNative(_hwnd, SwHide) && _appWindow != null)
            {
                _appWindow.Hide();
            }

            if (cloaked)
            {
                ShowWindowNative(_hwnd, SwShowNoActivate);
            }
            else
            {
                SetWindowPosNative(_hwnd, HwndBottom, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
            }
        }
        else if (_appWindow != null)
        {
            _appWindow.Hide();
        }
    }

    private void InitializeEventListeners()
    {
        InitializePositionMapping();
        if (!string.IsNullOrEmpty(_launchContext.ShowEventName))
        {
            try
            {
                _showEvent = EventWaitHandle.OpenExisting(_launchContext.ShowEventName!);
                EnsureListenerInfrastructure();
                StartShowListenerThread();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
            }
        }

        if (!string.IsNullOrEmpty(_launchContext.ExitEventName))
        {
            try
            {
                _exitEvent = EventWaitHandle.OpenExisting(_launchContext.ExitEventName!);
                EnsureListenerInfrastructure();
                StartExitListenerThread();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
            }
        }
    }

    private void ShowWindow()
    {
        if (_hwnd != IntPtr.Zero)
        {
            UncloakWindow();
            var positionApplied = TryReadRequestedPosition(out var requestedPosition);
            if (!positionApplied && _lastRequestedPosition.HasValue)
            {
                requestedPosition = _lastRequestedPosition.Value;
                positionApplied = true;
            }

            if (positionApplied)
            {
                _lastRequestedPosition = requestedPosition;
            }

            ShowWindowNative(_hwnd, SwShow);

            var flags = SwpNoSize | SwpShowWindow;
            var targetX = 0;
            var targetY = 0;
            if (!positionApplied)
            {
                flags |= SwpNoMove;
            }
            else
            {
                var windowSize = _appWindow?.Size;
                var windowWidth = windowSize?.Width ?? DefaultWidth;
                var windowHeight = windowSize?.Height ?? DefaultHeight;

                targetX = requestedPosition.X - (windowWidth / 2);
                targetY = requestedPosition.Y - windowHeight;

                var monitorHandle = MonitorFromPointNative(new NativePoint { X = requestedPosition.X, Y = requestedPosition.Y }, MonitorDefaulttonearest);
                if (monitorHandle != IntPtr.Zero)
                {
                    var monitorInfo = new MonitorInfo { CbSize = Marshal.SizeOf<MonitorInfo>() };
                    if (GetMonitorInfoNative(monitorHandle, ref monitorInfo))
                    {
                        var minX = monitorInfo.RcWork.Left;
                        var maxX = monitorInfo.RcWork.Right - windowWidth;
                        if (maxX < minX)
                        {
                            maxX = minX;
                        }

                        targetX = Math.Clamp(targetX, minX, maxX);

                        var minY = monitorInfo.RcWork.Top;
                        var maxY = monitorInfo.RcWork.Bottom - windowHeight;
                        if (maxY < minY)
                        {
                            maxY = minY;
                        }

                        targetY = Math.Clamp(targetY, minY, maxY);
                    }
                }
            }

            SetWindowPosNative(_hwnd, HwndTopmost, targetX, targetY, 0, 0, flags);
            BringToForeground(_hwnd);
        }

        Activate();
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            HideWindow();
            return;
        }

        if (_initialActivationHandled)
        {
            return;
        }

        _initialActivationHandled = true;
        PrimeWindow();
        HideWindow();
    }

    private void OnClosed(object sender, WindowEventArgs e)
    {
        StopEventListeners();
        _showEvent?.Dispose();
        _showEvent = null;
        _exitEvent?.Dispose();
        _exitEvent = null;
        DisposePositionResources();
        if (_hwnd != IntPtr.Zero)
        {
            UncloakWindow();
        }
    }

    private void PrimeWindow()
    {
        if (_isPrimed || _hwnd == IntPtr.Zero)
        {
            return;
        }

        _isPrimed = true;

        if (_appWindow != null)
        {
            var currentPosition = _appWindow.Position;
            _appWindow.MoveAndResize(new RectInt32(currentPosition.X, currentPosition.Y, DefaultWidth, DefaultHeight));
        }

        // Warm up the window while cloaked so the first summon does not pay XAML initialization cost.
        var cloaked = CloakWindow();
        if (cloaked)
        {
            ShowWindowNative(_hwnd, SwShowNoActivate);
        }
    }

    private void HideFromTaskbar()
    {
        if (_appWindow == null)
        {
            return;
        }

        _appWindow.IsShownInSwitchers = false;
    }

    private bool CloakWindow()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (_isWindowCloaked)
        {
            return true;
        }

        int cloak = 1;
        var result = DwmSetWindowAttribute(_hwnd, DwmWaCloak, ref cloak, sizeof(int));
        if (result == 0)
        {
            _isWindowCloaked = true;
            SetWindowPosNative(_hwnd, HwndBottom, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
            return true;
        }

        return false;
    }

    private void UncloakWindow()
    {
        if (_hwnd == IntPtr.Zero || !_isWindowCloaked)
        {
            return;
        }

        int cloak = 0;
        var result = DwmSetWindowAttribute(_hwnd, DwmWaCloak, ref cloak, sizeof(int));
        if (result == 0)
        {
            _isWindowCloaked = false;
        }
    }

    [DllImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true)]
    private static extern bool ShowWindowNative(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern nint GetWindowLongPtrNative(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern nint SetWindowLongPtrNative(IntPtr hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
    private static extern bool SetWindowPosNative(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", EntryPoint = "SetForegroundWindow", SetLastError = true)]
    private static extern bool SetForegroundWindowNative(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetForegroundWindow", SetLastError = true)]
    private static extern IntPtr GetForegroundWindowNative();

    [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId", SetLastError = true)]
    private static extern uint GetWindowThreadProcessIdNative(IntPtr hWnd, IntPtr lpdwProcessId);

    [DllImport("user32.dll", EntryPoint = "AttachThreadInput", SetLastError = true)]
    private static extern bool AttachThreadInputNative(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute", SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll", EntryPoint = "MonitorFromPoint", SetLastError = true)]
    private static extern IntPtr MonitorFromPointNative(NativePoint pt, uint dwFlags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    private static extern bool GetMonitorInfoNative(IntPtr hMonitor, ref MonitorInfo lpmi);

    private static void BringToForeground(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        SetForegroundWindowNative(hwnd);

        var foreground = GetForegroundWindowNative();
        if (foreground == hwnd)
        {
            return;
        }

        var windowThread = GetWindowThreadProcessIdNative(hwnd, IntPtr.Zero);
        var foregroundThread = foreground != IntPtr.Zero ? GetWindowThreadProcessIdNative(foreground, IntPtr.Zero) : 0;

        if (windowThread != 0 && foregroundThread != 0 && windowThread != foregroundThread)
        {
            if (AttachThreadInputNative(windowThread, foregroundThread, true))
            {
                SetForegroundWindowNative(hwnd);
                AttachThreadInputNative(windowThread, foregroundThread, false);
            }
        }
        else
        {
            SetForegroundWindowNative(hwnd);
        }
    }

    private void EnsureListenerInfrastructure()
    {
        _listenerShutdownEvent ??= new ManualResetEventSlim(false);
    }

    private void StartShowListenerThread()
    {
        if (_showEvent == null || _listenerShutdownEvent == null || _showListenerThread != null)
        {
            return;
        }

        _showListenerThread = new Thread(ListenForShowEvents)
        {
            IsBackground = true,
            Name = "QuickAccess-ShowEventListener",
        };
        _showListenerThread.Start();
    }

    private void StartExitListenerThread()
    {
        if (_exitEvent == null || _listenerShutdownEvent == null || _exitListenerThread != null)
        {
            return;
        }

        _exitListenerThread = new Thread(ListenForExitEvents)
        {
            IsBackground = true,
            Name = "QuickAccess-ExitEventListener",
        };
        _exitListenerThread.Start();
    }

    private void ListenForShowEvents()
    {
        if (_showEvent == null || _listenerShutdownEvent == null)
        {
            return;
        }

        var handles = new WaitHandle[] { _showEvent, _listenerShutdownEvent.WaitHandle };
        try
        {
            while (true)
            {
                var index = WaitHandle.WaitAny(handles);
                if (index == 0)
                {
                    _dispatcherQueue.TryEnqueue(ShowWindow);
                }
                else
                {
                    break;
                }
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (ThreadInterruptedException)
        {
        }
    }

    private void ListenForExitEvents()
    {
        if (_exitEvent == null || _listenerShutdownEvent == null)
        {
            return;
        }

        var handles = new WaitHandle[] { _exitEvent, _listenerShutdownEvent.WaitHandle };
        try
        {
            while (true)
            {
                var index = WaitHandle.WaitAny(handles);
                if (index == 0)
                {
                    _dispatcherQueue.TryEnqueue(Close);
                    break;
                }

                if (index == 1)
                {
                    break;
                }
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (ThreadInterruptedException)
        {
        }
    }

    private void StopEventListeners()
    {
        if (_listenerShutdownEvent == null)
        {
            return;
        }

        _listenerShutdownEvent.Set();

        JoinListenerThread(ref _showListenerThread);
        JoinListenerThread(ref _exitListenerThread);

        _listenerShutdownEvent.Dispose();
        _listenerShutdownEvent = null;
    }

    private static void JoinListenerThread(ref Thread? thread)
    {
        if (thread == null)
        {
            return;
        }

        try
        {
            if (!thread.Join(TimeSpan.FromMilliseconds(250)))
            {
                thread.Interrupt();
                thread.Join(TimeSpan.FromMilliseconds(250));
            }
        }
        catch (ThreadInterruptedException)
        {
        }
        catch (ThreadStateException)
        {
        }

        thread = null;
    }

    private void InitializePositionMapping()
    {
        if (string.IsNullOrEmpty(_launchContext.PositionMapName) || _positionMap != null)
        {
            return;
        }

        try
        {
            _positionMap = MemoryMappedFile.OpenExisting(_launchContext.PositionMapName!, MemoryMappedFileRights.Read);
            _positionView = _positionMap.CreateViewAccessor(0, sizeof(int) * 3, MemoryMappedFileAccess.Read);
        }
        catch (FileNotFoundException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private bool TryReadRequestedPosition(out PointInt32 position)
    {
        position = default;
        if (_positionView == null)
        {
            return false;
        }

        const int xOffset = 0;
        const int yOffset = sizeof(int);
        const int sequenceOffset = sizeof(int) * 2;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            int startSequence;
            try
            {
                startSequence = _positionView.ReadInt32(sequenceOffset);
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            if ((startSequence & 1) != 0)
            {
                Thread.Yield();
                continue;
            }

            int x;
            int y;
            try
            {
                x = _positionView.ReadInt32(xOffset);
                y = _positionView.ReadInt32(yOffset);
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            int endSequence;
            try
            {
                endSequence = _positionView.ReadInt32(sequenceOffset);
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            if (startSequence == endSequence)
            {
                position = new PointInt32(x, y);
                return true;
            }

            Thread.Yield();
        }

        return false;
    }

    private void DisposePositionResources()
    {
        try
        {
            _positionView?.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }

        _positionView = null;

        try
        {
            _positionMap?.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }

        _positionMap = null;
        _lastRequestedPosition = null;
    }

    private void CustomizeWindowChrome()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var windowAttributesChanged = false;

        var stylePtr = GetWindowLongPtrNative(_hwnd, GwlStyle);
        var styleError = Marshal.GetLastWin32Error();
        if (!(stylePtr == nint.Zero && styleError != 0))
        {
            var styleValue = (long)stylePtr;
            var newStyleValue = styleValue & ~(WsSysmenu | WsMinimizeBox | WsMaximizeBox);

            if (newStyleValue != styleValue)
            {
                SetWindowLongPtrNative(_hwnd, GwlStyle, (nint)newStyleValue);
                windowAttributesChanged = true;
            }
        }

        var exStylePtr = GetWindowLongPtrNative(_hwnd, GwlExStyle);
        var exStyleError = Marshal.GetLastWin32Error();
        if (!(exStylePtr == nint.Zero && exStyleError != 0))
        {
            var exStyleValue = (long)exStylePtr;
            var newExStyleValue = exStyleValue | WsExToolWindow;
            if (newExStyleValue != exStyleValue)
            {
                SetWindowLongPtrNative(_hwnd, GwlExStyle, (nint)newExStyleValue);
                windowAttributesChanged = true;
            }
        }

        if (windowAttributesChanged)
        {
            // Apply the new chrome immediately so caption buttons disappear right away and the tool-window flag takes effect.
            SetWindowPosNative(_hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoZorder | SwpNoActivate | SwpFrameChanged);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int CbSize;
        public Rect RcMonitor;
        public Rect RcWork;
        public uint DwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
