// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using CommunityToolkit.WinUI.Controls;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using WinUIEx;

namespace Microsoft.PowerToys.Settings.UI.SettingsXAML.Controls.Dashboard
{
    public sealed partial class ShortcutConflictWindow : WindowEx
    {
        public ShortcutConflictViewModel ViewModel { get; private set; }

        public ShortcutConflictWindow()
        {
            var settingsUtils = SettingsUtils.Default;
            ViewModel = new ShortcutConflictViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage);

            InitializeComponent();

            // Set DataContext on the root Grid instead of the Window
            RootGrid.DataContext = ViewModel;

            this.Activated += Window_Activated_SetIcon;

            // Set localized window title
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(titleBar);

            this.Title = resourceLoader.GetString("ShortcutConflictWindow_Title");
            this.CenterOnScreen();

            ViewModel.OnPageLoaded();
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
                var moduleType = moduleData.ModuleType;
                NavigationService.Navigate(ModuleHelper.GetModulePageType(moduleType));
                this.Close();
            }
        }

        private void OnIgnoreConflictClicked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is HotkeyConflictGroupData conflictGroup)
            {
                // The Click event only fires from user interaction, not programmatic changes
                if (checkBox.IsChecked == true)
                {
                    IgnoreConflictGroup(conflictGroup);
                }
                else
                {
                    UnignoreConflictGroup(conflictGroup);
                }
            }
        }

        private void IgnoreConflictGroup(HotkeyConflictGroupData conflictGroup)
        {
            try
            {
                // Ignore all hotkey settings in this conflict group
                if (conflictGroup.Modules != null)
                {
                    HotkeySettings hotkey = new(conflictGroup.Hotkey.Win, conflictGroup.Hotkey.Ctrl, conflictGroup.Hotkey.Alt, conflictGroup.Hotkey.Shift, conflictGroup.Hotkey.Key);
                    ViewModel.IgnoreShortcut(hotkey);
                }
            }
            catch
            {
            }
        }

        private void UnignoreConflictGroup(HotkeyConflictGroupData conflictGroup)
        {
            try
            {
                // Unignore all hotkey settings in this conflict group
                if (conflictGroup.Modules != null)
                {
                    HotkeySettings hotkey = new(conflictGroup.Hotkey.Win, conflictGroup.Hotkey.Ctrl, conflictGroup.Hotkey.Alt, conflictGroup.Hotkey.Shift, conflictGroup.Hotkey.Key);
                    ViewModel.UnignoreShortcut(hotkey);
                }
            }
            catch
            {
            }
        }

        private void WindowEx_Closed(object sender, WindowEventArgs args)
        {
            ViewModel?.Dispose();
        }

        private void Window_Activated_SetIcon(object sender, WindowActivatedEventArgs args)
        {
            // Set window icon
            AppWindow.SetIcon("Assets\\Settings\\icon.ico");
        }
    }
}
