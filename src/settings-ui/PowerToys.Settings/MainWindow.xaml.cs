// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using Microsoft.PowerLauncher.Telemetry;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.PowerToys.Telemetry;
using Microsoft.Toolkit.Wpf.UI.XamlHost;
using PowerToys.Settings.Helpers;
using Windows.ApplicationModel.Resources;
using Windows.Data.Json;

namespace PowerToys.Settings
{
    // Interaction logic for MainWindow.xaml.
    public partial class MainWindow : Window
    {
        private static Window inst;

        private bool isOpen = true;

        public MainWindow()
        {
            var bootTime = new System.Diagnostics.Stopwatch();
            bootTime.Start();

            this.InitializeComponent();

            ResourceLoader loader = ResourceLoader.GetForViewIndependentUse();
            Title = loader.GetString("SettingsWindow_Title");

            bootTime.Stop();

            PowerToysTelemetry.Log.WriteEvent(new SettingsBootEvent() { BootTimeMs = bootTime.ElapsedMilliseconds });
        }

        public static void CloseHiddenWindow()
        {
            if (inst != null && inst.Visibility == Visibility.Hidden)
            {
                inst.Close();
            }
        }

        public void NavigateToSection(Type type)
        {
            if (inst != null)
            {
                Activate();
                ShellPage.Navigate(type);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            Utils.FitToScreen(this);

            var handle = new WindowInteropHelper(this).Handle;
            NativeMethods.GetWindowPlacement(handle, out var startupPlacement);
            var placement = Utils.DeserializePlacementOrDefault(handle);
            NativeMethods.SetWindowPlacement(handle, ref placement);

            var windowRect = new Rectangle((int)Left, (int)Top, (int)Width, (int)Height);
            var screenRect = new Rectangle((int)SystemParameters.VirtualScreenLeft, (int)SystemParameters.VirtualScreenTop, (int)SystemParameters.VirtualScreenWidth, (int)SystemParameters.VirtualScreenHeight);
            var intersection = Rectangle.Intersect(windowRect, screenRect);

            // Restore default position if 1/4 of width or height of the window is offscreen
            if (intersection.Width < (Width * 0.75) || intersection.Height < (Height * 0.75))
            {
                NativeMethods.SetWindowPlacement(handle, ref startupPlacement);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            var handle = new WindowInteropHelper(this).Handle;

            Utils.SerializePlacement(handle);
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
                    Program.GetTwoWayIPCManager()?.Send(msg);
                    isOpen = false;
                    System.Windows.Application.Current.Shutdown(); // close application
                });

                // send IPC Message
                ShellPage.SetCheckForUpdatesMessageCallback(msg =>
                {
                    Program.GetTwoWayIPCManager()?.Send(msg);
                });

                // open oobe
                ShellPage.SetOpenOobeCallback(() =>
                {
                    var oobe = new OobeWindow();
                    oobe.Show();
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
                try
                {
                    Show();
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (OobeWindow.IsOpened)
            {
                e.Cancel = true;
                ((Window)sender).Hide();
            }
            else
            {
                isOpen = false;
            }

            // XAML Islands: If the window is closed while minimized, exit the process. Required to avoid process not terminating issue - https://github.com/microsoft/PowerToys/issues/4430
            if (WindowState == WindowState.Minimized)
            {
                // Run Environment.Exit on a separate task to avoid performance impact
                System.Threading.Tasks.Task.Run(() => { Environment.Exit(0); });
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            inst = (Window)sender;
        }

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            if (((Window)sender).Visibility == Visibility.Hidden)
            {
                ((Window)sender).Visibility = Visibility.Visible;
            }
        }
    }
}
