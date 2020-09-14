// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using Microsoft.PowerLauncher.Telemetry;
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
        private WindowsXamlHost windowsXamlHost;

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
            // Hook up x:Bind source.
            windowsXamlHost = sender as WindowsXamlHost;
            ShellPage shellPage = windowsXamlHost.GetUwpInternalObject() as ShellPage;

            if (shellPage != null)
            {
                // send IPC Message
                shellPage.SetDefaultSndMessageCallback(msg =>
                {
                    // IPC Manager is null when launching runner directly
                    Program.GetTwoWayIPCManager()?.Send(msg);
                });

                // send IPC Message
                shellPage.SetRestartAdminSndMessageCallback(msg =>
                {
                    Program.GetTwoWayIPCManager().Send(msg);
                    System.Windows.Application.Current.Shutdown(); // close application
                });

                // send IPC Message
                shellPage.SetCheckForUpdatesMessageCallback(msg =>
                {
                    Program.GetTwoWayIPCManager().Send(msg);
                });

                // receive IPC Message
                Program.IPCMessageReceivedCallback = (string msg) =>
                {
                    if (ShellPage.ShellHandler.IPCResponseHandleList != null)
                    {
                        try
                        {
                            JsonObject json = JsonObject.Parse(msg);
                            foreach (Action<JsonObject> handle in ShellPage.ShellHandler.IPCResponseHandleList)
                            {
                                handle(json);
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                };

                shellPage.SetElevationStatus(Program.IsElevated);
                shellPage.SetIsUserAnAdmin(Program.IsUserAnAdmin);
                shellPage.Refresh();
            }

            // If the window is open, explicity force it to be shown to solve the blank dialog issue https://github.com/microsoft/PowerToys/issues/3384
            if (isOpen)
            {
                Show();
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            isOpen = false;

            // If the window is closed while minimized, set the xaml island core window back to visible. Required to avoid process not terminating issue - https://github.com/microsoft/PowerToys/issues/4430
            if (WindowState == WindowState.Minimized && windowsXamlHost != null)
            {
                ShellPage shellPage = windowsXamlHost.GetUwpInternalObject() as ShellPage;
                shellPage.SetXamlIslandCoreWindowVisible();
            }
        }
    }
}
