// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Data.Json;
using Windows.Graphics;
using WinUIEx;

namespace RegistryPreview
{
    public sealed partial class MainWindow : WindowEx
    {
        // Const values
        private const string REGISTRYHEADER4 = "regedit4";
        private const string REGISTRYHEADER5 = "windows registry editor version 5.00";
        private const string APPNAME = "RegistryPreview";
        private const string KEYIMAGE = "ms-appx:///Assets/RegistryPreview/folder32.png";
        private const string DELETEDKEYIMAGE = "ms-appx:///Assets/RegistryPreview/deleted-folder32.png";
        private const string ERRORIMAGE = "ms-appx:///Assets/RegistryPreview/error32.png";

        // private members
        private Microsoft.UI.Windowing.AppWindow appWindow;
        private ResourceLoader resourceLoader;
        private bool visualTreeReady;
        private Dictionary<string, TreeViewNode> mapRegistryKeys;
        private List<RegistryValue> listRegistryValues;
        private JsonObject jsonWindowPlacement;
        private string settingsFolder = string.Empty;
        private string windowPlacementFile = "app-placement.json";

        internal MainWindow()
        {
            this.InitializeComponent();

            // Initialize the string table
            resourceLoader = ResourceLoaderInstance.ResourceLoader;

            // Open settings file; this moved to after the window tweak because it gives the window time to start up
            settingsFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Microsoft\PowerToys\" + APPNAME;
            OpenWindowPlacementFile(settingsFolder, windowPlacementFile);

            // Update the Win32 looking window with the correct icon (and grab the appWindow handle for later)
            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.SetIcon("Assets\\RegistryPreview\\app.ico");
            appWindow.Closing += AppWindow_Closing;
            Activated += MainWindow_Activated;

            // Extend the canvas to include the title bar so the app can support theming
            ExtendsContentIntoTitleBar = true;
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

            // Update Toolbar
            if ((App.AppFilename == null) || (File.Exists(App.AppFilename) != true))
            {
                UpdateToolBarAndUI(false);
                UpdateWindowTitle(resourceLoader.GetString("FileNotFound"));
            }

            ManagedCommon.WindowHelpers.BringToForeground(windowHandle);
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                titleBarText.Foreground =
                    (SolidColorBrush)App.Current.Resources["WindowCaptionForegroundDisabled"];
            }
            else
            {
                titleBarText.Foreground =
                    (SolidColorBrush)App.Current.Resources["WindowCaptionForeground"];
            }
        }
    }
}
