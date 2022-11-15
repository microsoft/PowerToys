// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class OobeShellPage : UserControl
    {
        public static Func<string> RunSharedEventCallback { get; set; }

        public static void SetRunSharedEventCallback(Func<string> implementation)
        {
            RunSharedEventCallback = implementation;
        }

        public static Func<string> ColorPickerSharedEventCallback { get; set; }

        public static void SetColorPickerSharedEventCallback(Func<string> implementation)
        {
            ColorPickerSharedEventCallback = implementation;
        }

        public static Action<Type> OpenMainWindowCallback { get; set; }

        public static void SetOpenMainWindowCallback(Action<Type> implementation)
        {
            OpenMainWindowCallback = implementation;
        }

        /// <summary>
        /// Gets view model.
        /// </summary>
        public OobeShellViewModel ViewModel { get; } = new OobeShellViewModel();

        /// <summary>
        /// Gets or sets a shell handler to be used to update contents of the shell dynamically from page within the frame.
        /// </summary>
        public static OobeShellPage OobeShellHandler { get; set; }

        public ObservableCollection<OobePowerToysModule> Modules { get; }

        public OobeShellPage()
        {
            InitializeComponent();

            DataContext = ViewModel;
            OobeShellHandler = this;
            UpdateUITheme();
            Modules = new ObservableCollection<OobePowerToysModule>();

            Modules.Insert((int)PowerToysModules.Overview, new OobePowerToysModule()
            {
                ModuleName = "Overview",
                IsNew = false,
            });
            Modules.Insert((int)PowerToysModules.AlwaysOnTop, new OobePowerToysModule()
            {
                ModuleName = "AlwaysOnTop",
                IsNew = false,
            });
            Modules.Insert((int)PowerToysModules.Awake, new OobePowerToysModule()
            {
                ModuleName = "Awake",
                IsNew = false,
            });
            Modules.Insert((int)PowerToysModules.ColorPicker, new OobePowerToysModule()
            {
                ModuleName = "ColorPicker",
                IsNew = false,
            });
            Modules.Insert((int)PowerToysModules.FancyZones, new OobePowerToysModule()
            {
                ModuleName = "FancyZones",
                IsNew = false,
            });
            Modules.Insert((int)PowerToysModules.FileLocksmith, new OobePowerToysModule()
            {
                ModuleName = "FileLocksmith",
                IsNew = true,
            });
            Modules.Insert((int)PowerToysModules.FileExplorer, new OobePowerToysModule()
            {
                ModuleName = "FileExplorer",
                IsNew = false,
            });
            Modules.Insert((int)PowerToysModules.ImageResizer, new OobePowerToysModule()
            {
                ModuleName = "ImageResizer",
                IsNew = false,
            });
            Modules.Insert((int)PowerToysModules.KBM, new OobePowerToysModule()
            {
                ModuleName = "KBM",
                IsNew = false,
            });
            Modules.Insert((int)PowerToysModules.MouseUtils, new OobePowerToysModule()
            {
                ModuleName = "MouseUtils",
                IsNew = false,
            });
            Modules.Insert((int)PowerToysModules.PowerRename, new OobePowerToysModule()
            {
                ModuleName = "PowerRename",
                IsNew = false,
            });
            Modules.Insert((int)PowerToysModules.Run, new OobePowerToysModule()
            {
                ModuleName = "Run",
                IsNew = false,
            });
            Modules.Insert((int)PowerToysModules.QuickAccent, new OobePowerToysModule()
            {
                ModuleName = "QuickAccent",
                IsNew = false,
            });
            Modules.Insert((int)PowerToysModules.ShortcutGuide, new OobePowerToysModule()
            {
                ModuleName = "ShortcutGuide",
                IsNew = false,
            });
            Modules.Insert((int)PowerToysModules.TextExtractor, new OobePowerToysModule()
            {
                ModuleName = "TextExtractor",
                IsNew = false,
            });

            Modules.Insert((int)PowerToysModules.VideoConference, new OobePowerToysModule()
            {
                ModuleName = "VideoConference",
                IsNew = false,
            });

            Modules.Insert((int)PowerToysModules.MeasureTool, new OobePowerToysModule()
            {
                ModuleName = "MeasureTool",
                IsNew = false,
            });

            Modules.Insert((int)PowerToysModules.Hosts, new OobePowerToysModule()
            {
                ModuleName = "Hosts",
                IsNew = true,
            });

            Modules.Insert((int)PowerToysModules.WhatsNew, new OobePowerToysModule()
            {
                ModuleName = "WhatsNew",
                IsNew = false,
            });
        }

        public void OnClosing()
        {
            Microsoft.UI.Xaml.Controls.NavigationViewItem selectedItem = this.NavigationView.SelectedItem as Microsoft.UI.Xaml.Controls.NavigationViewItem;
            if (selectedItem != null)
            {
                Modules[(int)(PowerToysModules)Enum.Parse(typeof(PowerToysModules), (string)selectedItem.Tag, true)].LogClosingModuleEvent();
            }
        }

        public void NavigateToModule(PowerToysModules selectedModule)
        {
            if (selectedModule == PowerToysModules.WhatsNew)
            {
                NavigationView.SelectedItem = NavigationView.FooterMenuItems[0];
            }
            else
            {
                NavigationView.SelectedItem = NavigationView.MenuItems[(int)selectedModule];
            }
        }

        private void NavigationView_SelectionChanged(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs args)
        {
            Microsoft.UI.Xaml.Controls.NavigationViewItem selectedItem = args.SelectedItem as Microsoft.UI.Xaml.Controls.NavigationViewItem;

            if (selectedItem != null)
            {
                switch (selectedItem.Tag)
                {
                    case "Overview": NavigationFrame.Navigate(typeof(OobeOverview)); break;
                    case "WhatsNew": NavigationFrame.Navigate(typeof(OobeWhatsNew)); break;
                    case "AlwaysOnTop": NavigationFrame.Navigate(typeof(OobeAlwaysOnTop)); break;
                    case "Awake": NavigationFrame.Navigate(typeof(OobeAwake)); break;
                    case "ColorPicker": NavigationFrame.Navigate(typeof(OobeColorPicker)); break;
                    case "FancyZones": NavigationFrame.Navigate(typeof(OobeFancyZones)); break;
                    case "FileLocksmith": NavigationFrame.Navigate(typeof(OobeFileLocksmith)); break;
                    case "Run": NavigationFrame.Navigate(typeof(OobeRun)); break;
                    case "ImageResizer": NavigationFrame.Navigate(typeof(OobeImageResizer)); break;
                    case "KBM": NavigationFrame.Navigate(typeof(OobeKBM)); break;
                    case "PowerRename": NavigationFrame.Navigate(typeof(OobePowerRename)); break;
                    case "QuickAccent": NavigationFrame.Navigate(typeof(OobePowerAccent)); break;
                    case "FileExplorer": NavigationFrame.Navigate(typeof(OobeFileExplorer)); break;
                    case "ShortcutGuide": NavigationFrame.Navigate(typeof(OobeShortcutGuide)); break;
                    case "TextExtractor": NavigationFrame.Navigate(typeof(OobePowerOCR)); break;
                    case "VideoConference": NavigationFrame.Navigate(typeof(OobeVideoConference)); break;
                    case "MouseUtils": NavigationFrame.Navigate(typeof(OobeMouseUtils)); break;
                    case "MeasureTool": NavigationFrame.Navigate(typeof(OobeMeasureTool)); break;
                    case "Hosts": NavigationFrame.Navigate(typeof(OobeHosts)); break;
                }
            }
        }

        public void UpdateUITheme()
        {
            switch (SettingsRepository<GeneralSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Theme.ToUpperInvariant())
            {
                case "LIGHT":
                    this.RequestedTheme = ElementTheme.Light;
                    break;
                case "DARK":
                    this.RequestedTheme = ElementTheme.Dark;
                    break;
                case "SYSTEM":
                    this.RequestedTheme = ElementTheme.Default;
                    break;
            }
        }
    }
}
