// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Awake.Core.Models;
using Awake.Core.Native;
using Awake.Core.Threading;
using Awake.Properties;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Awake.Core
{
    /// <summary>
    /// Helper class used to manage the system tray.
    /// </summary>
    /// <remarks>
    /// Because Awake is a console application, there is no built-in
    /// way to embed UI components so we have to heavily rely on the native Windows API.
    /// </remarks>
    internal static class TrayHelper
    {
        private static NotifyIconData _notifyIconData;
        private static SingleThreadSynchronizationContext? _syncContext;
        private static Thread? _mainThread;
        private static uint _taskbarCreatedMessage;

        private static IntPtr TrayMenu { get; set; }

        internal static IntPtr WindowHandle { get; private set; }

        internal static readonly Icon DefaultAwakeIcon = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/Awake/awake.ico"));
        internal static readonly Icon TimedIcon = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/Awake/timed.ico"));
        internal static readonly Icon ExpirableIcon = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/Awake/expirable.ico"));
        internal static readonly Icon IndefiniteIcon = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/Awake/indefinite.ico"));
        internal static readonly Icon DisabledIcon = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/Awake/disabled.ico"));

        static TrayHelper()
        {
            TrayMenu = IntPtr.Zero;
            WindowHandle = IntPtr.Zero;
        }

        private static void ShowContextMenu(IntPtr hWnd)
        {
            if (TrayMenu == IntPtr.Zero)
            {
                Logger.LogError("Tried to create a context menu while the TrayMenu object is a null pointer. Normal when used in standalone mode.");
                return;
            }

            Bridge.SetForegroundWindow(hWnd);

            // Get cursor position in screen coordinates
            Bridge.GetCursorPos(out Models.Point cursorPos);

            // Set menu information
            MenuInfo menuInfo = new()
            {
                CbSize = (uint)Marshal.SizeOf<MenuInfo>(),
                FMask = Native.Constants.MIM_STYLE,
                DwStyle = Native.Constants.MNS_AUTO_DISMISS,
            };
            Bridge.SetMenuInfo(TrayMenu, ref menuInfo);

            // Display the context menu at the cursor position
            Bridge.TrackPopupMenuEx(
                  TrayMenu,
                  Native.Constants.TPM_LEFT_ALIGN | Native.Constants.TPM_BOTTOMALIGN | Native.Constants.TPM_LEFT_BUTTON,
                  cursorPos.X,
                  cursorPos.Y,
                  hWnd,
                  IntPtr.Zero);
        }

        public static Task InitializeTray(Icon icon, string text)
        {
            TaskCompletionSource<bool> trayInitialized = new();

            IntPtr hWnd = IntPtr.Zero;

            // Start the message loop asynchronously
            _mainThread = new Thread(() =>
            {
                _syncContext = new SingleThreadSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(_syncContext);

                RunOnMainThread(() =>
                {
                    try
                    {
                        WndClassEx wcex = new()
                        {
                            CbSize = (uint)Marshal.SizeOf<WndClassEx>(),
                            Style = 0,
                            LpfnWndProc = Marshal.GetFunctionPointerForDelegate<Bridge.WndProcDelegate>(WndProc),
                            CbClsExtra = 0,
                            CbWndExtra = 0,
                            HInstance = Marshal.GetHINSTANCE(typeof(Program).Module),
                            HIcon = IntPtr.Zero,
                            HCursor = IntPtr.Zero,
                            HbrBackground = IntPtr.Zero,
                            LpszMenuName = string.Empty,
                            LpszClassName = Constants.TrayWindowId,
                            HIconSm = IntPtr.Zero,
                        };

                        Bridge.RegisterClassEx(ref wcex);

                        hWnd = Bridge.CreateWindowEx(
                            0,
                            Constants.TrayWindowId,
                            text,
                            0x00CF0000 | 0x00000001 | 0x00000008, // WS_OVERLAPPEDWINDOW | WS_VISIBLE | WS_MINIMIZEBOX
                            0,
                            0,
                            0,
                            0,
                            IntPtr.Zero,
                            IntPtr.Zero,
                            Marshal.GetHINSTANCE(typeof(Program).Module),
                            IntPtr.Zero);

                        if (hWnd == IntPtr.Zero)
                        {
                            int errorCode = Marshal.GetLastWin32Error();
                            throw new Win32Exception(errorCode, "Failed to add tray icon. Error code: " + errorCode);
                        }

                        // Keep this as a reference because we will need it when we update
                        // the tray icon in the future.
                        WindowHandle = hWnd;

                        Bridge.ShowWindow(hWnd, 0); // SW_HIDE
                        Bridge.UpdateWindow(hWnd);
                        Logger.LogInfo($"Created HWND for the window: {hWnd}");

                        SetShellIcon(hWnd, text, icon);

                        trayInitialized.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to properly initialize the tray. {ex.Message}");
                        trayInitialized.SetException(ex);
                    }
                });

                RunOnMainThread(() =>
                {
                    RunMessageLoop();
                });

                _syncContext!.BeginMessageLoop();
            });

            _mainThread.IsBackground = true;
            _mainThread.Start();

            return trayInitialized.Task;
        }

        internal static void SetShellIcon(IntPtr hWnd, string text, Icon? icon, TrayIconAction action = TrayIconAction.Add, [CallerMemberName] string callerName = "")
        {
            if (hWnd != IntPtr.Zero && icon != null)
            {
                int message = Native.Constants.NIM_ADD;

                switch (action)
                {
                    case TrayIconAction.Update:
                        message = Native.Constants.NIM_MODIFY;
                        break;
                    case TrayIconAction.Delete:
                        message = Native.Constants.NIM_DELETE;
                        break;
                    case TrayIconAction.Add:
                    default:
                        break;
                }

                if (action is TrayIconAction.Add or TrayIconAction.Update)
                {
                    _notifyIconData = new NotifyIconData
                    {
                        CbSize = Marshal.SizeOf<NotifyIconData>(),
                        HWnd = hWnd,
                        UId = 1000,
                        UFlags = Native.Constants.NIF_ICON | Native.Constants.NIF_TIP | Native.Constants.NIF_MESSAGE,
                        UCallbackMessage = (int)Native.Constants.WM_USER,
                        HIcon = icon?.Handle ?? IntPtr.Zero,
                        SzTip = text,
                    };
                }
                else if (action == TrayIconAction.Delete)
                {
                    _notifyIconData = new NotifyIconData
                    {
                        CbSize = Marshal.SizeOf<NotifyIconData>(),
                        HWnd = hWnd,
                        UId = 1000,
                        UFlags = 0,
                    };
                }

                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    if (Bridge.Shell_NotifyIcon(message, ref _notifyIconData))
                    {
                        break;
                    }
                    else
                    {
                        int errorCode = Marshal.GetLastWin32Error();
                        Logger.LogInfo($"Could not set the shell icon. Action: {action}, error code: {errorCode}. HIcon handle is {icon?.Handle} and HWnd is {hWnd}. Invoked by {callerName}.");

                        if (attempt == 3)
                        {
                            Logger.LogError($"Failed to change tray icon after 3 attempts. Action: {action} and error code: {errorCode}. Invoked by {callerName}.");
                            break;
                        }

                        Thread.Sleep(100);
                    }
                }

                if (action == TrayIconAction.Delete)
                {
                    _notifyIconData = default;
                }
            }
            else
            {
                Logger.LogInfo($"Cannot set the shell icon - parent window handle is zero or icon is not available. Text: {text} Action: {action}");
            }
        }

        private static void RunMessageLoop()
        {
            while (Bridge.GetMessage(out Msg msg, IntPtr.Zero, 0, 0))
            {
                Bridge.TranslateMessage(ref msg);
                Bridge.DispatchMessage(ref msg);
            }

            Logger.LogInfo("Message loop terminated.");
        }

        private static int WndProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
        {
            switch (message)
            {
                case Native.Constants.WM_USER:
                    if (lParam is Native.Constants.WM_LBUTTONDOWN or Native.Constants.WM_RBUTTONDOWN)
                    {
                        // Show the context menu associated with the tray icon
                        ShowContextMenu(hWnd);
                    }

                    break;

                case Native.Constants.WM_CREATE:
                    {
                        _taskbarCreatedMessage = (uint)Bridge.RegisterWindowMessage("TaskbarCreated");
                    }

                    break;
                case Native.Constants.WM_DESTROY:
                    // Clean up resources when the window is destroyed
                    Bridge.PostQuitMessage(0);
                    break;
                case Native.Constants.WM_COMMAND:
                    int trayCommandsSize = Enum.GetNames<TrayCommands>().Length;

                    long targetCommandIndex = wParam.ToInt64() & 0xFFFF;

                    switch (targetCommandIndex)
                    {
                        case (uint)TrayCommands.TC_EXIT:
                            {
                                Manager.CompleteExit(Environment.ExitCode);
                                break;
                            }

                        case (uint)TrayCommands.TC_DISPLAY_SETTING:
                            {
                                Manager.SetDisplay();
                                break;
                            }

                        case (uint)TrayCommands.TC_MODE_INDEFINITE:
                            {
                                AwakeSettings settings = Manager.ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName);
                                Manager.SetIndefiniteKeepAwake(keepDisplayOn: settings.Properties.KeepDisplayOn);
                                break;
                            }

                        case (uint)TrayCommands.TC_MODE_PASSIVE:
                            {
                                Manager.SetPassiveKeepAwake();
                                break;
                            }

                        default:
                            {
                                if (targetCommandIndex >= trayCommandsSize)
                                {
                                    AwakeSettings settings = Manager.ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName);
                                    if (settings.Properties.CustomTrayTimes.Count == 0)
                                    {
                                        settings.Properties.CustomTrayTimes.AddRange(Manager.GetDefaultTrayOptions());
                                    }

                                    int index = (int)targetCommandIndex - (int)TrayCommands.TC_TIME;
                                    uint targetTime = settings.Properties.CustomTrayTimes.ElementAt(index).Value;
                                    Manager.SetTimedKeepAwake(targetTime, keepDisplayOn: settings.Properties.KeepDisplayOn);
                                }

                                break;
                            }
                    }

                    break;
                default:
                    if (message == _taskbarCreatedMessage)
                    {
                        Logger.LogInfo("Taskbar re-created");
                        Manager.SetModeShellIcon(forceAdd: true);
                    }

                    // Let the default window procedure handle other messages
                    return Bridge.DefWindowProc(hWnd, message, wParam, lParam);
            }

            return Bridge.DefWindowProc(hWnd, message, wParam, lParam);
        }

        internal static void RunOnMainThread(Action action)
        {
            _syncContext!.Post(
                _ =>
                {
                    try
                    {
                        Logger.LogInfo($"Thread execution is on: {Environment.CurrentManagedThreadId}");
                        action();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error: " + e.Message);
                    }
                },
                null);
        }

        internal static void SetTray(AwakeSettings settings, bool startedFromPowerToys)
        {
            SetTray(
                settings.Properties.KeepDisplayOn,
                settings.Properties.Mode,
                settings.Properties.CustomTrayTimes,
                startedFromPowerToys);
        }

        public static void SetTray(bool keepDisplayOn, AwakeMode mode, Dictionary<string, uint> trayTimeShortcuts, bool startedFromPowerToys)
        {
            ClearExistingTrayMenu();
            CreateNewTrayMenu(startedFromPowerToys, keepDisplayOn, mode);

            InsertAwakeModeMenuItems(mode);

            EnsureDefaultTrayTimeShortcuts(trayTimeShortcuts);
            CreateAwakeTimeSubMenu(trayTimeShortcuts, mode == AwakeMode.TIMED);
        }

        private static void ClearExistingTrayMenu()
        {
            if (TrayMenu != IntPtr.Zero && !Bridge.DestroyMenu(TrayMenu))
            {
                int errorCode = Marshal.GetLastWin32Error();
                Logger.LogError($"Failed to destroy menu: {errorCode}");
            }
        }

        private static void CreateNewTrayMenu(bool startedFromPowerToys, bool keepDisplayOn, AwakeMode mode)
        {
            TrayMenu = Bridge.CreatePopupMenu();

            if (TrayMenu == IntPtr.Zero)
            {
                return;
            }

            if (!startedFromPowerToys)
            {
                InsertMenuItem(0, TrayCommands.TC_EXIT, Resources.AWAKE_EXIT);
            }

            InsertMenuItem(0, TrayCommands.TC_DISPLAY_SETTING, Resources.AWAKE_KEEP_SCREEN_ON, keepDisplayOn, mode == AwakeMode.PASSIVE);

            if (!startedFromPowerToys)
            {
                InsertSeparator(1);
            }
        }

        private static void InsertMenuItem(int position, TrayCommands command, string text, bool checkedState = false, bool disabled = false)
        {
            uint state = Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING;
            state |= checkedState ? Native.Constants.MF_CHECKED : Native.Constants.MF_UNCHECKED;
            state |= disabled ? Native.Constants.MF_DISABLED : Native.Constants.MF_ENABLED;

            Bridge.InsertMenu(TrayMenu, (uint)position, state, (uint)command, text);
        }

        private static void InsertSeparator(int position)
        {
            Bridge.InsertMenu(TrayMenu, (uint)position, Native.Constants.MF_BYPOSITION | Native.Constants.MF_SEPARATOR, 0, string.Empty);
        }

        private static void EnsureDefaultTrayTimeShortcuts(Dictionary<string, uint> trayTimeShortcuts)
        {
            if (trayTimeShortcuts.Count == 0)
            {
                trayTimeShortcuts.AddRange(Manager.GetDefaultTrayOptions());
            }
        }

        private static void CreateAwakeTimeSubMenu(Dictionary<string, uint> trayTimeShortcuts, bool isChecked = false)
        {
            nint awakeTimeMenu = Bridge.CreatePopupMenu();
            for (int i = 0; i < trayTimeShortcuts.Count; i++)
            {
                Bridge.InsertMenu(awakeTimeMenu, (uint)i, Native.Constants.MF_BYPOSITION | Native.Constants.MF_STRING, (uint)TrayCommands.TC_TIME + (uint)i, trayTimeShortcuts.ElementAt(i).Key);
            }

            Bridge.InsertMenu(TrayMenu, 0, Native.Constants.MF_BYPOSITION | Native.Constants.MF_POPUP | (isChecked ? Native.Constants.MF_CHECKED : Native.Constants.MF_UNCHECKED), (uint)awakeTimeMenu, Resources.AWAKE_KEEP_ON_INTERVAL);
        }

        private static void InsertAwakeModeMenuItems(AwakeMode mode)
        {
            InsertSeparator(0);

            InsertMenuItem(0, TrayCommands.TC_MODE_PASSIVE, Resources.AWAKE_OFF, mode == AwakeMode.PASSIVE);
            InsertMenuItem(0, TrayCommands.TC_MODE_INDEFINITE, Resources.AWAKE_KEEP_INDEFINITELY, mode == AwakeMode.INDEFINITE);
            InsertMenuItem(0, TrayCommands.TC_MODE_EXPIRABLE, Resources.AWAKE_KEEP_UNTIL_EXPIRATION, mode == AwakeMode.EXPIRABLE, true);
        }
    }
}
