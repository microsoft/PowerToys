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
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using RunnerV2.Helpers;
using RunnerV2.Models;
using RunnerV2.ModuleInterfaces;
using Update;
using static RunnerV2.NativeMethods;

namespace RunnerV2
{
    internal static partial class Runner
    {
        /// <summary>
        /// Gets the window handle for the Runner main window that hosts the tray icon and receives system messages.
        /// </summary>
        public static nint RunnerHwnd { get; private set; }

        public static bool DebbugingLogUncaughtExceptions { get; set; }

        public const string TrayWindowClassName = "pt_tray_icon_window_class";

        /// <summary>
        /// Gets all the currently loaded modules.
        /// </summary>
        public static List<IPowerToysModule> LoadedModules { get; } = [];

        private static List<IPowerToysModule> _failedModuleLoads = [];

        /// <summary>
        /// Gets the list of all available PowerToys modules.
        /// </summary>
        public static FrozenSet<IPowerToysModule> ModulesToLoad { get; } =
        [
            new ColorPickerModuleInterface(),
            new AlwaysOnTopModuleInterface(),
            new HostsModuleInterface(),
            new PowerAccentModuleInterface(),
            new AdvancedPasteModuleInterface(),
            new AwakeModuleInterface(),
            new CmdNotFoundModuleInterface(),
            new CommandPaletteModuleInterface(),
            new CropAndLockModuleInterface(),
            new EnvironmentVariablesModuleInterface(),
            new RegistryPreviewModuleInterface(),
            new FileExplorerModuleInterface(),
            new ZoomItModuleInterface(),
            new PowerOCRModuleInterface(),
            new MeasureToolModuleInterface(),
            new MouseJumpModuleInterface(),
            new FancyZonesModuleInterface(),
            new PowerToysRunModuleInterface(),
            new KeyboardManagerModuleInterface(),
            new LightSwitchModuleInterface(),
            new CursorWrapModuleInterface(),
            new FindMyMouseModuleInterface(),
            new WorkspacesModuleInterface(),
            new MousePointerCrosshairsModuleInterface(),
            new MouseHighlighterModuleInterface(),
            new MouseWithoutBordersModuleInterface(),
            new NewPlusModuleInterface(),
            new PowerRenameModuleInterface(),
            new ImageResizerModuleInterface(),
            new FileLocksmithModuleInterface(),
            new ShortcutGuideModuleInterface(),
            new PeekModuleInterface(),
            new PowerDisplayModuleInterface(),
        ];

        /// <summary>
        /// Runs the main message loop for Runner.
        /// </summary>
        /// <param name="afterInitializationAction">A function to execute after initialization.</param>
        internal static void Run(Action afterInitializationAction)
        {
            Logger.LogInfo("Runner started");

            InitializeTrayWindow();
            if (SettingsUtils.Default.GetSettings<GeneralSettings>().ShowSysTrayIcon)
            {
                TrayIconManager.StartTrayIcon();
            }

            if (SettingsUtils.Default.GetSettings<GeneralSettings>().EnableQuickAccess)
            {
                QuickAccessHelper.Start();
                CentralizedKeyboardHookManager.AddKeyboardHook("QuickAccess", SettingsUtils.Default.GetSettings<GeneralSettings>().QuickAccessShortcut, QuickAccessHelper.Show);
            }

            Task.Run(UpdateUtilities.UninstallPreviousMsixVersions);

            foreach (IPowerToysModule module in ModulesToLoad)
            {
                ToggleModuleStateBasedOnEnabledProperty(module);
                foreach ((string moduleName, var hotkeys) in CentralizedKeyboardHookManager.KeyboardHooks)
                {
                    HotkeyConflictsManager.RemoveHotkeysOfModule(moduleName);

                    foreach ((int i, (HotkeySettings hotkeySettings, Action _)) in hotkeys.Index())
                    {
                        HotkeyConflictsManager.AddHotkey(hotkeySettings, moduleName, i);
                    }
                }
            }

            Logger.InitializeLogger("\\RunnerLogs");

            CentralizedKeyboardHookManager.Start();

            afterInitializationAction();

            MessageLoop();
        }

