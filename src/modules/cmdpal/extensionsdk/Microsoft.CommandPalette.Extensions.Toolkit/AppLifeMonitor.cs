// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Monitors OS-initiated application lifecycle events (such as system shutdown or session end)
/// and signals an event so the extension process can exit gracefully.
/// </summary>
/// <remarks>
/// Extensions run as COM out-of-process servers with <c>[MTAThread]</c>, which means the main
/// thread has no Win32 message loop. Without a message loop, the process cannot receive
/// <c>WM_QUERYENDSESSION</c> or <c>WM_ENDSESSION</c> messages when the OS shuts down, causing
/// hang reports (MOAPPLICATION_HANG / HANG_QUIESCE) and delayed Store updates.
///
/// This class creates a dedicated hidden window on a background STA thread whose message loop
/// handles those messages and signals <paramref name="extensionDisposedEvent"/> so the
/// extension exits promptly.
///
/// <b>Important:</b> The window must <em>not</em> use the <c>HWND_MESSAGE</c> parent because
/// message-only windows are excluded from the OS broadcast of <c>WM_QUERYENDSESSION</c>.
/// </remarks>
public sealed class AppLifeMonitor : IDisposable
{
    // Win32 window message constants
    private const uint WM_CLOSE = 0x0010;
    private const uint WM_DESTROY = 0x0002;
    private const uint WM_QUERYENDSESSION = 0x0011;
    private const uint WM_ENDSESSION = 0x0016;

    // Invisible zero-size pop-up window style. Must NOT use HWND_MESSAGE parent:
    // message-only windows are excluded from OS shutdown broadcasts.
    private const uint WS_POPUP = 0x80000000;

    private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASSW
    {
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public nint lpszMenuName;
        public nint lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern ushort RegisterClassW(ref WNDCLASSW lpWndClass);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CreateWindowExW(
        uint dwExStyle,
        nint lpClassName,
        nint lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        nint hWndParent,
        nint hMenu,
        nint hInstance,
        nint lpParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessageW(ref MSG lpmsg);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterClassW(nint lpClassName, nint hInstance);

    [DllImport("kernel32.dll")]
    private static extern nint GetModuleHandleW(nint lpModuleName);

    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly ManualResetEvent _windowCreated = new(false);

    // Keep the delegate alive for the lifetime of the window to prevent GC collection.
    // The GC may collect the delegate if it is only stored as a function pointer.
    private WndProcDelegate? _wndProcDelegate;

    private nint _hwnd;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppLifeMonitor"/> class and starts
    /// the background message loop immediately.
    /// </summary>
    /// <param name="extensionDisposedEvent">
    /// The event to signal when the OS requests the session to end (e.g. system shutdown,
    /// user logoff, or Store-initiated update). Signalling this event causes the
    /// <c>extensionDisposedEvent.WaitOne()</c> call in <c>Program.Main</c> to unblock so
    /// the COM server can stop gracefully before the OS deadline expires.
    /// </param>
    public AppLifeMonitor(ManualResetEvent extensionDisposedEvent)
    {
        _extensionDisposedEvent = extensionDisposedEvent ?? throw new ArgumentNullException(nameof(extensionDisposedEvent));

        var thread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "AppLifeMonitor",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        // Block until the window is ready (or has failed to initialize) before returning.
        _windowCreated.WaitOne();
    }

    private void RunMessageLoop()
    {
        // Use a process-specific class name to avoid collisions if the same extension
        // is loaded multiple times in the same session.
        var className = $"AppLifeMonitor_{Environment.ProcessId}";
        var classNamePtr = Marshal.StringToHGlobalUni(className);

        try
        {
            // Store the delegate in a field so it is not collected by the GC.
            _wndProcDelegate = HandleMessage;
            var wndProcPtr = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);

            var hInstance = GetModuleHandleW(0);

            var wndClass = new WNDCLASSW
            {
                lpfnWndProc = wndProcPtr,
                hInstance = hInstance,
                lpszClassName = classNamePtr,
            };

            var atom = RegisterClassW(ref wndClass);
            if (atom == 0)
            {
                // Window class registration failed. The monitor is non-operational, but the
                // extension continues to run normally — just without graceful shutdown support.
                return;
            }

            // Create a 0×0 invisible pop-up window.
            // This must NOT be a message-only window (HWND_MESSAGE parent) because
            // message-only windows are excluded from OS shutdown broadcasts.
            _hwnd = CreateWindowExW(
                dwExStyle: 0,
                lpClassName: classNamePtr,
                lpWindowName: 0,
                dwStyle: WS_POPUP,
                x: 0,
                y: 0,
                nWidth: 0,
                nHeight: 0,
                hWndParent: 0,
                hMenu: 0,
                hInstance: hInstance,
                lpParam: 0);

            if (_hwnd == 0)
            {
                // Window creation failed. The monitor is non-operational, but the extension
                // continues to run normally — just without graceful shutdown support.
                UnregisterClassW(classNamePtr, hInstance);
                return;
            }

            // Signal that initialization succeeded before entering the message loop.
            // The constructor's WaitOne() unblocks here.
            _windowCreated.Set();

            // Run the message loop until PostQuitMessage is called (WM_QUIT).
            while (GetMessageW(out var msg, nint.Zero, 0, 0))
            {
                TranslateMessage(ref msg);
                DispatchMessageW(ref msg);
            }

            UnregisterClassW(classNamePtr, hInstance);
        }
        finally
        {
            // Always signal and dispose the event so the constructor never hangs, even on
            // failure paths. Set() is idempotent, so calling it here after an early success
            // Set() above is safe.
            _windowCreated.Set();
            _windowCreated.Dispose();
            Marshal.FreeHGlobal(classNamePtr);
            _wndProcDelegate = null;
        }
    }

    private nint HandleMessage(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case WM_QUERYENDSESSION:
                // Return non-zero to permit the session to end.
                return 1;

            case WM_ENDSESSION:
                // wParam is non-zero when the session is actually ending.
                if (wParam != 0)
                {
                    _extensionDisposedEvent.Set();
                }

                return 0;

            case WM_CLOSE:
                DestroyWindow(hWnd);
                return 0;

            case WM_DESTROY:
                PostQuitMessage(0);
                return 0;

            default:
                return DefWindowProcW(hWnd, msg, wParam, lParam);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        var hwnd = Interlocked.Exchange(ref _hwnd, 0);
        if (hwnd != 0)
        {
            // Post WM_CLOSE to our window on the message thread.
            // The WndProc will call DestroyWindow → WM_DESTROY → PostQuitMessage,
            // which unblocks GetMessageW and lets the background thread exit cleanly.
            // The background thread's finally block will then signal and dispose _windowCreated.
            PostMessageW(hwnd, WM_CLOSE, 0, 0);
        }
    }
}
