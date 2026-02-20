// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using PowerToys.Interop;
using RunnerV2.Models;
using RunnerV2.properties;
using static RunnerV2.NativeMethods;

namespace RunnerV2.Helpers
{
    internal static partial class TrayIconManager
    {
        private static bool _trayIconVisible;

        private static nint GetTrayIcon()
        {
            if (SettingsUtils.Default.GetSettings<GeneralSettings>().ShowThemeAdaptiveTrayIcon)
            {
                return Icon.ExtractAssociatedIcon(ThemeHelper.GetCurrentSystemTheme() ? "./Assets/Runner/PowerToysDark.ico" : "./Assets/Runner/PowerToysLight.ico")!.Handle;
            }
            else
            {
                return Icon.ExtractAssociatedIcon(Environment.ProcessPath!)!.Handle;
            }
        }

        public static void UpdateTrayIcon()
        {
            if (!_trayIconVisible)
            {
                return;
            }

            NOTIFYICONDATA notifyicondata = GetNOTIFYICONDATA();
            Shell_NotifyIcon(0x1, ref notifyicondata);
        }

        private static NOTIFYICONDATA GetNOTIFYICONDATA() => new()
        {
            CbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            HWnd = Runner.RunnerHwnd,
            UId = 1,
            HIcon = GetTrayIcon(),
            UFlags = 0x0000001 | 0x00000002 | 0x4,
            UCallbackMessage = (uint)WindowMessages.ICON_NOTIFY,
            SzTip = "PowerToys v" + Assembly.GetExecutingAssembly().GetName().Version!.Major + "." + Assembly.GetExecutingAssembly().GetName().Version!.Minor + "." + Assembly.GetExecutingAssembly().GetName().Version!.Build,
        };

        internal static void StartTrayIcon()
        {
            if (_trayIconVisible)
            {
                return;
            }

            NOTIFYICONDATA notifyicondata = GetNOTIFYICONDATA();
            ChangeWindowMessageFilterEx(Runner.RunnerHwnd, 0x0111, 0x0001, IntPtr.Zero);

            Shell_NotifyIcon(NIMADD, ref notifyicondata);
            _trayIconVisible = true;
        }

        internal static void StopTrayIcon()
        {
            if (!_trayIconVisible)
            {
                return;
            }

            NOTIFYICONDATA notifyicondata = new()
            {
                CbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                HWnd = Runner.RunnerHwnd,
                UId = 1,
            };

            Shell_NotifyIcon(NIMDELETE, ref notifyicondata);
            _trayIconVisible = false;
        }

        internal enum TrayButton : uint
        {
            Settings = 1,
            Documentation,
            ReportBug,
            Close,
            QuickAccess,
            DebugKeyboardShortcuts,
            DebugSettingsIpc,
            DebugWriteUncaughtExceptionsToConsole,
            DebugSendCustomMessage,
            DebugActivateModule,
            DebugDeactivateModule,
            DebugAllEnableModule,
            DebugAllDisableModule,
            DebugSendCustomAction,
            DebugListAllCustomActions,
            DebugListAllShortcuts,
            DebugFullModuleReport,
        }

        private static bool _doubleClickTimerRunning;
        private static bool _doubleClickDetected;

        private static IntPtr _trayIconMenu;

        static TrayIconManager()
        {
            RegenerateRightClickMenu();
            new ThemeListener().ThemeChanged += (_) =>
            {
                PostMessageW(Runner.RunnerHwnd, 0x0800, IntPtr.Zero, 0x9000);
            };
        }

