// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.UI.Xaml;

namespace PowerDisplay.Helpers
{
    /// <summary>
    /// System tray icon helper class
    /// </summary>
    public class TrayIconHelper : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public uint CbSize;
            public IntPtr HWnd;
            public uint UID;
            public uint UFlags;
            public uint UCallbackMessage;
            public IntPtr HIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string SzTip;
            public uint DwState;
            public uint DwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string SzInfo;
            public uint UTimeout;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string SzInfoTitle;
            public uint DwInfoFlags;
        }

        private const uint NifMessage = 0x00000001;
        private const uint NifIcon = 0x00000002;
        private const uint NifTip = 0x00000004;
        private const uint NifInfo = 0x00000010;

        private const uint NimAdd = 0x00000000;
        private const uint NimModify = 0x00000001;
        private const uint NimDelete = 0x00000002;

        private const uint WmUser = 0x0400;
        private const uint WmTrayicon = WmUser + 1;
        private const uint WmLbuttonup = 0x0202;
        private const uint WmRbuttonup = 0x0205;
        private const uint WmCommand = 0x0111;

        private uint _wmTaskbarCreated; // TaskbarCreated message ID

        // Menu item IDs
        private const int IdShow = 1001;
        private const int IdExit = 1002;
        private const int IdRefresh = 1003;
        private const int IdSettings = 1004;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);



        [DllImport("user32.dll")]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern int TrackPopupMenu(
            IntPtr hMenu,
            uint uFlags,
            int x,
            int y,
            int nReserved,
            IntPtr hWnd,
            IntPtr prcRect);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }


        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const uint MfString = 0x00000000;
        private const uint MfSeparator = 0x00000800;
        private const uint TpmLeftalign = 0x0000;
        private const uint TpmReturncmd = 0x0100;

        private const int SwHide = 0;
        private const int SwShow = 5;

        private IntPtr _messageWindowHandle;
        private NOTIFYICONDATA _notifyIconData;
        private bool _isDisposed;
        private WndProc _wndProc;
        private Window _mainWindow;
        private Action? _onShowWindow;
        private Action? _onExitApplication;
        private Action? _onRefresh;
        private Action? _onSettings;
        private bool _isWindowVisible = true;
        private System.Drawing.Icon? _trayIcon;  // Keep icon reference to prevent garbage collection

        public TrayIconHelper(Window mainWindow)
        {
            _mainWindow = mainWindow;
            _wndProc = WindowProc;

            // Register TaskbarCreated message
            _wmTaskbarCreated = RegisterWindowMessage("TaskbarCreated");
            Logger.LogInfo($"Registered TaskbarCreated message: {_wmTaskbarCreated}");

            if (!CreateMessageWindow())
            {
                Logger.LogError("Failed to create message window");
                return;
            }

            CreateTrayIcon();
        }

        /// <summary>
        /// Set callback functions
        /// </summary>
        public void SetCallbacks(Action onShow, Action onExit, Action? onRefresh = null, Action? onSettings = null)
        {
            _onShowWindow = onShow;
            _onExitApplication = onExit;
            _onRefresh = onRefresh;
            _onSettings = onSettings;
        }

        /// <summary>
        /// Create message window - using system predefined Message window class
        /// </summary>
        private bool CreateMessageWindow()
        {
            try
            {
                Logger.LogDebug("Creating message window using system Message class...");

                // Use system predefined "Message" window class, no registration needed
                // HWND_MESSAGE (-3) creates pure message window, no hInstance needed
                _messageWindowHandle = CreateWindowEx(
                    0,                      // dwExStyle
                    "Message",              // lpClassName - system predefined message window class
                    string.Empty,           // lpWindowName
                    0,                      // dwStyle
                    0, 0, 0, 0,            // x, y, width, height
                    new IntPtr(-3),         // hWndParent = HWND_MESSAGE (pure message window)
                    IntPtr.Zero,            // hMenu
                    IntPtr.Zero,            // hInstance - not needed
                    IntPtr.Zero             // lpParam
                );

                if (_messageWindowHandle == IntPtr.Zero)
                {
                    var error = Marshal.GetLastWin32Error();
                    Logger.LogError($"CreateWindowEx failed with error: {error}");
                    return false;
                }

                Logger.LogInfo($"Message window created successfully: {_messageWindowHandle}");

                // Set window procedure to handle our messages
                SetWindowLongPtr(_messageWindowHandle, -4, Marshal.GetFunctionPointerForDelegate(_wndProc));

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"CreateMessageWindow exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Create tray icon
        /// </summary>
        private void CreateTrayIcon()
        {
            if (_messageWindowHandle == IntPtr.Zero)
            {
                Logger.LogError("Cannot create tray icon: invalid message window handle");
                return;
            }

            // First try to delete any existing old icon (if any)
            var tempData = new NOTIFYICONDATA
            {
                CbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                HWnd = _messageWindowHandle,
                UID = 1
            };
            Shell_NotifyIcon(NimDelete, ref tempData);

            // Get icon handle
            var iconHandle = GetDefaultIcon();
            if (iconHandle == IntPtr.Zero)
            {
                Logger.LogError("Cannot create tray icon: invalid icon handle");
                return;
            }

            _notifyIconData = new NOTIFYICONDATA
            {
                CbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                HWnd = _messageWindowHandle,
                UID = 1,
                UFlags = NifMessage | NifIcon | NifTip,
                UCallbackMessage = WmTrayicon,
                HIcon = iconHandle,
                SzTip = "Power Display",
            };

            // Retry mechanism: try up to 3 times to create tray icon
            const int maxRetries = 3;
            const int retryDelayMs = 500;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                Logger.LogDebug($"Creating tray icon (attempt {attempt}/{maxRetries})...");

                bool result = Shell_NotifyIcon(NimAdd, ref _notifyIconData);
                if (result)
                {
                    Logger.LogInfo($"Tray icon created successfully on attempt {attempt}");
                    return;
                }

                var lastError = Marshal.GetLastWin32Error();
                Logger.LogWarning($"Failed to create tray icon on attempt {attempt}. Error: {lastError}");

                // Analyze specific error and provide suggestions
                switch (lastError)
                {
                    case 0: // ERROR_SUCCESS - may be false success
                        Logger.LogWarning("Shell_NotifyIcon returned false but GetLastWin32Error is 0");
                        break;
                    case 1400: // ERROR_INVALID_WINDOW_HANDLE
                        Logger.LogWarning("Invalid window handle - message window may not be properly created");
                        break;
                    case 1418: // ERROR_THREAD_1_INACTIVE
                        Logger.LogWarning("Thread inactive - may need to wait for Explorer to be ready");
                        break;
                    case 1414: // ERROR_INVALID_ICON_HANDLE
                        Logger.LogWarning("Invalid icon handle - icon may have been garbage collected");
                        break;
                    default:
                        Logger.LogWarning($"Unexpected error code: {lastError}");
                        break;
                }

                // If not the last attempt, wait and retry
                if (attempt < maxRetries)
                {
                    Logger.LogDebug($"Retrying in {retryDelayMs}ms...");
                    System.Threading.Thread.Sleep(retryDelayMs);

                    // Re-get icon handle to prevent handle invalidation
                    iconHandle = GetDefaultIcon();
                    _notifyIconData.HIcon = iconHandle;
                }
            }

            Logger.LogError($"Failed to create tray icon after {maxRetries} attempts");
        }

        /// <summary>
        /// Get default icon
        /// </summary>
        private IntPtr GetDefaultIcon()
        {
            try
            {
                // First release previous icon
                _trayIcon?.Dispose();
                _trayIcon = null;

                // Try to load icon from Assets folder in exe directory
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    var exeDir = System.IO.Path.GetDirectoryName(exePath);
                    if (!string.IsNullOrEmpty(exeDir))
                    {
                        var iconPath = System.IO.Path.Combine(exeDir, "Assets", "PowerDisplay.ico");

                        Logger.LogDebug($"Attempting to load icon from: {iconPath}");

                        if (System.IO.File.Exists(iconPath))
                        {
                            // Create icon and save as class member to prevent garbage collection
                            _trayIcon = new System.Drawing.Icon(iconPath);
                            Logger.LogInfo($"Successfully loaded custom icon from {iconPath}");
                            return _trayIcon.Handle;
                        }
                        else
                        {
                            Logger.LogWarning($"Icon file not found at: {iconPath}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load PowerDisplay icon: {ex.Message}");
                _trayIcon?.Dispose();
                _trayIcon = null;
            }

            // If loading fails, use system default icon
            var systemIconHandle = LoadIcon(IntPtr.Zero, new IntPtr(32512)); // IDI_APPLICATION
            Logger.LogInfo($"Using system default icon: {systemIconHandle}");
            return systemIconHandle;
        }

        /// <summary>
        /// Window message processing
        /// </summary>
        private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == _wmTaskbarCreated)
            {
                // Explorer restarted, need to recreate tray icon
                Logger.LogInfo("TaskbarCreated message received - recreating tray icon");
                CreateTrayIcon();
                return IntPtr.Zero;
            }

            switch (msg)
            {
                case WmTrayicon:
                    HandleTrayIconMessage(lParam);
                    break;
                case WmCommand:
                    HandleMenuCommand(wParam);
                    break;
            }

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        /// <summary>
        /// Handle tray icon messages
        /// </summary>
        private void HandleTrayIconMessage(IntPtr lParam)
        {
            switch ((uint)lParam)
            {
                case WmLbuttonup:
                    // Left click - show/hide window
                    ToggleWindowVisibility();
                    break;
                case WmRbuttonup:
                    // Right click - show menu
                    ShowContextMenu();
                    break;
            }
        }

        /// <summary>
        /// Toggle window visibility state
        /// </summary>
        private void ToggleWindowVisibility()
        {
            _isWindowVisible = !_isWindowVisible;
            if (_isWindowVisible)
            {
                _onShowWindow?.Invoke();
            }
            else
            {
                // Hide window logic will be implemented in MainWindow
                if (_mainWindow != null)
                {
                    var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);
                    ShowWindow(hWnd, SwHide);
                }
            }
        }

        /// <summary>
        /// Show right-click menu
        /// </summary>
        private void ShowContextMenu()
        {
            var hMenu = CreatePopupMenu();

            AppendMenu(hMenu, MfString, IdShow, _isWindowVisible ? "Hide Window" : "Show Window");
            if (_onRefresh != null)
            {
                AppendMenu(hMenu, MfString, IdRefresh, "Refresh Monitors");
            }
            if (_onSettings != null)
            {
                AppendMenu(hMenu, MfString, IdSettings, "Settings");
            }

            AppendMenu(hMenu, MfSeparator, 0, string.Empty);
            AppendMenu(hMenu, MfString, IdExit, "Exit");

            GetCursorPos(out POINT pt);
            SetForegroundWindow(_messageWindowHandle);

            var cmd = TrackPopupMenu(hMenu, TpmLeftalign | TpmReturncmd, pt.X, pt.Y, 0, _messageWindowHandle, IntPtr.Zero);

            if (cmd != 0)
            {
                HandleMenuCommand(new IntPtr(cmd));
            }

            DestroyMenu(hMenu);
        }

        /// <summary>
        /// Handle menu commands
        /// </summary>
        private void HandleMenuCommand(IntPtr commandId)
        {
            switch (commandId.ToInt32())
            {
                case IdShow:
                    ToggleWindowVisibility();
                    break;
                case IdRefresh:
                    _onRefresh?.Invoke();
                    break;
                case IdSettings:
                    _onSettings?.Invoke();
                    break;
                case IdExit:
                    _onExitApplication?.Invoke();
                    break;
            }
        }

        /// <summary>
        /// Show balloon tip
        /// </summary>
        public void ShowBalloonTip(string title, string text, uint timeout = 3000)
        {
            _notifyIconData.UFlags |= NifInfo;
            _notifyIconData.SzInfoTitle = title;
            _notifyIconData.SzInfo = text;
            _notifyIconData.UTimeout = timeout;
            _notifyIconData.DwInfoFlags = 1; // NIIF_INFO

            Shell_NotifyIcon(NimModify, ref _notifyIconData);
        }

        /// <summary>
        /// Update tray icon tooltip text
        /// </summary>
        public void UpdateTooltip(string tooltip)
        {
            _notifyIconData.SzTip = tooltip;
            Shell_NotifyIcon(NimModify, ref _notifyIconData);
        }

        /// <summary>
        /// Recreate tray icon (for failure recovery)
        /// </summary>
        public void RecreateTrayIcon()
        {
            Logger.LogInfo("Manually recreating tray icon...");
            CreateTrayIcon();
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                Logger.LogDebug("Disposing TrayIconHelper...");

                // Remove tray icon
                try
                {
                    Shell_NotifyIcon(NimDelete, ref _notifyIconData);
                    Logger.LogInfo("Tray icon removed successfully");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error removing tray icon: {ex.Message}");
                }

                // Release icon resources
                try
                {
                    _trayIcon?.Dispose();
                    _trayIcon = null;
                    Logger.LogInfo("Icon resources disposed successfully");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error disposing icon: {ex.Message}");
                }

                // Destroy message window
                try
                {
                    if (_messageWindowHandle != IntPtr.Zero)
                    {
                        DestroyWindow(_messageWindowHandle);
                        _messageWindowHandle = IntPtr.Zero;
                        Logger.LogInfo("Message window destroyed successfully");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error destroying message window: {ex.Message}");
                }

                _isDisposed = true;
                GC.SuppressFinalize(this);
                Logger.LogDebug("TrayIconHelper disposed completely");
            }
        }
    }
}
