// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Telemetry.Events;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using PowerToys.Interop;
using Windows.UI.Popups;
using WinRT.Interop;
using WinUIEx;

namespace Microsoft.PowerToys.Settings.UI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private enum Arguments
        {
            PTPipeName = 1,
            SettingsPipeName,
            PTPid,
            Theme, // used in the old settings
            ElevatedStatus,
            IsUserAdmin,
            ShowOobeWindow,
            ShowScoobeWindow,
            ShowFlyout,
            ContainsSettingsWindow,
            ContainsFlyoutPosition,
        }

        private const int RequiredArgumentsSetSettingQty = 4;
        private const int RequiredArgumentsSetAdditionalSettingsQty = 4;
        private const int RequiredArgumentsGetSettingQty = 3;

        private const int RequiredArgumentsLaunchedFromRunnerQty = 12;

        // Create an instance of the  IPC wrapper.
        private static TwoWayPipeMessageIPCManaged ipcmanager;

        public static bool IsElevated { get; set; }

        public static bool IsUserAnAdmin { get; set; }

        public static int PowerToysPID { get; set; }

        public bool ShowOobe { get; set; }

        public bool ShowFlyout { get; set; }

        public bool ShowScoobe { get; set; }

        public Type StartupPage { get; set; } = typeof(Views.DashboardPage);

        public static Action<string> IPCMessageReceivedCallback { get; set; }

        public ETWTrace EtwTrace { get; private set; } = new ETWTrace();

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Initializes the singleton application object. This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            Logger.InitializeLogger(@"\Settings\Logs");

            string appLanguage = LanguageHelper.LoadLanguage();
            if (!string.IsNullOrEmpty(appLanguage))
            {
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = appLanguage;
            }

            InitializeComponent();

            UnhandledException += App_UnhandledException;

            NativeEventWaiter.WaitForEventLoop(
                Constants.PowerToysRunnerTerminateSettingsEvent(), () =>
            {
                EtwTrace?.Dispose();
                Environment.Exit(0);
            });
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.LogError("Unhandled exception", e.Exception);
        }

        public static void OpenSettingsWindow(Type type = null, bool ensurePageIsSelected = false)
        {
            if (settingsWindow == null)
            {
                settingsWindow = new MainWindow();
            }

            settingsWindow.Activate();

            if (type != null)
            {
                settingsWindow.NavigateToSection(type);

                WindowHelpers.BringToForeground(settingsWindow.GetWindowHandle());
            }

            if (ensurePageIsSelected)
            {
                settingsWindow.EnsurePageIsSelected();
            }
        }

        private void OnLaunchedToSetSetting(string[] cmdArgs)
        {
            var settingName = cmdArgs[2];
            var settingValue = cmdArgs[3];
            try
            {
                SetSettingCommandLineCommand.Execute(settingName, settingValue, new SettingsUtils());
            }
            catch (Exception ex)
            {
                Logger.LogError($"SetSettingCommandLineCommand exception: '{settingName}' setting couldn't be set to {settingValue}", ex);
            }

            Exit();
        }

        private void OnLaunchedToSetAdditionalSetting(string[] cmdArgs)
        {
            var moduleName = cmdArgs[2];
            var ipcFileName = cmdArgs[3];
            try
            {
                using (var settings = JsonDocument.Parse(File.ReadAllText(ipcFileName)))
                {
                    SetAdditionalSettingsCommandLineCommand.Execute(moduleName, settings, new SettingsUtils());
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"SetAdditionalSettingsCommandLineCommand exception: couldn't set additional settings for '{moduleName}'", ex);
            }

            Exit();
        }

        private void OnLaunchedToGetSetting(string[] cmdArgs)
        {
            var ipcFileName = cmdArgs[2];

            try
            {
                var requestedSettings = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(ipcFileName), SourceGenerationContextContext.Default.DictionaryStringListString);
                File.WriteAllText(ipcFileName, GetSettingCommandLineCommand.Execute(requestedSettings));
            }
            catch (Exception ex)
            {
                Logger.LogError($"GetSettingCommandLineCommand exception", ex);
            }

            Exit();
        }

        private void OnLaunchedFromRunner(string[] cmdArgs)
        {
            // Skip the first argument which is prepended when launched by explorer
            if (cmdArgs[0].EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase) && cmdArgs[1].EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase) && (cmdArgs.Length >= RequiredArgumentsLaunchedFromRunnerQty + 1))
            {
                cmdArgs = cmdArgs.Skip(1).ToArray();
            }

            _ = int.TryParse(cmdArgs[(int)Arguments.PTPid], out int powerToysPID);
            PowerToysPID = powerToysPID;

            IsElevated = cmdArgs[(int)Arguments.ElevatedStatus] == "true";
            IsUserAnAdmin = cmdArgs[(int)Arguments.IsUserAdmin] == "true";
            ShowOobe = cmdArgs[(int)Arguments.ShowOobeWindow] == "true";
            ShowScoobe = cmdArgs[(int)Arguments.ShowScoobeWindow] == "true";
            ShowFlyout = cmdArgs[(int)Arguments.ShowFlyout] == "true";
            bool containsSettingsWindow = cmdArgs[(int)Arguments.ContainsSettingsWindow] == "true";
            bool containsFlyoutPosition = cmdArgs[(int)Arguments.ContainsFlyoutPosition] == "true";

            // To keep track of variable arguments
            int currentArgumentIndex = RequiredArgumentsLaunchedFromRunnerQty;

            if (containsSettingsWindow)
            {
                // Open specific window
                StartupPage = GetPage(cmdArgs[currentArgumentIndex]);

                currentArgumentIndex++;
            }

            int flyout_x = 0;
            int flyout_y = 0;
            if (containsFlyoutPosition)
            {
                // get the flyout position arguments
                _ = int.TryParse(cmdArgs[currentArgumentIndex++], out flyout_x);
                _ = int.TryParse(cmdArgs[currentArgumentIndex++], out flyout_y);
            }

            RunnerHelper.WaitForPowerToysRunner(PowerToysPID, () =>
            {
                Environment.Exit(0);
            });

            ipcmanager = new TwoWayPipeMessageIPCManaged(cmdArgs[(int)Arguments.SettingsPipeName], cmdArgs[(int)Arguments.PTPipeName], (string message) =>
            {
                if (IPCMessageReceivedCallback != null && message.Length > 0)
                {
                    IPCMessageReceivedCallback(message);
                }
            });
            ipcmanager.Start();

            GlobalHotkeyConflictManager.Initialize(message =>
            {
                ipcmanager.Send(message);
                return 0;
            });

            if (!ShowOobe && !ShowScoobe && !ShowFlyout)
            {
                settingsWindow = new MainWindow();
                settingsWindow.Activate();
                settingsWindow.ExtendsContentIntoTitleBar = true;
                settingsWindow.NavigateToSection(StartupPage);

                // https://github.com/microsoft/microsoft-ui-xaml/issues/7595 - Activate doesn't bring window to the foreground
                // Need to call SetForegroundWindow to actually gain focus.
                WindowHelpers.BringToForeground(settingsWindow.GetWindowHandle());

                // https://github.com/microsoft/microsoft-ui-xaml/issues/8948 - A window's top border incorrectly
                // renders as black on Windows 10.
                WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(WindowNative.GetWindowHandle(settingsWindow));
            }
            else
            {
                // Create the Settings window hidden so that it's fully initialized and
                // it will be ready to receive the notification if the user opens
                // the Settings from the tray icon.
                settingsWindow = new MainWindow(true);

                if (ShowOobe)
                {
                    PowerToysTelemetry.Log.WriteEvent(new OobeStartedEvent());
                    OobeWindow oobeWindow = new OobeWindow(OOBE.Enums.PowerToysModules.Overview);
                    oobeWindow.Activate();
                    oobeWindow.ExtendsContentIntoTitleBar = true;
                    WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(WindowNative.GetWindowHandle(settingsWindow));
                    SetOobeWindow(oobeWindow);
                }
                else if (ShowScoobe)
                {
                    PowerToysTelemetry.Log.WriteEvent(new ScoobeStartedEvent());
                    OobeWindow scoobeWindow = new OobeWindow(OOBE.Enums.PowerToysModules.WhatsNew);
                    scoobeWindow.Activate();
                    scoobeWindow.ExtendsContentIntoTitleBar = true;
                    WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(WindowNative.GetWindowHandle(settingsWindow));
                    SetOobeWindow(scoobeWindow);
                }
                else if (ShowFlyout)
                {
                    POINT? p = null;
                    if (containsFlyoutPosition)
                    {
                        p = new POINT(flyout_x, flyout_y);
                    }

                    ShellPage.OpenFlyoutCallback(p);
                }
            }
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            var cmdArgs = Environment.GetCommandLineArgs();

            if (cmdArgs?.Length >= RequiredArgumentsLaunchedFromRunnerQty)
            {
                OnLaunchedFromRunner(cmdArgs);
            }
            else if (cmdArgs?.Length == RequiredArgumentsSetSettingQty && cmdArgs[1] == "set")
            {
                OnLaunchedToSetSetting(cmdArgs);
            }
            else if (cmdArgs?.Length == RequiredArgumentsSetAdditionalSettingsQty && cmdArgs[1] == "setAdditional")
            {
                OnLaunchedToSetAdditionalSetting(cmdArgs);
            }
            else if (cmdArgs?.Length == RequiredArgumentsGetSettingQty && cmdArgs[1] == "get")
            {
                OnLaunchedToGetSetting(cmdArgs);
            }
            else
            {
#if DEBUG
                // For debugging purposes
                // Window is also needed to show MessageDialog
                settingsWindow = new MainWindow();
                settingsWindow.ExtendsContentIntoTitleBar = true;
                WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(WindowNative.GetWindowHandle(settingsWindow));
                settingsWindow.Activate();
                settingsWindow.NavigateToSection(StartupPage);

                // In DEBUG mode, we might not have IPC set up, so provide a dummy implementation
                GlobalHotkeyConflictManager.Initialize(message =>
                {
                    // In debug mode, just log or do nothing
                    System.Diagnostics.Debug.WriteLine($"IPC Message: {message}");
                    return 0;
                });
#else
        /* If we try to run Settings as a standalone app, it will start PowerToys.exe if not running and open Settings again through it in the Dashboard page. */
        Common.UI.SettingsDeepLink.OpenSettings(Common.UI.SettingsDeepLink.SettingsWindow.Dashboard, true);
        Exit();
#endif
            }
        }

        public static TwoWayPipeMessageIPCManaged GetTwoWayIPCManager()
        {
            return ipcmanager;
        }

        public static bool IsDarkTheme()
        {
            return ThemeService.Theme == ElementTheme.Dark || (ThemeService.Theme == ElementTheme.Default && ThemeHelpers.GetAppTheme() == AppTheme.Dark);
        }

        public static int UpdateUIThemeMethod(string themeName)
        {
            return 0;
        }

        private static ISettingsUtils settingsUtils = new SettingsUtils();
        private static ThemeService themeService = new ThemeService(SettingsRepository<GeneralSettings>.GetInstance(settingsUtils));

        public static ThemeService ThemeService => themeService;

        private static MainWindow settingsWindow;
        private static OobeWindow oobeWindow;
        private static FlyoutWindow flyoutWindow;

        public static void ClearSettingsWindow()
        {
            settingsWindow = null;
        }

        public static MainWindow GetSettingsWindow()
        {
            return settingsWindow;
        }

        public static OobeWindow GetOobeWindow()
        {
            return oobeWindow;
        }

        public static FlyoutWindow GetFlyoutWindow()
        {
            return flyoutWindow;
        }

        public static void SetOobeWindow(OobeWindow window)
        {
            oobeWindow = window;
        }

        public static void SetFlyoutWindow(FlyoutWindow window)
        {
            flyoutWindow = window;
        }

        public static void ClearOobeWindow()
        {
            oobeWindow = null;
        }

        public static void ClearFlyoutWindow()
        {
            flyoutWindow = null;
        }

        public static Type GetPage(string settingWindow)
        {
            switch (settingWindow)
            {
                case "Dashboard": return typeof(DashboardPage);
                case "Overview": return typeof(GeneralPage);
                case "AdvancedPaste": return typeof(AdvancedPastePage);
                case "AlwaysOnTop": return typeof(AlwaysOnTopPage);
                case "Awake": return typeof(AwakePage);
                case "CmdNotFound": return typeof(CmdNotFoundPage);
                case "ColorPicker": return typeof(ColorPickerPage);
                case "LightSwitch": return typeof(LightSwitchPage);
                case "FancyZones": return typeof(FancyZonesPage);
                case "FileLocksmith": return typeof(FileLocksmithPage);
                case "Run": return typeof(PowerLauncherPage);
                case "ImageResizer": return typeof(ImageResizerPage);
                case "KBM": return typeof(KeyboardManagerPage);
                case "MouseUtils": return typeof(MouseUtilsPage);
                case "MouseWithoutBorders": return typeof(MouseWithoutBordersPage);
                case "Peek": return typeof(PeekPage);
                case "PowerAccent": return typeof(PowerAccentPage);
                case "PowerLauncher": return typeof(PowerLauncherPage);
                case "PowerPreview": return typeof(PowerPreviewPage);
                case "PowerRename": return typeof(PowerRenamePage);
                case "QuickAccent": return typeof(PowerAccentPage);
                case "FileExplorer": return typeof(PowerPreviewPage);
                case "ShortcutGuide": return typeof(ShortcutGuidePage);
                case "PowerOcr": return typeof(PowerOcrPage);
                case "MeasureTool": return typeof(MeasureToolPage);
                case "Hosts": return typeof(HostsPage);
                case "RegistryPreview": return typeof(RegistryPreviewPage);
                case "CropAndLock": return typeof(CropAndLockPage);
                case "EnvironmentVariables": return typeof(EnvironmentVariablesPage);
                case "NewPlus": return typeof(NewPlusPage);
                case "Workspaces": return typeof(WorkspacesPage);
                case "CmdPal": return typeof(CmdPalPage);
                case "ZoomIt": return typeof(ZoomItPage);
                default:
                    // Fallback to Dashboard
                    Debug.Assert(false, "Unexpected SettingsWindow argument value");
                    return typeof(DashboardPage);
            }
        }
    }
}
