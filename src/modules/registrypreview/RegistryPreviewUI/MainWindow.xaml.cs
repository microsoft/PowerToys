// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources;
using Windows.Management.Core;
using Windows.Storage;

namespace RegistryPreview
{
    public sealed partial class MainWindow : Window
    {
        // Const values
        private const string REGISTRYHEADER4 = "regedit4";
        private const string REGISTRYHEADER5 = "windows registry editor version 5.00";
        private const string APPNAME = "Registry Preview";

        // private members
        private Microsoft.UI.Windowing.AppWindow appWindow;
        private ResourceLoader resourceLoader = null;
        private bool visualTreeReady = false;
        private Dictionary<string, TreeViewNode> mapRegistryKeys = null;
        private List<RegistryValue> listRegistryValues;
        private ApplicationDataContainer applicationDataContainer = null;
        private SolidColorBrush solidColorBrushNormal = null;
        private SolidColorBrush solidColorBrushReadOnly = null;

        internal MainWindow()
        {
            this.InitializeComponent();

            // Initialize the string table
            resourceLoader = ResourceLoader.GetForViewIndependentUse();

            applicationDataContainer = null;

            // TODO: replace with working settings.
            // attempt to load the settings via the current AppContainer
            // try
            // {
            //     applicationDataContainer = ApplicationDataManager.CreateForPackageFamily(Package.Current.Id.FamilyName).LocalSettings;
            // }
            // catch
            // {
            //     applicationDataContainer = ApplicationDataManager.CreateForPackageFamily("736d5a59-dea7-4177-9d37-57b41883614c_cs0kxz6c6q80t").LocalSettings;
            // }

            // Attempts to force the visual tree to load faster
            this.Activate();

            // Update the Win32 looking window with the correct icon (and grab the appWindow handle for later)
            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.SetIcon("app.ico");
            appWindow.Closing += AppWindow_Closing;

            // set up textBox's font colors
            solidColorBrushReadOnly = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 120, 120, 120));
            solidColorBrushNormal = new SolidColorBrush(Colors.Black);

            // Update Toolbar
            if ((App.AppFilename == null) || (File.Exists(App.AppFilename) != true))
            {
                UpdateToolBarAndUI(false);
                UpdateWindowTitle(resourceLoader.GetString("FileNotFound"));
                return;
            }
        }
    }
}
