// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Windows.Forms;
using CommunityToolkit.WinUI.Controls;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using Windows.Web.AtomPub;

namespace Microsoft.PowerToys.Settings.UI.SettingsXAML.Controls.Dashboard
{
    public sealed partial class ShortcutConflictWindow : Window
    {
        public ShortcutConflictViewModel DataContext { get; }

        public ShortcutConflictViewModel ViewModel { get; private set; }

        public ShortcutConflictWindow()
        {
            var settingsUtils = new SettingsUtils();
            ViewModel = new ShortcutConflictViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage);

            DataContext = ViewModel;
            InitializeComponent();

            // Set up the custom action name delegate for LocalizationHelper
            LocalizationHelper.GetCustomActionNameDelegate = GetCustomActionName;

            // Set localized window title
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            this.Title = resourceLoader.GetString("ShortcutConflictWindow_Title/Text");

            // Configure window presenter to disable maximize button
            if (this.AppWindow.Presenter is OverlappedPresenter overlappedPresenter)
            {
                overlappedPresenter.IsMaximizable = false;
                overlappedPresenter.IsMinimizable = false;
            }

            // Set window size using AppWindow API
            this.AppWindow.Resize(new SizeInt32(900, 1200));

            // Set window properties
            this.AppWindow.SetIcon("Assets/Settings/Icons/PowerToys.ico");
            this.AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            this.AppWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
            this.AppWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;

            // Center the window on screen
            this.CenterOnScreen();

            ViewModel.OnPageLoaded();

            Closed += (s, e) =>
            {
                // Clean up the delegate when window is closed
                LocalizationHelper.GetCustomActionNameDelegate = null;
                ViewModel?.Dispose();
            };
        }

        private void CenterOnScreen()
        {
            var displayArea = DisplayArea.GetFromWindowId(this.AppWindow.Id, DisplayAreaFallback.Nearest);
            if (displayArea != null)
            {
                var windowSize = this.AppWindow.Size;
                var centeredPosition = new PointInt32
                {
                    X = (displayArea.WorkArea.Width - windowSize.Width) / 2,
                    Y = (displayArea.WorkArea.Height - windowSize.Height) / 2,
                };
                this.AppWindow.Move(centeredPosition);
            }
        }

        private void SettingsCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is SettingsCard settingsCard &&
                settingsCard.DataContext is ModuleHotkeyData moduleData)
            {
                var moduleName = moduleData.ModuleName;

                // Navigate to the module's settings page
                if (ModuleNavigationHelper.NavigateToModulePage(moduleName))
                {
                    this.Close();
                }
            }
        }

        private void SettingsCard_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is SettingsCard card && card.DataContext is ModuleHotkeyData moduleData)
            {
                var iconPath = GetModuleIconPath(moduleData.ModuleName);

                // Setup header for SettingsCard
                card.Header = LocalizationHelper.GetLocalizedHotkeyHeader(moduleData.ModuleName, moduleData.HotkeyName);

                card.HeaderIcon = new BitmapIcon
                {
                    UriSource = new Uri(iconPath),
                    ShowAsMonochrome = false,
                };
            }
        }

        /// <summary>
        /// Gets the custom action name for AdvancedPaste
        /// </summary>
        /// <param name="moduleName">The module name</param>
        /// <param name="actionId">The custom action ID</param>
        /// <returns>The custom action name, or null if not found</returns>
        private string GetCustomActionName(string moduleName, int actionId)
        {
            if (!moduleName.Equals("advancedpaste", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return ViewModel?.GetAdvancedPasteCustomActionName(actionId);
        }

        private string GetModuleIconPath(string moduleName)
        {
            return moduleName?.ToLowerInvariant() switch
            {
                "advancedpaste" => "ms-appx:///Assets/Settings/Icons/AdvancedPaste.png",
                "alwaysontop" => "ms-appx:///Assets/Settings/Icons/AlwaysOnTop.png",
                "colorpicker" => "ms-appx:///Assets/Settings/Icons/ColorPicker.png",
                "cropandlock" => "ms-appx:///Assets/Settings/Icons/CropAndLock.png",
                "fancyzones" => "ms-appx:///Assets/Settings/Icons/FancyZones.png",
                "mousehighlighter" => "ms-appx:///Assets/Settings/Icons/MouseHighlighter.png",
                "mousepointercrosshairs" => "ms-appx:///Assets/Settings/Icons/MouseCrosshairs.png",
                "findmymouse" => "ms-appx:///Assets/Settings/Icons/FindMyMouse.png",
                "mousejump" => "ms-appx:///Assets/Settings/Icons/MouseJump.png",
                "peek" => "ms-appx:///Assets/Settings/Icons/Peek.png",
                "powerlauncher" => "ms-appx:///Assets/Settings/Icons/PowerToysRun.png",
                "measuretool" => "ms-appx:///Assets/Settings/Icons/ScreenRuler.png",
                "shortcutguide" => "ms-appx:///Assets/Settings/Icons/ShortcutGuide.png",
                "powerocr" => "ms-appx:///Assets/Settings/Icons/TextExtractor.png",
                "workspaces" => "ms-appx:///Assets/Settings/Icons/Workspaces.png",
                "cmdpal" => "ms-appx:///Assets/Settings/Icons/CmdPal.png",
                "mousewithoutborders" => "ms-appx:///Assets/Settings/Icons/MouseWithoutBorders.png",
                "zoomit" => "ms-appx:///Assets/Settings/Icons/ZoomIt.png",
                "measure tool" => "ms-appx:///Assets/Settings/Icons/ScreenRuler.png",
                _ => "ms-appx:///Assets/Settings/Icons/PowerToys.png",
            };
        }
    }
}
