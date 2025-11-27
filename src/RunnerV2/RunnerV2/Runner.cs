// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ManagedCommon;
using RunnerV2.Helpers;
using Update;
using static RunnerV2.NativeMethods;

namespace RunnerV2
{
    internal static partial class Runner
    {
        public static nint RunnerHwnd { get; private set; }

        private const string TrayWindowClassName = "pt_tray_icon_window_class";

        static Runner()
        {
            InitializeTrayWindow();
        }

        public static List<IPowerToysModule> LoadedModules { get; } = [];

        public static FrozenSet<IPowerToysModule> ModulesToLoad { get; } =
        [
            new ModuleInterfaces.AlwaysOnTopModuleInterface(),
            new ModuleInterfaces.HostsModuleInterface(),
            new ModuleInterfaces.PowerAccentModuleInterface(),
        ];

        internal static bool Run(Action afterInitializationAction)
        {
            TrayIconManager.StartTrayIcon();

            Task.Run(UpdateUtilities.UninstallPreviousMsixVersions);

            foreach (IPowerToysModule module in ModulesToLoad)
            {
                ToggleModuleStateBasedOnEnabledProperty(module);
            }

            afterInitializationAction();

            MessageLoop();

            return true;
        }

        private static readonly uint _taskbarCreatedMessage = RegisterWindowMessageW("TaskbarCreated");

        [STAThread]
        private static void MessageLoop()
        {
            while (GetMessageW(out MSG msg, IntPtr.Zero, 0, 0) != 0)
            {
                TranslateMessage(ref msg);
                DispatchMessageW(ref msg);

                // Supress duplicate handling of HOTKEY messages
                if (msg.Message == (uint)WindowMessages.HOTKEY)
                {
                    continue;
                }

                HandleMessage(msg.HWnd, msg.Message, (nint)msg.WParam, (nint)msg.LParam);
            }

            Close();
        }

        [DoesNotReturn]
        internal static void Close()
        {
            TrayIconManager.StopTrayIcon();
            SettingsHelper.CloseSettingsWindow();
            ElevationHelper.RestartIfScheudled();

            foreach (IPowerToysModule module in LoadedModules)
            {
                try
                {
                    module.Disable();
                    foreach (var hotkey in module.Hotkeys)
                    {
                        HotkeyManager.DisableHotkey(hotkey.Key);
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show($"The module {module.Name} failed to unload: \n" + e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            Environment.Exit(0);
        }

        public static void ToggleModuleStateBasedOnEnabledProperty(IPowerToysModule module)
        {
            try
            {
                if ((module.Enabled && (module.GpoRuleConfigured != PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)) || module.GpoRuleConfigured == PowerToys.GPOWrapper.GpoRuleConfigured.Enabled)
                {
                    /* Todo: conflict manager */

                    // ToArray is called to mitigate mutations while the foreach is executing
                    foreach (var hotkey in module.Hotkeys.ToArray())
                    {
                        HotkeyManager.EnableHotkey(hotkey.Key, hotkey.Value);
                    }

                    if (!LoadedModules.Contains(module))
                    {
                        module.Enable();
                        LoadedModules.Add(module);
                    }

                    return;
                }
            }
            catch (IOException)
            {
            }
            catch (Exception e)
            {
                MessageBox.Show($"The module {module.Name} failed to load: \n" + e.Message, "Error: " + e.GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                module.Disable();

                foreach (var hotkey in module.Hotkeys)
                {
                    HotkeyManager.DisableHotkey(hotkey.Key);
                }

                LoadedModules.Remove(module);
            }
            catch (IOException)
            {
            }
            catch (Exception e)
            {
                MessageBox.Show($"The module {module.Name} failed to unload: \n" + e.Message, "Error: " + e.GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        [STAThread]
        private static void InitializeTrayWindow()
        {
            IntPtr hInstance = Process.GetCurrentProcess().MainModule!.BaseAddress;
            IntPtr hCursor = Cursors.Arrow.Handle;
            IntPtr hIcon = SystemIcons.Application.Handle;

            var wc = new WNDCLASS
            {
                HCursor = hCursor,
                HInstance = hInstance,
                LpszClassName = TrayWindowClassName,
                Style = CSHREDRAW | CSVREDRAW,
                LpfnWndProc = HandleMessage,
                HIcon = hIcon,
                HbrBackground = IntPtr.Zero,
                LpszMenuName = string.Empty,
                CbClsExtra = 0,
                CbWndExtra = 0,
            };

            _ = RegisterClassW(ref wc);

            RunnerHwnd = CreateWindowExW(
                0,
                wc.LpszClassName,
                TrayWindowClassName,
                WSOVERLAPPEDWINDOW | WSPOPUP,
                CWUSEDEFAULT,
                CWUSEDEFAULT,
                CWUSEDEFAULT,
                CWUSEDEFAULT,
                IntPtr.Zero,
                IntPtr.Zero,
                wc.HInstance,
                IntPtr.Zero);

            if (RunnerHwnd == IntPtr.Zero)
            {
                var err = Marshal.GetLastPInvokeError();
                MessageBox.Show($"CreateWindowExW failed. LastError={err}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static IntPtr HandleMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case (uint)WindowMessages.HOTKEY:
                    HotkeyManager.ProcessHotkey((nuint)wParam);
                    break;
                case (uint)WindowMessages.ICON_NOTIFY:
                    TrayIconManager.ProcessTrayIconMessage(lParam);
                    break;
                case (uint)WindowMessages.COMMAND:
                    TrayIconManager.ProcessTrayMenuCommand((nuint)wParam);
                    break;
                case (uint)WindowMessages.WINDOWPOSCHANGING:
                    TrayIconManager.StartTrayIcon();
                    break;
                case (uint)WindowMessages.DESTROY:
                    Close();
                    break;
                case (uint)WindowMessages.REFRESH_SETTINGS:
                    foreach (IPowerToysModule module in ModulesToLoad)
                    {
                        ToggleModuleStateBasedOnEnabledProperty(module);
                    }

                    break;
                default:
                    if (msg == _taskbarCreatedMessage)
                    {
                        TrayIconManager.StartTrayIcon();
                    }

                    break;
            }

            return DefWindowProcW(hWnd, msg, wParam, lParam);
        }
    }
}
