// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using Microsoft.PowerLauncher.Telemetry;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.PowerToys.Telemetry;
using Microsoft.Toolkit.Wpf.UI.XamlHost;
using Windows.Data.Json;

namespace Microsoft.PowerToys.Settings.UI.Runner
{
    // Interaction logic for MainWindow.xaml.
    public partial class MainWindow : Window
    {
        private bool isOpen = true;

        public MainWindow()
        {
            var bootTime = new System.Diagnostics.Stopwatch();
            bootTime.Start();

            this.InitializeComponent();
            bootTime.Stop();

            PowerToysTelemetry.Log.WriteEvent(new SettingsBootEvent() { BootTimeMs = bootTime.ElapsedMilliseconds });
        }

        private void WindowsXamlHost_ChildChanged(object sender, EventArgs e)
        {
            // If sender is null, it could lead to a NullReferenceException. This might occur on restarting as admin (check https://github.com/microsoft/PowerToys/issues/7393 for details)
            if (sender == null)
            {
                return;
            }

            // Hook up x:Bind source.
            WindowsXamlHost windowsXamlHost = sender as WindowsXamlHost;
            ShellPage shellPage = windowsXamlHost.GetUwpInternalObject() as ShellPage;

            if (shellPage != null)
            {
                // send IPC Message
                ShellPage.SetDefaultSndMessageCallback(msg =>
                {
                    // IPC Manager is null when launching runner directly
                    Program.GetTwoWayIPCManager()?.Send(msg);
                });

                // send IPC Message
                ShellPage.SetRestartAdminSndMessageCallback(msg =>
                {
                    Program.GetTwoWayIPCManager().Send(msg);
                    System.Windows.Application.Current.Shutdown(); // close application
                });

                // send IPC Message
                ShellPage.SetCheckForUpdatesMessageCallback(msg =>
                {
                    Program.GetTwoWayIPCManager().Send(msg);
                });

                // receive IPC Message
                Program.IPCMessageReceivedCallback = (string msg) =>
                {
                    if (ShellPage.ShellHandler.IPCResponseHandleList != null)
                    {
                        var success = JsonObject.TryParse(msg, out JsonObject json);
                        if (success)
                        {
                            foreach (Action<JsonObject> handle in ShellPage.ShellHandler.IPCResponseHandleList)
                            {
                                handle(json);
                            }
                        }
                        else
                        {
                            Logger.LogError("Failed to parse JSON from IPC message.");
                        }
                    }
                };

                ShellPage.SetElevationStatus(Program.IsElevated);
                ShellPage.SetIsUserAnAdmin(Program.IsUserAnAdmin);
                shellPage.Refresh();
            }

            // XAML Islands: If the window is open, explicitly force it to be shown to solve the blank dialog issue https://github.com/microsoft/PowerToys/issues/3384
            if (isOpen)
            {
                Show();
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            isOpen = false;

            // XAML Islands: If the window is closed while minimized, exit the process. Required to avoid process not terminating issue - https://github.com/microsoft/PowerToys/issues/4430
            if (WindowState == WindowState.Minimized)
            {
                // Run Environment.Exit on a separate task to avoid performance impact
                System.Threading.Tasks.Task.Run(() => { Environment.Exit(0); });
            }
        }
    }
}
