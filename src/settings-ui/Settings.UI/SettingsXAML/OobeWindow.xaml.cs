// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.PowerToys.Settings.UI.OOBE.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PowerToys.Interop;
using WinRT.Interop;
using WinUIEx;

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

        /// <summary>
        /// Gets or sets the callback that selects the OOBE page of a module, by its navigation tag.
        /// </summary>
        public static Action<string> NavigateToModuleCallback { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Welcome intro animation already played in this OOBE window.
        /// </summary>
        public static bool WelcomeIntroPlayed { get; set; }

        private static readonly TimeSpan RevealFallbackTimeout = TimeSpan.FromSeconds(1);

        private bool _isCloaked;
        private DispatcherQueueTimer _revealFallbackTimer;

        public OobeWindow()
        {
            WelcomeIntroPlayed = false;

            this.InitializeComponent();

            // Apply the theme after the content exists so the first rendered frame already uses it.
            App.ThemeService.ThemeChanged += OnThemeChanged;
            App.ThemeService.ApplyTheme();

            SetTitleBar();
            CloakUntilFirstFrame();

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

            NavigateToModuleCallback = NavigateToModule;
        }

        private void NavigateToModule(string tag)
        {
            foreach (var item in navigationView.MenuItems)
            {
                if (item is NavigationViewItem navigationItem && navigationItem.Tag is string itemTag && itemTag == tag)
                {
                    navigationView.SelectedItem = navigationItem;
                    return;
                }
            }
        }

        private void SetTitleBar()
        {
            WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(WindowNative.GetWindowHandle(this));
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
            TitleBarHelper.SetPreferredTheme(this);

            Title = ResourceLoaderInstance.ResourceLoader.GetString("OobeWindow_Title");
        }

        // Keep the window invisible until XAML has rendered the first page; otherwise DWM shows
        // the default white surface before the content and Mica backdrop are ready.
        private void CloakUntilFirstFrame()
        {
            _isCloaked = SetCloak(true);
            if (!_isCloaked)
            {
                return;
            }

            CompositionTarget.Rendering += OnCompositionTargetRendering;

            _revealFallbackTimer = DispatcherQueue.CreateTimer();
            _revealFallbackTimer.Interval = RevealFallbackTimeout;
            _revealFallbackTimer.IsRepeating = false;
            _revealFallbackTimer.Tick += (_, _) => Reveal();
            _revealFallbackTimer.Start();
        }

        private void OnCompositionTargetRendering(object sender, object e)
        {
            if (NavigationFrame.Content is not FrameworkElement { IsLoaded: true })
            {
                return;
            }

            CompositionTarget.Rendering -= OnCompositionTargetRendering;

            // Rendering is raised before the frame is committed, so reveal once the dispatcher is idle.
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, Reveal);
        }

        private void Reveal()
        {
            CompositionTarget.Rendering -= OnCompositionTargetRendering;
            _revealFallbackTimer?.Stop();
            _revealFallbackTimer = null;

            if (_isCloaked)
            {
                _isCloaked = !SetCloak(false);
            }
        }

        private bool SetCloak(bool cloak)
        {
            int value = cloak ? 1 : 0;
            return NativeMethods.DwmSetWindowAttribute(WindowNative.GetWindowHandle(this), NativeMethods.DWMWA_CLOAK, ref value, sizeof(int)) == 0;
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
                    case "PowerDisplay": NavigationFrame.Navigate(typeof(OobePowerDisplay)); break;
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
                    case "AltWindowCycle": NavigationFrame.Navigate(typeof(OobeAltWindowCycle)); break;
                    case "Workspaces": NavigationFrame.Navigate(typeof(OobeWorkspaces)); break;
                    case "GrabAndMove": NavigationFrame.Navigate(typeof(OobeGrabAndMove)); break;
                    case "ZoomIt": NavigationFrame.Navigate(typeof(OobeZoomIt)); break;
                }
            }
        }

        private void NavigationView_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
        {
            if (args.DisplayMode == NavigationViewDisplayMode.Compact || args.DisplayMode == NavigationViewDisplayMode.Minimal)
            {
                AppTitleBar.IsPaneToggleButtonVisible = true;
            }
            else
            {
                AppTitleBar.IsPaneToggleButtonVisible = false;
            }
        }

        private void TitleBar_PaneButtonClick(Microsoft.UI.Xaml.Controls.TitleBar sender, object args)
        {
            navigationView.IsPaneOpen = !navigationView.IsPaneOpen;
        }

        private void WhatIsNewItem_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ((App)App.Current)!.OpenScoobe();
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            // Set window icon
            this.SetIcon("Assets\\Settings\\icon.ico");
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            CompositionTarget.Rendering -= OnCompositionTargetRendering;
            _revealFallbackTimer?.Stop();
            _revealFallbackTimer = null;

            if (navigationView.SelectedItem is NavigationViewItem selectedItem && selectedItem.Tag is string tag)
            {
                App.OobeShellViewModel.GetModuleFromTag(tag).LogClosingModuleEvent();
            }

            if (App.GetSettingsWindow() is MainWindow mainWindow)
            {
                mainWindow.CloseHiddenWindow();
            }

            App.ThemeService.ThemeChanged -= OnThemeChanged;

            if (NavigateToModuleCallback == NavigateToModule)
            {
                NavigateToModuleCallback = null;
            }
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
