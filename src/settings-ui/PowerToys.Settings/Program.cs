// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Windows;
using interop;
using ManagedCommon;

namespace PowerToys.Settings
{
    public static class Program
    {
        private enum Arguments
        {
            PTPipeName = 0,
            SettingsPipeName,
            PTPid,
            Theme, // used in the old settings
            ElevatedStatus,
            IsUserAdmin,
            ShowOobeWindow,
            SettingsWindow,
        }

        // Quantity of arguments
        private const int RequiredArgumentsQty = 7;
        private const int RequiredAndOptionalArgumentsQty = 8;

        // Create an instance of the  IPC wrapper.
        private static TwoWayPipeMessageIPCManaged ipcmanager;

        public static bool IsElevated { get; set; }

        public static bool IsUserAnAdmin { get; set; }

        public static int PowerToysPID { get; set; }

        public static Action<string> IPCMessageReceivedCallback { get; set; }

        [STAThread]
        public static void Main(string[] args)
        {
            using (new Microsoft.PowerToys.Settings.UI.App())
            {
                App app = new App();
                app.InitializeComponent();

                if (args != null && args.Length >= RequiredArgumentsQty)
                {
                    _ = int.TryParse(args[(int)Arguments.PTPid], out int powerToysPID);
                    PowerToysPID = powerToysPID;

                    IsElevated = args[(int)Arguments.ElevatedStatus] == "true";
                    IsUserAnAdmin = args[(int)Arguments.IsUserAdmin] == "true";
                    app.ShowOobe = args[(int)Arguments.ShowOobeWindow] == "true";

                    if (args.Length == RequiredAndOptionalArgumentsQty)
                    {
                        // open specific window
                        switch (args[(int)Arguments.SettingsWindow])
                        {
                            case "Overview": app.StartupPage = typeof(Microsoft.PowerToys.Settings.UI.Views.GeneralPage); break;
                            case "Awake": app.StartupPage = typeof(Microsoft.PowerToys.Settings.UI.Views.AwakePage); break;
                            case "ColorPicker": app.StartupPage = typeof(Microsoft.PowerToys.Settings.UI.Views.ColorPickerPage); break;
                            case "FancyZones": app.StartupPage = typeof(Microsoft.PowerToys.Settings.UI.Views.FancyZonesPage); break;
                            case "Run": app.StartupPage = typeof(Microsoft.PowerToys.Settings.UI.Views.PowerLauncherPage); break;
                            case "ImageResizer": app.StartupPage = typeof(Microsoft.PowerToys.Settings.UI.Views.ImageResizerPage); break;
                            case "KBM": app.StartupPage = typeof(Microsoft.PowerToys.Settings.UI.Views.KeyboardManagerPage); break;
                            case "MouseUtils": app.StartupPage = typeof(Microsoft.PowerToys.Settings.UI.Views.MouseUtilsPage); break;
                            case "PowerRename": app.StartupPage = typeof(Microsoft.PowerToys.Settings.UI.Views.PowerRenamePage); break;
                            case "FileExplorer": app.StartupPage = typeof(Microsoft.PowerToys.Settings.UI.Views.PowerPreviewPage); break;
                            case "ShortcutGuide": app.StartupPage = typeof(Microsoft.PowerToys.Settings.UI.Views.ShortcutGuidePage); break;
                            case "VideoConference": app.StartupPage = typeof(Microsoft.PowerToys.Settings.UI.Views.VideoConferencePage); break;
                            default: Debug.Assert(false, "Unexpected SettingsWindow argument value"); break;
                        }
                    }

                    RunnerHelper.WaitForPowerToysRunner(PowerToysPID, () =>
                    {
                        Environment.Exit(0);
                    });

                    ipcmanager = new TwoWayPipeMessageIPCManaged(args[(int)Arguments.SettingsPipeName], args[(int)Arguments.PTPipeName], (string message) =>
                    {
                        if (IPCMessageReceivedCallback != null && message.Length > 0)
                        {
                            Application.Current.Dispatcher.BeginInvoke(new System.Action(() =>
                            {
                                IPCMessageReceivedCallback(message);
                            }));
                        }
                    });
                    ipcmanager.Start();
                    app.Run();
                }
                else
                {
                    MessageBox.Show(
                        "The application cannot be run as a standalone process. Please start the application through the runner.",
                        "Forbidden",
                        MessageBoxButton.OK);
                    app.Shutdown();
                }
            }
        }

        public static TwoWayPipeMessageIPCManaged GetTwoWayIPCManager()
        {
            return ipcmanager;
        }
    }
}
