using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources;
using Windows.Management.Core;
using Windows.Storage;

namespace RegistryPreview
{
    /// <summary>
    /// Class representing an each item in the tree view, each one a Registry Key; 
    /// FullPath is so we can re-select the node after a live update
    /// Tag is an Array of ListViewItems that below to each key
    /// </summary>   
    public class RegistryKey
    {
        public String Name { get; set; }

        public String FullPath { get; set; }

        public Object Tag { get; set; }

        public RegistryKey(String name, String fullPath)
        {
            this.Name = name;
            this.FullPath = fullPath;
        }
    }
    /// <summary>
    /// Class representing an each item in the list view, each one a Registry Value.
    /// </summary>   
    public class RegistryValue
    {
        public String Name { get; set; }
        public String Type { get; set; }
        public String Value { get; set; }
        public Uri ImageUri
        {
            // Based off the Type of the item, pass back the correct image Uri used by the Binding of the DataGrid
            get
            {
                switch (Type)
                {
                    case "REG_SZ":
                    case "REG_EXAND_SZ":
                    case "REG_MULTI_SZ":
                        return MainWindow.s_uriStringValue;

                }
                return MainWindow.s_uriBinaryValue;
            }
        }
        public RegistryValue(String name, String type, String value)
        {
            this.Name = name;
            this.Type = type;
            this.Value = value;
        }
    }

    public sealed partial class MainWindow : Window
    {
        // Const values
        private const String REGISTRY_HEADER_4 = "regedit4";
        private const String REGISTRY_HEADER_5 = "windows registry editor version 5.00";
        private const String APP_NAME = "Registry Preview";

        // Static members
        internal static Uri s_uriStringValue = new Uri("ms-appx:///Assets/string32.png");
        internal static Uri s_uriBinaryValue = new Uri("ms-appx:///Assets/data32.png");

        // private members
        private Microsoft.UI.Windowing.AppWindow m_appWindow;
        private ResourceLoader m_resourceLoader = null;
        private bool m_visualTreeReady = false;
        private Dictionary<String, TreeViewNode> m_mapRegistryKeys = null;
        private List<RegistryValue> m_listRegistryValues;
        private ApplicationDataContainer m_applicationDataContainer = null;
        private SolidColorBrush m_solidColorBrushNormal = null;
        private SolidColorBrush m_solidColorBrushReadOnly = null;

        internal MainWindow()
        {
            this.InitializeComponent();

            // Initialize the string table
            m_resourceLoader = ResourceLoader.GetForViewIndependentUse();

            // attempt to load the settings via the current AppContainer
            try
            {
                m_applicationDataContainer = ApplicationDataManager.CreateForPackageFamily(Package.Current.Id.FamilyName).LocalSettings;
            }
            catch
            {
                m_applicationDataContainer = ApplicationDataManager.CreateForPackageFamily("736d5a59-dea7-4177-9d37-57b41883614c_cs0kxz6c6q80t").LocalSettings;
            }

            // Attempts to force the visual tree to load faster
            this.Activate();

            // Update the Win32 looking window with the correct icon (and grab the appWindow handle for later)
            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            m_appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            m_appWindow.SetIcon("app.ico");
            m_appWindow.Closing += m_appWindow_Closing;

            // set up textBox's font colors
            m_solidColorBrushReadOnly = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 120, 120, 120));
            m_solidColorBrushNormal = new SolidColorBrush(Colors.Black);

            // Update Toolbar
            if ((App.s_Filename == null) || (File.Exists(App.s_Filename) != true))
            {
                UpdateToolBarAndUI(false);
                UpdateWindowTitle(m_resourceLoader.GetString("FileNotFound"));
                return;
            }
        }
    }
}