        public static void RegenerateRightClickMenu()
        {
            _trayIconMenu = CreatePopupMenu();
#if DEBUG
            IntPtr ipcMenu = CreateMenu();
            AppendMenuW(ipcMenu, 0u, new UIntPtr((uint)TrayButton.DebugSettingsIpc), "Toggle logging Settings IPC");
            AppendMenuW(ipcMenu, 0u, new UIntPtr((uint)TrayButton.DebugSendCustomMessage), "Send Custom Message To Runner");

            IntPtr modulesMenu = CreateMenu();
            AppendMenuW(modulesMenu, 0u, new UIntPtr((uint)TrayButton.DebugFullModuleReport), "Full report on all modules");
            AppendMenuW(modulesMenu, 0x00000800u, UIntPtr.Zero, string.Empty); // serator
            AppendMenuW(modulesMenu, 0u, new UIntPtr((uint)TrayButton.DebugActivateModule), "Activate Module by name");
            AppendMenuW(modulesMenu, 0u, new UIntPtr((uint)TrayButton.DebugDeactivateModule), "Deactivate Module by name");
            AppendMenuW(modulesMenu, 0u, new UIntPtr((uint)TrayButton.DebugAllEnableModule), "Run all enable functions of all modules");
            AppendMenuW(modulesMenu, 0u, new UIntPtr((uint)TrayButton.DebugAllDisableModule), "Run all disable functions of all modules");
            AppendMenuW(modulesMenu, 0u, new UIntPtr((uint)TrayButton.DebugSendCustomAction), "Send Custom Action");
            AppendMenuW(modulesMenu, 0u, new UIntPtr((uint)TrayButton.DebugListAllCustomActions), "List all Custom Actions");
            AppendMenuW(modulesMenu, 0u, new UIntPtr((uint)TrayButton.DebugListAllShortcuts), "List all Shortcuts");

            AppendMenuW(_trayIconMenu, 0x2u, UIntPtr.Zero, "Debug build options:");
            AppendMenuW(_trayIconMenu, 0x10u, (UIntPtr)ipcMenu, "Settings <-> Runner IPC");
            AppendMenuW(_trayIconMenu, 0x10u, (UIntPtr)modulesMenu, "Module interfaces");
            AppendMenuW(_trayIconMenu, 0u, new UIntPtr((uint)TrayButton.DebugKeyboardShortcuts), "Toggle logging Centralized Keyboard Hook");
            AppendMenuW(_trayIconMenu, 0u, new UIntPtr((uint)TrayButton.DebugWriteUncaughtExceptionsToConsole), "Toggle logging Uncaught Exceptions");
            AppendMenuW(_trayIconMenu, 0x00000800u, UIntPtr.Zero, string.Empty); // separator
#endif
            if (SettingsUtils.Default.GetSettings<GeneralSettings>().EnableQuickAccess)
            {
                AppendMenuW(_trayIconMenu, 0u, new UIntPtr((uint)TrayButton.QuickAccess), Resources.ContextMenu_QuickAccess + "\t" + Resources.ContextMenu_LeftClick);
                AppendMenuW(_trayIconMenu, 0u, new UIntPtr((uint)TrayButton.Settings), Resources.ContextMenu_Settings + "\t" + Resources.ContextMenu_DoubleClick);
            }
            else
            {
                AppendMenuW(_trayIconMenu, 0u, new UIntPtr((uint)TrayButton.Settings), Resources.ContextMenu_Settings + "\t" + Resources.ContextMenu_LeftClick);
            }

            AppendMenuW(_trayIconMenu, 0x00000800u, UIntPtr.Zero, string.Empty); // serator
            AppendMenuW(_trayIconMenu, 0u, new UIntPtr((uint)TrayButton.Documentation), Resources.ContextMenu_Documentation);
            AppendMenuW(_trayIconMenu, 0u, new UIntPtr((uint)TrayButton.ReportBug), Resources.ContextMenu_ReportBug);
            AppendMenuW(_trayIconMenu, 0x00000800u, UIntPtr.Zero, string.Empty); // separator
            AppendMenuW(_trayIconMenu, 0u, new UIntPtr((uint)TrayButton.Close), Resources.ContextMenu_Close);
        }

        internal static void ProcessTrayIconMessage(long lParam)
        {
            switch (lParam)
            {
                case 0x0205: // WM_RBUTTONDBLCLK
                case 0x007B: // WM_CONTEXTMENU
                    SetForegroundWindow(Runner.RunnerHwnd);
                    TrackPopupMenu(_trayIconMenu, 0x0004 | 0x0020, Cursor.Position.X, Cursor.Position.Y, 0, Runner.RunnerHwnd, IntPtr.Zero);
                    break;
                case 0x0202: // WM_LBUTTONUP
                    if (_doubleClickTimerRunning)
                    {
                        break;
                    }

                    _doubleClickTimerRunning = true;
                    Task.Delay(SystemInformation.DoubleClickTime).ContinueWith(_ =>
                    {
                        if (!_doubleClickDetected)
                        {
                            if (SettingsUtils.Default.GetSettings<GeneralSettings>().EnableQuickAccess)
                            {
                                QuickAccessHelper.Show();
                            }
                            else
                            {
                                SettingsHelper.OpenSettingsWindow();
                            }
                        }

                        _doubleClickDetected = false;
                        _doubleClickTimerRunning = false;
                    });
                    break;
                case 0x0203: // WM_LBUTTONDBLCLK
                    _doubleClickDetected = true;
                    SettingsHelper.OpenSettingsWindow();
                    break;
                case 0x9000: // Update tray icon
                    UpdateTrayIcon();
                    RegenerateRightClickMenu();
                    break;
            }
        }

        internal static bool IsBugReportToolRunning { get; set; }

