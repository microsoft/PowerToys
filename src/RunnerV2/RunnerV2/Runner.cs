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
using System.Windows.Forms;
using ManagedCommon;
using RunnerV2.Helpers;
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

        private static List<IPowerToysModule> _successfullyAddedModules = [];

        public static List<IPowerToysModule> LoadedModules => _successfullyAddedModules;

        internal static bool Run(Action afterInitializationAction)
        {
            // Todo: Start tray icon
            TrayIconManager.StartTrayIcon();
            FrozenSet<string> modulesToLoad = ["PowerToys.AlwaysOnTopModuleInterface.dll", "WinUI3Apps\\PowerToys.Hosts.dll"];

            List<string> failedModuleLoads = [];

            foreach (string module in modulesToLoad)
            {
                try
                {
                    Assembly moduleAssembly = Assembly.LoadFrom(Path.GetFullPath(module));
                    Type moduleInterfaceType = moduleAssembly.GetTypes().First(t => t.GetInterfaces().Any(i => i.Name.StartsWith(typeof(IPowerToysModule).Name, StringComparison.InvariantCulture)));
                    _successfullyAddedModules.Add((IPowerToysModule)Activator.CreateInstance(moduleInterfaceType)!);
                }
                catch (Exception e)
                {
                    failedModuleLoads.Add(module);
                    Console.WriteLine($"Failed to load module {module}: {e.Message}");
                }
            }

            if (failedModuleLoads.Count > 0)
            {
                MessageBox.Show("The following modules failed to load: \n- " + string.Join("\n- ", failedModuleLoads), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            foreach (IPowerToysModule module in _successfullyAddedModules)
            {
                ToggleModuleStateBasedOnEnabledProperty(module);
            }

            afterInitializationAction();
            MessageLoop();

            return true;
        }

        private static void MessageLoop()
        {
            while (true)
            {
                if (GetMessageW(out MSG msg, IntPtr.Zero, 0, 0) != 0)
                {
                    TranslateMessage(ref msg);
                    DispatchMessageW(ref msg);

                    switch (msg.Message)
                    {
                        case (uint)WindowMessages.HOTKEY:
                            HotkeyManager.ProcessHotkey(msg.WParam);
                            break;
                        case (uint)WindowMessages.ICON_NOTIFY:
                            TrayIconManager.ProcessTrayIconMessage(msg.LParam);
                            break;
                        case (uint)WindowMessages.COMMAND:
                            TrayIconManager.ProcessTrayMenuCommand(msg.WParam);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        [DoesNotReturn]
        internal static void Close()
        {
            foreach (IPowerToysModule module in _successfullyAddedModules)
            {
                try
                {
                    module.Disable();
                    if (module.HotkeyEx is not null)
                    {
                        HotkeyManager.DisableHotkey(module.HotkeyEx);
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
            if ((module.Enabled && (module.GpoRuleConfigured != PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)) || module.GpoRuleConfigured == PowerToys.GPOWrapper.GpoRuleConfigured.Enabled)
            {
                try
                {
                    module.Enable();

                    /* Todo: conflict manager */

                    if (module.HotkeyEx is not null)
                    {
                        HotkeyManager.EnableHotkey(module.HotkeyEx, module.OnHotkey);
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show($"The module {module.Name} failed to load: \n" + e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                return;
            }

            try
            {
                module.Disable();

                if (module.HotkeyEx is not null)
                {
                    HotkeyManager.DisableHotkey(module.HotkeyEx);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show($"The module {module.Name} failed to unload: \n" + e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

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
                LpfnWndProc = TrayIconWindowProc,
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

        private static IntPtr TrayIconWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            TrayIconManager.ProcessTrayIconMessage(lParam.ToInt64());
            return DefWindowProcW(hWnd, msg, wParam, lParam);
        }
    }
}
