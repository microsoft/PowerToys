// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace RegistryPreviewUILib
{
    public sealed partial class RegistryPreviewMainPage : Page
    {
        // Const values
        private const string REGISTRYHEADER4 = "regedit4";
        private const string REGISTRYHEADER5 = "windows registry editor version 5.00";
        private const string APPNAME = "RegistryPreview";
        private const string KEYIMAGE = "ms-appx:///Assets/RegistryPreview/folder32.png";
        private const string DELETEDKEYIMAGE = "ms-appx:///Assets/RegistryPreview/deleted-folder32.png";
        private const string ERRORIMAGE = "ms-appx:///Assets/RegistryPreview/error32.png";

        // private members
        private ResourceLoader resourceLoader;
        private bool visualTreeReady;
        private Dictionary<string, TreeViewNode> mapRegistryKeys;
        private List<RegistryValue> listRegistryValues;

        private UpdateWindowTitleFunction _updateWindowTitleFunction;
        private string _appFileName;
        private Window _mainWindow;

        public RegistryPreviewMainPage(Window mainWindow, UpdateWindowTitleFunction updateWindowTitleFunction, string appFilename)
        {
            // TODO (stefan): check ctor
            this.InitializeComponent();

            _mainWindow = mainWindow;
            _updateWindowTitleFunction = updateWindowTitleFunction;
            _appFileName = appFilename;

            _mainWindow.Closed += MainWindow_Closed;

            // Initialize the string table
            resourceLoader = ResourceLoaderInstance.ResourceLoader;

            // Update Toolbar
            if ((_appFileName == null) || (File.Exists(_appFileName) != true))
            {
                UpdateToolBarAndUI(false);
                _updateWindowTitleFunction(resourceLoader.GetString("FileNotFound"));
            }
        }
    }
}
