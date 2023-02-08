// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using interop;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Telemetry.Events;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using Windows.UI.Popups;
using WinRT.Interop;

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

        // Quantity of arguments
        private const int RequiredArgumentsQty = 12;

        // Create an instance of the  IPC wrapper.
        private static TwoWayPipeMessageIPCManaged ipcmanager;

        public static bool IsElevated { get; set; }

        public static bool IsUserAnAdmin { get; set; }

        public static int PowerToysPID { get; set; }

        public bool ShowOobe { get; set; }

        public bool ShowFlyout { get; set; }

        public bool ShowScoobe { get; set; }

        public Type StartupPage { get; set; } = typeof(Views.GeneralPage);

        public static Action<string> IPCMessageReceivedCallback { get; set; }

        private static bool loggedImmersiveDarkException;

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Initializes the singleton application object. This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        public static void OpenSettingsWindow(Type type = null, bool ensurePageIsSelected = false)
        {
            if (settingsWindow == null)
            {
                settingsWindow = new MainWindow(IsDarkTheme());
                type = typeof(GeneralPage);
            }

            settingsWindow.Activate();
            if (type != null)
            {
                settingsWindow.NavigateToSection(type);
            }

            if (ensurePageIsSelected)
            {
                settingsWindow.EnsurePageIsSelected();
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

            var isDark = IsDarkTheme();

            if (cmdArgs != null && cmdArgs.Length >= RequiredArgumentsQty)
            {
                // Skip the first argument which is prepended when launched by explorer
                if (cmdArgs[0].EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase) && cmdArgs[1].EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase) && (cmdArgs.Length >= RequiredArgumentsQty + 1))
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
                int currentArgumentIndex = RequiredArgumentsQty;

                if (containsSettingsWindow)
                {
                    // open specific window
                    switch (cmdArgs[currentArgumentIndex])
                    {
                        case "Overview": StartupPage = typeof(Views.GeneralPage); break;
                        case "AlwaysOnTop": StartupPage = typeof(Views.AlwaysOnTopPage); break;
                        case "Awake": StartupPage = typeof(Views.AwakePage); break;
                        case "ColorPicker": StartupPage = typeof(Views.ColorPickerPage); break;
                        case "FancyZones": StartupPage = typeof(Views.FancyZonesPage); break;
                        case "FileLocksmith": StartupPage = typeof(Views.FileLocksmithPage); break;
                        case "Run": StartupPage = typeof(Views.PowerLauncherPage); break;
                        case "ImageResizer": StartupPage = typeof(Views.ImageResizerPage); break;
                        case "KBM": StartupPage = typeof(Views.KeyboardManagerPage); break;
                        case "MouseUtils": StartupPage = typeof(Views.MouseUtilsPage); break;
                        case "PowerRename": StartupPage = typeof(Views.PowerRenamePage); break;
                        case "QuickAccent": StartupPage = typeof(Views.PowerAccentPage); break;
                        case "FileExplorer": StartupPage = typeof(Views.PowerPreviewPage); break;
                        case "ShortcutGuide": StartupPage = typeof(Views.ShortcutGuidePage); break;
                        case "TextExtractor": StartupPage = typeof(Views.PowerOcrPage); break;
                        case "VideoConference": StartupPage = typeof(Views.VideoConferencePage); break;
                        case "MeasureTool": StartupPage = typeof(Views.MeasureToolPage); break;
                        case "Hosts": StartupPage = typeof(Views.HostsPage); break;
                        default: Debug.Assert(false, "Unexpected SettingsWindow argument value"); break;
                    }

                    currentArgumentIndex++;
                }

                int flyout_x = 0;
                int flyout_y = 0;
                if (containsFlyoutPosition)
                {
                    // get the flyout position arguments
                    int.TryParse(cmdArgs[currentArgumentIndex++], out flyout_x);
                    int.TryParse(cmdArgs[currentArgumentIndex++], out flyout_y);
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

                if (!ShowOobe && !ShowScoobe && !ShowFlyout)
                {
                    settingsWindow = new MainWindow(isDark);
                    settingsWindow.Activate();
                    settingsWindow.NavigateToSection(StartupPage);
                }
                else
                {
                    // Create the Settings window hidden so that it's fully initialized and
                    // it will be ready to receive the notification if the user opens
                    // the Settings from the tray icon.
                    settingsWindow = new MainWindow(isDark, true);

                    if (ShowOobe)
                    {
                        PowerToysTelemetry.Log.WriteEvent(new OobeStartedEvent());
                        OobeWindow oobeWindow = new OobeWindow(OOBE.Enums.PowerToysModules.Overview, isDark);
                        oobeWindow.Activate();
                        SetOobeWindow(oobeWindow);
                    }
                    else if (ShowScoobe)
                    {
                        PowerToysTelemetry.Log.WriteEvent(new ScoobeStartedEvent());
                        OobeWindow scoobeWindow = new OobeWindow(OOBE.Enums.PowerToysModules.WhatsNew, isDark);
                        scoobeWindow.Activate();
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
            else
            {
                // For debugging purposes
                // Window is also needed to show MessageDialog
                settingsWindow = new MainWindow(isDark);
                settingsWindow.Activate();

#if !DEBUG
                ShowMessageDialogAndExit("The application cannot be run as a standalone process. Please start the application through the runner.", "Forbidden");
#else
                ShowMessageDialog("The application cannot be run as a standalone process. Please start the application through the runner.", "Forbidden");
#endif
            }
        }

#if !DEBUG
        private async void ShowMessageDialogAndExit(string content, string title = null)
#else
        private async void ShowMessageDialog(string content, string title = null)
#endif
        {
            await ShowDialogAsync(content, title);
#if !DEBUG
            this.Exit();
#endif
        }

        public static Task<IUICommand> ShowDialogAsync(string content, string title = null)
        {
            var dialog = new MessageDialog(content, title ?? string.Empty);
            var handle = NativeMethods.GetActiveWindow();
            if (handle == IntPtr.Zero)
            {
                throw new InvalidOperationException();
            }

            InitializeWithWindow.Initialize(dialog, handle);
            return dialog.ShowAsync().AsTask<IUICommand>();
        }

        public static TwoWayPipeMessageIPCManaged GetTwoWayIPCManager()
        {
            return ipcmanager;
        }

        public static string SelectedTheme()
        {
            return SettingsRepository<GeneralSettings>.GetInstance(settingsUtils).SettingsConfig.Theme.ToUpper(CultureInfo.InvariantCulture);
        }

        public static bool IsDarkTheme()
        {
            var selectedTheme = SelectedTheme();
            return selectedTheme == "DARK" || (selectedTheme == "SYSTEM" && ThemeHelpers.GetAppTheme() == AppTheme.Dark);
        }

        public static void HandleThemeChange()
        {
            try
            {
                var isDark = IsDarkTheme();
                if (settingsWindow != null)
                {
                    var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(settingsWindow);
                    ThemeHelpers.SetImmersiveDarkMode(hWnd, isDark);
                }

                if (oobeWindow != null)
                {
                    var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(oobeWindow);
                    ThemeHelpers.SetImmersiveDarkMode(hWnd, isDark);
                }

                var selectedTheme = SelectedTheme();
                if (selectedTheme == "SYSTEM")
                {
                    themeListener = new ThemeListener();
                    themeListener.ThemeChanged += (_) => HandleThemeChange();
                }
                else if (themeListener != null)
                {
                    themeListener.Dispose();
                    themeListener = null;
                }
            }
            catch (Exception e)
            {
                if (!loggedImmersiveDarkException)
                {
                    Logger.LogError($"HandleThemeChange exception. Please install .NET 4.", e);
                    loggedImmersiveDarkException = true;
                }
            }
        }

        private static ISettingsUtils settingsUtils = new SettingsUtils();

        private static MainWindow settingsWindow;
        private static OobeWindow oobeWindow;
        private static FlyoutWindow flyoutWindow;
        private static ThemeListener themeListener;

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
    }
}