        internal static void ProcessTrayMenuCommand(nuint commandId)
        {
            switch ((TrayButton)commandId)
            {
                case TrayButton.Settings:
                    SettingsHelper.OpenSettingsWindow();
                    break;
                case TrayButton.QuickAccess:
                    QuickAccessHelper.Show();
                    break;
                case TrayButton.Documentation:
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://aka.ms/PowerToysOverview",
                        UseShellExecute = true,
                    });
                    break;
                case TrayButton.ReportBug:
                    Logger.LogInfo("Starting bug report tool from tray menu");
                    Process bugReportProcess = new();
                    bugReportProcess.StartInfo = new ProcessStartInfo
                    {
                        FileName = "Tools\\PowerToys.BugReportTool.exe",
                        CreateNoWindow = true,
                    };

                    bugReportProcess.EnableRaisingEvents = true;

                    EnableMenuItem(_trayIconMenu, (uint)TrayButton.ReportBug, 0x000000 | 0x00001);

                    bugReportProcess.Exited += (sender, e) =>
                    {
                        bugReportProcess.Dispose();
                        EnableMenuItem(_trayIconMenu, (uint)TrayButton.ReportBug, 0x00000000);
                        IsBugReportToolRunning = false;
                        Logger.LogInfo("Bug report tool exited");
                    };

                    bugReportProcess.Start();
                    IsBugReportToolRunning = true;

                    break;
                case TrayButton.Close:
                    Runner.Close();
                    break;
#if DEBUG
                case TrayButton.DebugKeyboardShortcuts:
                    AllocConsole();
                    CentralizedKeyboardHookManager.DebugConsole = !CentralizedKeyboardHookManager.DebugConsole;
                    break;
                case TrayButton.DebugSettingsIpc:
                    AllocConsole();
                    SettingsHelper.Debugging = !SettingsHelper.Debugging;
                    break;
                case TrayButton.DebugWriteUncaughtExceptionsToConsole:
                    AllocConsole();
                    Runner.DebbugingLogUncaughtExceptions = !Runner.DebbugingLogUncaughtExceptions;
                    break;
                case TrayButton.DebugSendCustomMessage:
                    AllocConsole();
                    Console.Write("Enter the message you want to send to the Runner process: ");
                    string? message = Console.ReadLine();
                    if (string.IsNullOrEmpty(message))
                    {
                        Console.WriteLine("No message entered. Aborting.");
                        return;
                    }

                    SettingsHelper.OnSettingsMessageReceived(message);
                    break;
                case TrayButton.DebugActivateModule:
                    AllocConsole();
                    Console.Write("Enter the name of the module you want to activate: ");
                    string? moduleToActivate = Console.ReadLine();
                    if (string.IsNullOrEmpty(moduleToActivate))
                    {
                        Console.WriteLine("No module name entered. Aborting.");
                        return;
                    }

                    SettingsHelper.OnSettingsMessageReceived("{\"module_status\": {\"" + moduleToActivate + "\": true}}");
                    break;
                case TrayButton.DebugDeactivateModule:
                    AllocConsole();
                    Console.Write("Enter the name of the module you want to deactivate: ");
                    string? moduleToDeactivate = Console.ReadLine();
                    if (string.IsNullOrEmpty(moduleToDeactivate))
                    {
                        Console.WriteLine("No module name entered. Aborting.");
                        return;
                    }

                    SettingsHelper.OnSettingsMessageReceived("{\"module_status\": {\"" + moduleToDeactivate + "\": false}}");
                    break;

                case TrayButton.DebugAllEnableModule:
                    AllocConsole();
                    foreach (var module in Runner.ModulesToLoad)
                    {
                        module.Enable();
                    }

                    Console.WriteLine("All enabled functions run.");
                    break;
                case TrayButton.DebugAllDisableModule:
                    AllocConsole();
                    foreach (var module in Runner.ModulesToLoad)
                    {
                        module.Disable();
                    }

                    Console.WriteLine("All disable functions run.");
                    break;
                case TrayButton.DebugSendCustomAction:
                    AllocConsole();
                    Console.Write("Enter the name of the module you want to send the action to: ");
                    string? moduleName = Console.ReadLine();
                    if (string.IsNullOrEmpty(moduleName))
                    {
                        Console.WriteLine("No module name entered. Aborting.");
                        return;
                    }

                    Console.Write("Enter the name of the action you want to send: ");
                    string? actionName = Console.ReadLine();
                    if (string.IsNullOrEmpty(actionName))
                    {
                        Console.WriteLine("No action name entered. Aborting.");
                        return;
                    }

                    Console.Write("Enter the data you want to send with the action (or leave empty for no data): ");
                    string? actionData = Console.ReadLine();
                    if (actionData is null)
                    {
                        Console.WriteLine("No action data entered. Aborting.");
                        return;
                    }

