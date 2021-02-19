// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
        }

        // Quantity of arguments
        private const int ArgumentsQty = 6;

        // Create an instance of the  IPC wrapper.
        private static TwoWayPipeMessageIPCManaged ipcmanager;

        public static bool IsElevated { get; set; }

        public static bool IsUserAnAdmin { get; set; }

        public static int PowerToysPID { get; set; }

        public static Action<string> IPCMessageReceivedCallback { get; set; }

        [STAThread]
        public static void Main(string[] args)
        {
            using (var xamlApp = new Microsoft.PowerToys.Settings.UI.App())
            {
                App app = new App();
                app.InitializeComponent();

                if (args != null && args.Length >= ArgumentsQty)
                {
                    _ = int.TryParse(args[(int)Arguments.PTPid], out int powerToysPID);
                    PowerToysPID = powerToysPID;

                    IsElevated = args[(int)Arguments.ElevatedStatus] == "true";
                    IsUserAnAdmin = args[(int)Arguments.IsUserAdmin] == "true";

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

                    app.Exit += (object sender, ExitEventArgs e) =>
                    {
                        xamlApp.HostAppExit();
                    };

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
