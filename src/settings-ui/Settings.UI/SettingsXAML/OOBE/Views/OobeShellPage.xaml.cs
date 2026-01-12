// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class OobeShellPage : Page
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

        private static SettingsUtils settingsUtils = SettingsUtils.Default;

        /* NOTE: Experimentation for OOBE is currently turned off on server side. Keeping this code in a comment to allow future experiments.
          private bool ExperimentationToggleSwitchEnabled { get; set; } = true;
        */

        public OobeShellPage()
        {
            InitializeComponent();

            // NOTE: Experimentation for OOBE is currently turned off on server side. Keeping this code in a comment to allow future experiments.
            // ExperimentationToggleSwitchEnabled = SettingsRepository<GeneralSettings>.GetInstance(settingsUtils).SettingsConfig.EnableExperimentation;
            DataContext = ViewModel;
            OobeShellHandler = this;
            Modules = new ObservableCollection<OobePowerToysModule>();

            Modules.Insert((int)PowerToysModules.Overview, new OobePowerToysModule()
            {
                ModuleName = "Overview",
                IsNew = false,
            });
            Modules.Insert((int)PowerToysModules.AdvancedPaste, new OobePowerToysModule()
            {
                ModuleName = "AdvancedPaste",
                IsNew = true,
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
            Modules.Insert((int)PowerToysModules.CmdNotFound, new OobePowerToysModule()
            {
                ModuleName = "CmdNotFound",
                IsNew = false,
            });
            Modules.Insert((int)PowerToysModules.CmdPal, new OobePowerToysModule()
            {
                ModuleName = "CmdPal",
                IsNew = true,
            });
            Modules.Insert((int)PowerToysModules.ColorPicker, new OobePowerToysModule()
            {
                ModuleName = "ColorPicker",
                IsNew = false,
            });
            Modules.Insert((int)PowerToysModules.CropAndLock, new OobePowerToysModule()
            {
                ModuleName = "CropAndLock",
                IsNew = false,
            });
            Modules.Insert((int)PowerToysModules.EnvironmentVariables, new OobePowerToysModule()
            {
                ModuleName = "EnvironmentVariables",
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
                IsNew = false,
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
            Modules.Insert((int)PowerToysModules.LightSwitch, new OobePowerToysModule()
            {
                ModuleName = "LightSwitch",
                IsNew = true,
            });
            Modules.Insert((int)PowerToysModules.MouseUtils, new OobePowerToysModule()
            {
                ModuleName = "MouseUtils",
                IsNew = false,
            });
            Modules.Insert((int)PowerToysModules.MouseWithoutBorders, new OobePowerToysModule()
            {
                ModuleName = "MouseWithoutBorders",
                IsNew = false,
            });
            Modules.Insert((int)PowerToysModules.Peek, new OobePowerToysModule()
            {
                ModuleName = "Peek",
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

            Modules.Insert((int)PowerToysModules.MeasureTool, new OobePowerToysModule()
            {
                ModuleName = "MeasureTool",
                IsNew = false,
            });

            Modules.Insert((int)PowerToysModules.Hosts, new OobePowerToysModule()
            {
                ModuleName = "Hosts",
                IsNew = false,
            });

            Modules.Insert((int)PowerToysModules.Workspaces, new OobePowerToysModule()
            {
                ModuleName = "Workspaces",
                IsNew = true,
            });

            Modules.Insert((int)PowerToysModules.RegistryPreview, new OobePowerToysModule()
            {
                ModuleName = "RegistryPreview",
                IsNew = false,
            });

            Modules.Insert((int)PowerToysModules.NewPlus, new OobePowerToysModule()
            {
                ModuleName = "NewPlus",
                IsNew = true,
            });

            Modules.Insert((int)PowerToysModules.ZoomIt, new OobePowerToysModule()
            {
                ModuleName = "ZoomIt",
                IsNew = true,
            });
        }

        public void OnClosing()
        {
            NavigationViewItem selectedItem = this.navigationView.SelectedItem as NavigationViewItem;
            if (selectedItem != null)
            {
                Modules[(int)(PowerToysModules)Enum.Parse(typeof(PowerToysModules), (string)selectedItem.Tag, true)].LogClosingModuleEvent();
            }
        }

        public void NavigateToModule(PowerToysModules selectedModule)
        {
            navigationView.SelectedItem = navigationView.MenuItems[(int)selectedModule];
        }

        private static void OpenScoobeWindow()
        {
            if (App.GetScoobeWindow() == null)
            {
                App.SetScoobeWindow(new ScoobeWindow());
            }

            App.GetScoobeWindow().Activate();
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            NavigationViewItem selectedItem = args.SelectedItem as NavigationViewItem;

            if (selectedItem != null)
            {
                switch (selectedItem.Tag)
                {
                    case "Overview": NavigationFrame.Navigate(typeof(OobeOverview)); break;
                    /* NOTE: Experimentation for OOBE is currently turned off on server side. Keeping this code in a comment to allow future experiments.
                        if (ExperimentationToggleSwitchEnabled && GPOWrapper.GetAllowExperimentationValue() != GpoRuleConfigured.Disabled)
                        {
                            switch (AllExperiments.Experiments.LandingPageExperiment)
                            {
                                case Experiments.ExperimentState.Enabled:
                                    NavigationFrame.Navigate(typeof(OobeOverviewAlternate)); break;
                                case Experiments.ExperimentState.Disabled:
                                    NavigationFrame.Navigate(typeof(OobeOverview)); break;
                                case Experiments.ExperimentState.NotLoaded:
                                    NavigationFrame.Navigate(typeof(OobeOverviewPlaceholder)); break;
                            }

                            break;
                        }
                        else
                        {
                            NavigationFrame.Navigate(typeof(OobeOverview));
                            break;
                        }
                    */

                    case "AdvancedPaste": NavigationFrame.Navigate(typeof(OobeAdvancedPaste)); break;
                    case "AlwaysOnTop": NavigationFrame.Navigate(typeof(OobeAlwaysOnTop)); break;
                    case "Awake": NavigationFrame.Navigate(typeof(OobeAwake)); break;
                    case "CmdNotFound": NavigationFrame.Navigate(typeof(OobeCmdNotFound)); break;
                    case "CmdPal": NavigationFrame.Navigate(typeof(OobeCmdPal)); break;
                    case "ColorPicker": NavigationFrame.Navigate(typeof(OobeColorPicker)); break;
                    case "CropAndLock": NavigationFrame.Navigate(typeof(OobeCropAndLock)); break;
                    case "EnvironmentVariables": NavigationFrame.Navigate(typeof(OobeEnvironmentVariables)); break;
                    case "FancyZones": NavigationFrame.Navigate(typeof(OobeFancyZones)); break;
                    case "FileLocksmith": NavigationFrame.Navigate(typeof(OobeFileLocksmith)); break;
                    case "Run": NavigationFrame.Navigate(typeof(OobeRun)); break;
                    case "ImageResizer": NavigationFrame.Navigate(typeof(OobeImageResizer)); break;
                    case "KBM": NavigationFrame.Navigate(typeof(OobeKBM)); break;
                    case "LightSwitch": NavigationFrame.Navigate(typeof(OobeLightSwitch)); break;
                    case "PowerRename": NavigationFrame.Navigate(typeof(OobePowerRename)); break;
                    case "QuickAccent": NavigationFrame.Navigate(typeof(OobePowerAccent)); break;
                    case "FileExplorer": NavigationFrame.Navigate(typeof(OobeFileExplorer)); break;
                    case "ShortcutGuide": NavigationFrame.Navigate(typeof(OobeShortcutGuide)); break;
                    case "TextExtractor": NavigationFrame.Navigate(typeof(OobePowerOCR)); break;
                    case "MouseUtils": NavigationFrame.Navigate(typeof(OobeMouseUtils)); break;
                    case "MouseWithoutBorders": NavigationFrame.Navigate(typeof(OobeMouseWithoutBorders)); break;
                    case "MeasureTool": NavigationFrame.Navigate(typeof(OobeMeasureTool)); break;
                    case "Hosts": NavigationFrame.Navigate(typeof(OobeHosts)); break;
                    case "RegistryPreview": NavigationFrame.Navigate(typeof(OobeRegistryPreview)); break;
                    case "Peek": NavigationFrame.Navigate(typeof(OobePeek)); break;
                    case "NewPlus": NavigationFrame.Navigate(typeof(OobeNewPlus)); break;
                    case "Workspaces": NavigationFrame.Navigate(typeof(OobeWorkspaces)); break;
                    case "ZoomIt": NavigationFrame.Navigate(typeof(OobeZoomIt)); break;
                }
            }
        }

        private void ShellPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Select the first module by default
            if (navigationView.MenuItems.Count > 0)
            {
                navigationView.SelectedItem = navigationView.MenuItems[0];
            }
        }

        private void NavigationView_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
        {
            AppTitleBar.IsPaneToggleButtonVisible = args.DisplayMode == NavigationViewDisplayMode.Compact || args.DisplayMode == NavigationViewDisplayMode.Minimal;
        }

        private void TitleBar_PaneButtonClick(TitleBar sender, object args)
        {
            navigationView.IsPaneOpen = !navigationView.IsPaneOpen;
        }

        private void WhatIsNewItem_Tapped(object sender, TappedRoutedEventArgs e)
        {
            OpenScoobeWindow();
        }
    }
}