        private static readonly uint _taskbarCreatedMessage = RegisterWindowMessageW("TaskbarCreated");

        /// <summary>
        /// The main message loop that processes Windows messages.
        /// </summary>
        [STAThread]
        private static void MessageLoop()
        {
            while (GetMessageW(out MSG msg, IntPtr.Zero, 0, 0) != 0 || true)
            {
                TranslateMessage(ref msg);
                try
                {
                    DispatchMessageW(ref msg);
                }
                catch (Exception e)
                {
                    Logger.LogError("Uncaught error in message loop: ", e);
                    if (DebbugingLogUncaughtExceptions)
                    {
                        Console.WriteLine("Uncaught error in message loop: " + e.Message + "\n" + e.StackTrace);
                    }
                }

                // Supress duplicate handling of HOTKEY messages
                if (msg.Message == (uint)WindowMessages.HOTKEY)
                {
                    continue;
                }

                try
                {
                    HandleMessage(msg.HWnd, msg.Message, (nint)msg.WParam, (nint)msg.LParam);
                }
                catch (Exception e)
                {
                    Logger.LogError("Uncaught error in message handling: ", e);
                    if (DebbugingLogUncaughtExceptions)
                    {
                        Console.WriteLine("Uncaught error in message loop: " + e.Message + "\n" + e.StackTrace);
                    }
                }
            }

            Close();
        }

