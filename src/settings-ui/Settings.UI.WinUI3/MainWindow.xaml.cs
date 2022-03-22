using Microsoft.PowerLauncher.Telemetry;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.WinUI3.Views;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using System;
using Windows.ApplicationModel.Resources;
using Windows.Data.Json;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Microsoft.PowerToys.Settings.UI.WinUI3
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private static Window inst;

/*        private bool isOpen = true;
*/
        public MainWindow()
        {
            var bootTime = new System.Diagnostics.Stopwatch();
            bootTime.Start();

            this.InitializeComponent();

/*            
 *            todo(Stefan): Is needed?
 *            Utils.FitToScreen(this);
*/
            ResourceLoader loader = ResourceLoader.GetForViewIndependentUse();
            Title = loader.GetString("SettingsWindow_Title");

            // send IPC Message
            ShellPage.SetDefaultSndMessageCallback(msg =>
            {
                // IPC Manager is null when launching runner directly
                App.GetTwoWayIPCManager()?.Send(msg);
            });

            // send IPC Message
            ShellPage.SetRestartAdminSndMessageCallback(msg =>
            {
                App.GetTwoWayIPCManager()?.Send(msg);
/*                isOpen = false;
*/                Environment.Exit(0); // close application
            });

            // send IPC Message
            ShellPage.SetCheckForUpdatesMessageCallback(msg =>
            {
                App.GetTwoWayIPCManager()?.Send(msg);
            });

            // open oobe
            ShellPage.SetOpenOobeCallback(() =>
            {
                var oobe = new OobeWindow(Microsoft.PowerToys.Settings.UI.WinUI3.OOBE.Enums.PowerToysModulesEnum.Overview);
                oobe.Activate();
            });

            // receive IPC Message
            App.IPCMessageReceivedCallback = (string msg) =>
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

            ShellPage.SetElevationStatus(App.IsElevated);
            ShellPage.SetIsUserAnAdmin(App.IsUserAnAdmin);

            bootTime.Stop();

            PowerToysTelemetry.Log.WriteEvent(new SettingsBootEvent() { BootTimeMs = bootTime.ElapsedMilliseconds });

        }

        public static void CloseHiddenWindow()
        {
            if (inst != null && inst.Visible == false)
            {
                inst.Close();
            }
        }

        public void NavigateToSection(System.Type type)
        {
            Activate();
            ShellPage.Navigate(type);
        }

        /*        
                todo(Stefan): Is needed?XAML ISLAND RELATED STUFF!!!

                 protected override void OnSourceInitialized(EventArgs e)
                {
                    base.OnSourceInitialized(e);

                    var handle = new WindowInteropHelper(this).Handle;
                    NativeMethods.GetWindowPlacement(handle, out var startupPlacement);
                    var placement = Utils.DeserializePlacementOrDefault(handle);
                    NativeMethods.SetWindowPlacement(handle, ref placement);

                    var windowRect = new Rectangle((int)Left, (int)Top, (int)Width, (int)Height);
                    var screenRect = new Rectangle((int)SystemParameters.VirtualScreenLeft, (int)SystemParameters.VirtualScreenTop, (int)SystemParameters.VirtualScreenWidth, (int)SystemParameters.VirtualScreenHeight);
                    var intersection = Rectangle.Intersect(windowRect, screenRect);

                    // Restore default position if 5% of width or height of the window is offscreen
                    if (intersection.Width < (Width * 0.95) || intersection.Height < (Height * 0.95))
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
        */

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (OobeWindow.IsOpened)
            {
                e.Cancel = true;
                ((Window)sender).Close();
            }
            else
            {
/*                isOpen = false;
*/            }

            /*            todo(Stefan): Is needed?// XAML Islands: If the window is closed while minimized, exit the process. Required to avoid process not terminating issue - https://github.com/microsoft/PowerToys/issues/4430
                        if (WindowState == WindowState.Minimized)
                        {
                            // Run Environment.Exit on a separate task to avoid performance impact
                            System.Threading.Tasks.Task.Run(() => { Environment.Exit(0); });
                        }
            */
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            inst = (Window)sender;
        }

        private void MainWindow_Activated(object sender, RoutedEventArgs e)
        {
            if (((Window)sender).Visible == false)
            {
                ((Window)sender).Activate();
            }
        }
    }
}