                    SettingsHelper.OnSettingsMessageReceived("{\"action\": {\"" + moduleName + "\": {\"action_name\": \"" + actionName + "\", \"value\": \"" + actionData + "\"}}}");
                    break;
                case TrayButton.DebugListAllCustomActions:
                    AllocConsole();
                    Console.Write("Name of the module whose custom actions you want to list: ");
                    string? moduleNameForCustomActions = Console.ReadLine();
                    if (string.IsNullOrEmpty(moduleNameForCustomActions))
                    {
                        Console.WriteLine("No module name entered. Aborting.");
                        return;
                    }

                    var moduleForCustomActions = Runner.ModulesToLoad.FirstOrDefault(m => m.Name.Equals(moduleNameForCustomActions, StringComparison.OrdinalIgnoreCase));
                    if (moduleForCustomActions == null)
                    {
                        Console.WriteLine("No module with that name found. Aborting.");
                        return;
                    }

                    if (moduleForCustomActions is not IPowerToysModuleCustomActionsProvider customActionsProvider)
                    {
                        Console.WriteLine("Module does not provide custom actions. Aborting.");
                        return;
                    }

                    foreach (var customAction in customActionsProvider.CustomActions)
                    {
                        Console.WriteLine("Action name: " + customAction.Key);
                    }

                    break;
                case TrayButton.DebugListAllShortcuts:
                    AllocConsole();
                    Console.Write("Name of the module whose shortcuts you want to list: ");
                    string? moduleNameForShortcuts = Console.ReadLine();
                    if (string.IsNullOrEmpty(moduleNameForShortcuts))
                    {
                        Console.WriteLine("No module name entered. Aborting.");
                        return;
                    }

                    var moduleForShortcuts = Runner.ModulesToLoad.FirstOrDefault(m => m.Name.Equals(moduleNameForShortcuts, StringComparison.OrdinalIgnoreCase));

                    if (moduleForShortcuts == null)
                    {
                        Console.WriteLine("No module with that name found. Aborting.");
                        return;
                    }

                    if (moduleForShortcuts is not IPowerToysModuleShortcutsProvider shortcutsProvider)
                    {
                        Console.WriteLine("Module does not provide shortcuts. Aborting.");
                        return;
                    }

                    foreach (var shortcut in shortcutsProvider.Shortcuts)
                    {
                        Console.WriteLine("Shortcut: " + shortcut.Hotkey.ToString());
                    }

                    break;
                case TrayButton.DebugFullModuleReport:
                    AllocConsole();
                    static string GpoRuleToString(GpoRuleConfigured g) => g switch
                    {
                        GpoRuleConfigured.WrongValue => "Wrong value",
                        GpoRuleConfigured.Unavailable => "Unavailable",
                        GpoRuleConfigured.NotConfigured => "Not configured",
                        GpoRuleConfigured.Disabled => "Disabled",
                        GpoRuleConfigured.Enabled => "Enabled",
                        _ => "Unknown",
                    };

                    Console.WriteLine("=============================");
                    Console.WriteLine("=Full report of all modules:=");
                    Console.WriteLine("=============================");
                    foreach (var module in Runner.ModulesToLoad)
                    {
                        Console.WriteLine("Module name: " + module.Name);
                        Console.WriteLine("Enabled: " + module.Enabled);
                        Console.WriteLine("GPO configured: " + GpoRuleToString(module.GpoRuleConfigured));
                        if (module is ProcessModuleAbstractClass pmac)
                        {
                            Console.WriteLine("Process name: " + pmac.ProcessName);
                            Console.WriteLine("Process path: " + pmac.ProcessPath);
                            Console.WriteLine("Launch options: " + pmac.LaunchOptions);
                            Console.WriteLine("Launch arguments: " + pmac.ProcessArguments);
                            Console.WriteLine("Is running: " + pmac.IsProcessRunning());
                        }

                        if (module is IPowerToysModuleCustomActionsProvider ptmcap)
                        {
                            Console.WriteLine("Custom actions: " + string.Join(", ", ptmcap.CustomActions.Keys));
                        }

                        if (module is IPowerToysModuleShortcutsProvider ptmscp)
                        {
                            Console.WriteLine("Shortcuts: " + string.Join(", ", ptmscp.Shortcuts.Select(s => s.Hotkey.ToString())));
                        }

                        Console.WriteLine("Is subscribed to settings changes: " + (module is IPowerToysModuleSettingsChangedSubscriber));

                        Console.WriteLine("-----------------------------");
                    }

                    break;
#endif
            }
        }
    }
}
