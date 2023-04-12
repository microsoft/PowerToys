// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Resources;
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
        private const string APPNAME = "Registry Preview";
        private const string KEYIMAGE = "ms-appx:///Assets/folder32.png";
        private const string DELETEDKEYIMAGE = "ms-appx:///Assets/deleted-folder32.png";

        // private members
        private Microsoft.UI.Windowing.AppWindow appWindow;
        private ResourceLoader resourceLoader;
        private bool visualTreeReady;
        private Dictionary<string, TreeViewNode> mapRegistryKeys;
        private List<RegistryValue> listRegistryValues;
        private JsonObject jsonSettings;
        private string settingsFolder = string.Empty;
        private string settingsFile = string.Empty;

        internal MainWindow()
        {
            this.InitializeComponent();

            // Initialize the string table
            resourceLoader = ResourceLoader.GetForViewIndependentUse();

            // Open settings file; this moved to after the window tweak because it gives the window time to start up
            settingsFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Microsoft\PowerToys\" + APPNAME;
            settingsFile = APPNAME + "_settings.json";
            OpenSettingsFile(settingsFolder, settingsFile);

            // Update the Win32 looking window with the correct icon (and grab the appWindow handle for later)
            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.SetIcon("app.ico");
            appWindow.Closing += AppWindow_Closing;

            // if have settings, update the location of the window
            if (jsonSettings != null)
            {
                // resize the window
                if (jsonSettings.ContainsKey("appWindow.Size.Width") && jsonSettings.ContainsKey("appWindow.Size.Height"))
                {
                    SizeInt32 size;
                    size.Width = (int)jsonSettings.GetNamedNumber("appWindow.Size.Width");
                    size.Height = (int)jsonSettings.GetNamedNumber("appWindow.Size.Height");

                    // check to make sure the size values are reasonable before attempting to restore the last saved size
                    if (size.Width >= 320 && size.Height >= 240)
                    {
                        appWindow.Resize(size);
                    }
                }

                // reposition the window
                if (jsonSettings.ContainsKey("appWindow.Position.X") && jsonSettings.ContainsKey("appWindow.Position.Y"))
                {
                    PointInt32 point;
                    point.X = (int)jsonSettings.GetNamedNumber("appWindow.Position.X");
                    point.Y = (int)jsonSettings.GetNamedNumber("appWindow.Position.Y");

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
        }
    }
}
