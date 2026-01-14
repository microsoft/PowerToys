// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using ManagedCommon;
using Microsoft.Extensions.AI;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.PowerToys.Settings.UI.OOBE.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PowerToys.Interop;
using Windows.Graphics;
using WinRT.Interop;
using WinUIEx;
using WinUIEx.Messaging;

namespace Microsoft.PowerToys.Settings.UI
{
    public sealed partial class OobeWindow : WindowEx
    {
        public OobeShellViewModel ViewModel => App.OobeShellViewModel;

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

        public OobeWindow()
        {
            App.ThemeService.ThemeChanged += OnThemeChanged;
            App.ThemeService.ApplyTheme();

            this.InitializeComponent();

            SetTitleBar();

            this.ExtendsContentIntoTitleBar = true;

            RootGrid.DataContext = ViewModel;

            SetRunSharedEventCallback(() =>
            {
                return Constants.PowerLauncherSharedEvent();
            });

            SetColorPickerSharedEventCallback(() =>
            {
                return Constants.ShowColorPickerSharedEvent();
            });

            SetOpenMainWindowCallback((Type type) =>
            {
                App.OpenSettingsWindow(type);
            });
        }

        private void SetTitleBar()
        {
            WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(WindowNative.GetWindowHandle(this));
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
            Title = ResourceLoaderInstance.ResourceLoader.GetString("OobeWindow_Title");
        }

        public void OnClosing()
        {
            if (navigationView.SelectedItem is NavigationViewItem selectedItem)
            {
                App.OobeShellViewModel.GetModuleFromTag((string)selectedItem.Tag).LogClosingModuleEvent();
            }
        }

        public void NavigateToModule(PowerToysModules selectedModule)
        {
            navigationView.SelectedItem = navigationView.MenuItems[(int)selectedModule];
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (navigationView.SelectedItem is NavigationViewItem selectedItem)
            {
                switch (selectedItem.Tag)
                {
                    case "Overview": NavigationFrame.Navigate(typeof(OobeOverview)); break;
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

        private void NavigationView_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
        {
            if (args.DisplayMode == NavigationViewDisplayMode.Compact || args.DisplayMode == NavigationViewDisplayMode.Minimal)
            {
                TitleBarIcon.Margin = new Thickness(0, 0, 8, 0); // Workaround, see XAML comment
                AppTitleBar.IsPaneToggleButtonVisible = true;
            }
            else
            {
                TitleBarIcon.Margin = new Thickness(16, 0, 0, 0);  // Workaround, see XAML comment
                AppTitleBar.IsPaneToggleButtonVisible = false;
            }
        }

        private void TitleBar_PaneButtonClick(Microsoft.UI.Xaml.Controls.TitleBar sender, object args)
        {
            navigationView.IsPaneOpen = !navigationView.IsPaneOpen;
        }

        private void WhatIsNewItem_Tapped(object sender, TappedRoutedEventArgs e)
        {
            App.OpenScoobeWindow();
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            // Set window icon
            this.SetIcon("Assets\\Settings\\icon.ico");
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            var mainWindow = App.GetSettingsWindow();
            if (mainWindow != null)
            {
                mainWindow.CloseHiddenWindow();
            }

            App.ThemeService.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged(object sender, ElementTheme theme)
        {
            WindowHelper.SetTheme(this, theme);
        }

        private void NavigationView_Loaded(object sender, RoutedEventArgs e)
        {
            // Select the first module by default
            if (navigationView.MenuItems.Count > 0)
            {
                navigationView.SelectedItem = navigationView.MenuItems[0];
            }
        }
    }
}
