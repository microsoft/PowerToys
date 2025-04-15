// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RegistryPreview.Telemetry;
using RegistryPreviewUILib;
using Windows.Data.Json;
using Windows.Graphics;
using WinUIEx;

namespace RegistryPreview
{
    public sealed partial class MainWindow : WindowEx
    {
        // Const values
        private const string APPNAME = "RegistryPreview";

        // private members
        private Microsoft.UI.Windowing.AppWindow appWindow;
        private JsonObject jsonWindowPlacement;
        private string settingsFolder = string.Empty;
        private string windowPlacementFile = "app-placement.json";

        private RegistryPreviewMainPage MainPage { get; }

        internal MainWindow()
        {
            this.InitializeComponent();

            // Open settings file; this moved to after the window tweak because it gives the window time to start up
            settingsFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Microsoft\PowerToys\" + APPNAME;
            OpenWindowPlacementFile(settingsFolder, windowPlacementFile);

            // Update the Win32 looking window with the correct icon (and grab the appWindow handle for later)
            IntPtr windowHandle = this.GetWindowHandle();
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.SetIcon("Assets\\RegistryPreview\\RegistryPreview.ico");

            // TODO(stefan)
            appWindow.Closing += AppWindow_Closing;
            Activated += MainWindow_Activated;

            // Extend the canvas to include the title bar so the app can support theming
            ExtendsContentIntoTitleBar = true;
            WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(windowHandle);
            SetTitleBar(titleBar);

            // if have settings, update the location of the window
            if (jsonWindowPlacement != null)
            {
                // resize the window
                if (jsonWindowPlacement.ContainsKey("appWindow.Size.Width") && jsonWindowPlacement.ContainsKey("appWindow.Size.Height"))
                {
                    SizeInt32 size;
                    size.Width = (int)jsonWindowPlacement.GetNamedNumber("appWindow.Size.Width");
                    size.Height = (int)jsonWindowPlacement.GetNamedNumber("appWindow.Size.Height");

                    // check to make sure the size values are reasonable before attempting to restore the last saved size
                    if (size.Width >= 320 && size.Height >= 240)
                    {
                        appWindow.Resize(size);
                    }
                }

                // reposition the window
                if (jsonWindowPlacement.ContainsKey("appWindow.Position.X") && jsonWindowPlacement.ContainsKey("appWindow.Position.Y"))
                {
                    PointInt32 point;
                    point.X = (int)jsonWindowPlacement.GetNamedNumber("appWindow.Position.X");
                    point.Y = (int)jsonWindowPlacement.GetNamedNumber("appWindow.Position.Y");

                    // check to make sure the move values are reasonable before attempting to restore the last saved location
                    if (point.X >= 0 && point.Y >= 0)
                    {
                        appWindow.Move(point);
                    }
                }
            }

            MainPage = new RegistryPreviewMainPage(this, this.UpdateWindowTitle, App.AppFilename);

            WindowHelpers.BringToForeground(windowHandle);

            PowerToysTelemetry.Log.WriteEvent(new RegistryPreviewEditorStartFinishEvent() { TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                titleBarText.Foreground =
                    (SolidColorBrush)Application.Current.Resources["WindowCaptionForegroundDisabled"];
            }
            else
            {
                titleBarText.Foreground =
                    (SolidColorBrush)Application.Current.Resources["WindowCaptionForeground"];
            }
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            MainGrid.Children.Add(MainPage);
            Grid.SetRow(MainPage, 1);
        }

        public void UpdateWindowTitle(string title)
        {
            string filename = title;

            if (string.IsNullOrEmpty(filename))
            {
                titleBarText.Text = APPNAME;
                appWindow.Title = APPNAME;
            }
            else
            {
                string[] file = filename.Split('\\');
                if (file.Length > 0)
                {
                    titleBarText.Text = file[file.Length - 1] + " - " + APPNAME;
                }
                else
                {
                    titleBarText.Text = filename + " - " + APPNAME;
                }

                // Continue to update the window's title, after updating the custom title bar
                appWindow.Title = titleBarText.Text;
            }
        }
    }
}