        /// <summary>
        /// Closes Runner and all loaded modules.
        /// </summary>
        [DoesNotReturn]
        internal static void Close()
        {
            TrayIconManager.StopTrayIcon();
            SettingsHelper.CloseSettingsWindow();
            ElevationHelper.RestartIfScheudled();
            QuickAccessHelper.Stop();

            foreach (IPowerToysModule module in LoadedModules)
            {
                try
                {
                    module.Disable();

                    if (module is ProcessModuleAbstractClass pmac)
                    {
                        pmac.ProcessExit();
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show($"The module {module.Name} failed to unload: \n" + e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            Environment.Exit(0);
        }

        /// <summary>
        /// Toggles the state of a module based on its enabled property and GPO rules.
        /// </summary>
        /// <param name="module">The module to toggle</param>
        public static void ToggleModuleStateBasedOnEnabledProperty(IPowerToysModule module)
        {
            Logger.InitializeLogger("\\" + module.Name + "\\ModuleInterface\\Logs");
            if (_failedModuleLoads.Contains(module))
            {
                return;
            }

            try
            {
                if ((module.Enabled && (module.GpoRuleConfigured != PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)) || module.GpoRuleConfigured == PowerToys.GPOWrapper.GpoRuleConfigured.Enabled)
                {
                    if (!LoadedModules.Contains(module))
                    {
                        module.Enable();
                        if (module is ProcessModuleAbstractClass pmac)
                        {
                            pmac.LaunchProcess(true);
                        }

                        LoadedModules.Add(module);
                    }

                    CentralizedKeyboardHookManager.RemoveAllHooksFromModule(module.Name);

                    if (module is IPowerToysModuleShortcutsProvider shortcutsProvider)
                    {
                        foreach (var shortcut in shortcutsProvider.Shortcuts.ToArray())
                        {
                            CentralizedKeyboardHookManager.AddKeyboardHook(module.Name, shortcut.Hotkey, shortcut.Action);
                        }
                    }

                    return;
                }
            }
            catch (IOException)
            {
                return;
            }
            catch (Exception e)
            {
#if RELEASE
                MessageBox.Show($"The module {module.Name} failed to load: \n" + e.Message, "Error: " + e.GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
#endif
                Logger.LogError($"The module {module.Name} failed to load: \n", e);
                _failedModuleLoads.Add(module);
                return;
            }

            try
            {
                module.Disable();

                if (module is ProcessModuleAbstractClass pmac)
                {
                    pmac.ProcessExit();
                }

                CentralizedKeyboardHookManager.RemoveAllHooksFromModule(module.Name);

                LoadedModules.Remove(module);
            }
            catch (IOException)
            {
            }
            catch (Exception e)
            {
#if RELEASE
                MessageBox.Show($"The module {module.Name} failed to unload: \n" + e.Message, "Error: " + e.GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
#endif
                Logger.LogError($"The module {module.Name} failed to unload: \n", e);
                _failedModuleLoads.Add(module);
            }
        }

        /// <summary>
        /// Initializes the tray window to receive system messages.
        /// </summary>
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

        private static bool _trayMenuCommandProcessed;

        /// <summary>
        /// Handles Windows messages sent to the tray window.
        /// </summary>
        private static IntPtr HandleMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case (uint)WindowMessages.ICON_NOTIFY:
                    TrayIconManager.ProcessTrayIconMessage(lParam);
                    break;
                case (uint)WindowMessages.COMMAND:
                    if (_trayMenuCommandProcessed)
                    {
                        _trayMenuCommandProcessed = false;
                        break;
                    }

                    _trayMenuCommandProcessed = true;
                    TrayIconManager.ProcessTrayMenuCommand((nuint)wParam);
                    break;
                case (uint)WindowMessages.WINDOWPOSCHANGING:
                    if (SettingsUtils.Default.GetSettings<GeneralSettings>().ShowSysTrayIcon)
                    {
                        TrayIconManager.StartTrayIcon();
                    }

                    break;
                case (uint)WindowMessages.DESTROY:
                    Close();
                    break;
                case (uint)WindowMessages.REFRESH_SETTINGS:
                    foreach (IPowerToysModule module in ModulesToLoad)
                    {
                        ToggleModuleStateBasedOnEnabledProperty(module);
                    }

                    foreach ((string moduleName, var hotkeys) in CentralizedKeyboardHookManager.KeyboardHooks)
                    {
                        HotkeyConflictsManager.RemoveHotkeysOfModule(moduleName);

                        foreach ((int i, (HotkeySettings hotkeySettings, Action _)) in hotkeys.Index())
                        {
                            HotkeyConflictsManager.AddHotkey(hotkeySettings, moduleName, i);
                        }
                    }

                    Logger.InitializeLogger("\\RunnerLogs");

                    CentralizedKeyboardHookManager.RemoveAllHooksFromModule("QuickAccess");
                    if (SettingsUtils.Default.GetSettings<GeneralSettings>().EnableQuickAccess)
                    {
                        CentralizedKeyboardHookManager.AddKeyboardHook("QuickAccess", SettingsUtils.Default.GetSettings<GeneralSettings>().QuickAccessShortcut, QuickAccessHelper.Show);
                    }
                    else
                    {
                        CentralizedKeyboardHookManager.RemoveAllHooksFromModule("QuickAccess");
                        QuickAccessHelper.Stop();
                    }

                    TrayIconManager.UpdateTrayIcon();
                    TrayIconManager.RegenerateRightClickMenu();

                    if (SettingsUtils.Default.GetSettings<GeneralSettings>().ShowSysTrayIcon)
                    {
                        TrayIconManager.StartTrayIcon();
                    }
                    else
                    {
                        TrayIconManager.StopTrayIcon();
                    }

                    AutoStartHelper.SetAutoStartState(SettingsUtils.Default.GetSettings<GeneralSettings>().Startup);

                    break;
                default:
                    if (msg == _taskbarCreatedMessage && SettingsUtils.Default.GetSettings<GeneralSettings>().ShowSysTrayIcon)
                    {
                        TrayIconManager.StartTrayIcon();
                    }

                    break;
            }

            return DefWindowProcW(hWnd, msg, wParam, lParam);
        }
    }
}
