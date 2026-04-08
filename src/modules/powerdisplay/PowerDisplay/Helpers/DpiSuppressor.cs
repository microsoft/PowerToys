// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using WinUIEx;

namespace PowerDisplay.Helpers
{
    /// <summary>
    /// Subclasses a window's WndProc to suppress WM_DPICHANGED messages during
    /// cross-monitor MoveAndResize calls. Without suppression, the framework
    /// auto-scales the window a second time, causing double-scaling artifacts.
    ///
    /// Usage:
    ///   var suppressor = new DpiSuppressor(window);
    ///   using (suppressor.Suppress())
    ///   {
    ///       window.AppWindow.MoveAndResize(rect, displayArea);
    ///   }
    /// </summary>
    internal sealed partial class DpiSuppressor : IDisposable
    {
        // Optional external WndProc handler (e.g., HotkeyService) called before default processing.
        // Return true to indicate the message was handled.
        private readonly Func<uint, nuint, nint, bool>? _preProcessor;

        private const int GwlWndProc = -4;
        private const uint WmDpiChanged = 0x02E0;

        private readonly nint _hwnd;
        private nint _originalWndProc;
        private WndProcDelegate? _wndProcDelegate;
        private bool _suppressing;
        private bool _disposed;

        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

        [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW")]
        private static partial nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint msg, nuint wParam, nint lParam);

        /// <summary>
        /// Initializes a new instance of the <see cref="DpiSuppressor"/> class.
        /// Subclass the window's WndProc to enable DPI suppression.
        /// </summary>
        /// <param name="window">Window to subclass.</param>
        /// <param name="preProcessor">Optional callback invoked for every message before default processing.
        /// Receives (uMsg, wParam, lParam). Return true to swallow the message.</param>
        public DpiSuppressor(WindowEx window, Func<uint, nuint, nint, bool>? preProcessor = null)
        {
            _hwnd = window.GetWindowHandle();
            _preProcessor = preProcessor;
            _wndProcDelegate = WndProc;
            var ptr = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
            _originalWndProc = SetWindowLongPtr(_hwnd, GwlWndProc, ptr);
        }

        /// <summary>
        /// Returns a disposable scope during which WM_DPICHANGED is suppressed.
        /// </summary>
        public SuppressScope Suppress() => new(this);

        private nint WndProc(nint hwnd, uint uMsg, nuint wParam, nint lParam)
        {
            // Let external handler process first (e.g., hotkey messages)
            if (_preProcessor?.Invoke(uMsg, wParam, lParam) == true)
            {
                return 0;
            }

            if (uMsg == WmDpiChanged && _suppressing)
            {
                return 0;
            }

            return CallWindowProc(_originalWndProc, hwnd, uMsg, wParam, lParam);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Restore original WndProc
            if (_originalWndProc != 0)
            {
                SetWindowLongPtr(_hwnd, GwlWndProc, _originalWndProc);
                _originalWndProc = 0;
            }

            _wndProcDelegate = null;
        }

        internal readonly struct SuppressScope : IDisposable
        {
            private readonly DpiSuppressor _owner;

            internal SuppressScope(DpiSuppressor owner)
            {
                _owner = owner;
                _owner._suppressing = true;
            }

            public void Dispose()
            {
                _owner._suppressing = false;
            }
        }
    }
}
