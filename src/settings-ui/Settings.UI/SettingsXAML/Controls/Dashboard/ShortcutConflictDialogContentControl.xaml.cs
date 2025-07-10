// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.WinUI.Controls;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Microsoft.PowerToys.Settings.UI.SettingsXAML.Controls.Dashboard
{
    public sealed partial class ShortcutConflictDialogContentControl : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty ConflictsDataProperty =
            DependencyProperty.Register(
                nameof(ConflictsData),
                typeof(AllHotkeyConflictsData),
                typeof(ShortcutConflictDialogContentControl),
                new PropertyMetadata(null, OnConflictsDataChanged));

        public AllHotkeyConflictsData ConflictsData
        {
            get => (AllHotkeyConflictsData)GetValue(ConflictsDataProperty);
            set => SetValue(ConflictsDataProperty, value);
        }

        public List<HotkeyConflictGroupData> ConflictItems { get; private set; } = new List<HotkeyConflictGroupData>();

        // Event to close the dialog when navigation occurs
        public event EventHandler DialogCloseRequested;

        private static void OnConflictsDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ShortcutConflictDialogContentControl content)
            {
                content.UpdateConflictItems();
            }
        }

        public ShortcutConflictDialogContentControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void UpdateConflictItems()
        {
            var items = new List<HotkeyConflictGroupData>();

            if (ConflictsData?.InAppConflicts != null)
            {
                foreach (var conflict in ConflictsData.InAppConflicts)
                {
                    // Ensure each module has HotkeySettings for the ShortcutControl
                    foreach (var module in conflict.Modules)
                    {
                        if (module.HotkeySettings == null)
                        {
                            // Create HotkeySettings from the conflict hotkey data
                            module.HotkeySettings = ConvertToHotkeySettings(conflict.Hotkey, module.HotkeyName, module.ModuleName, false);
                        }

                        // Mark as having conflict and set conflict properties
                        module.HotkeySettings.HasConflict = true;
                        module.HotkeySettings.IsSystemConflict = false;
                        module.HotkeySettings.ConflictDescription = GetConflictDescription(conflict, module, false);
                        module.IsSystemConflict = false; // In-app conflicts are not system conflicts
                    }
                }

                items.AddRange(ConflictsData.InAppConflicts);
            }

            if (ConflictsData?.SystemConflicts != null)
            {
                foreach (var conflict in ConflictsData.SystemConflicts)
                {
                    // Ensure each module has HotkeySettings for the ShortcutControl
                    foreach (var module in conflict.Modules)
                    {
                        if (module.HotkeySettings == null)
                        {
                            // Create HotkeySettings from the conflict hotkey data
                            module.HotkeySettings = ConvertToHotkeySettings(conflict.Hotkey, module.HotkeyName, module.ModuleName, true);
                        }

                        // Mark as having conflict and set conflict properties
                        module.HotkeySettings.HasConflict = true;
                        module.HotkeySettings.IsSystemConflict = true;
                        module.HotkeySettings.ConflictDescription = GetConflictDescription(conflict, module, true);
                        module.IsSystemConflict = true; // System conflicts
                    }
                }

                items.AddRange(ConflictsData.SystemConflicts);
            }

            ConflictItems = items;
            OnPropertyChanged(nameof(ConflictItems));
        }

        private HotkeySettings ConvertToHotkeySettings(HotkeyData hotkeyData, string hotkeyName, string moduleName, bool isSystemConflict)
        {
            // Convert HotkeyData to HotkeySettings using actual data from hotkeyData
            return new HotkeySettings(
                win: hotkeyData.Win,
                ctrl: hotkeyData.Ctrl,
                alt: hotkeyData.Alt,
                shift: hotkeyData.Shift,
                code: hotkeyData.Key,
                hotkeyName: hotkeyName,
                ownerModuleName: moduleName,
                hasConflict: true) // Always set to true since this is a conflict dialog
            {
                IsSystemConflict = isSystemConflict,
            };
        }

        private void SettingsCard_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is SettingsCard card && card.DataContext is ModuleHotkeyData moduleData)
            {
                var iconPath = GetModuleIconPath(moduleData.ModuleName);
                card.HeaderIcon = new BitmapIcon
                {
                    UriSource = new Uri(iconPath),
                    ShowAsMonochrome = false,
                };
            }
        }

        private string GetModuleIconPath(string moduleName)
        {
            return moduleName?.ToLowerInvariant() switch
            {
                "advancedpaste" => "ms-appx:///Assets/Settings/Icons/AdvancedPaste.png",
                "alwaysontop" => "ms-appx:///Assets/Settings/Icons/AlwaysOnTop.png",
                "awake" => "ms-appx:///Assets/Settings/Icons/Awake.png",
                "cmdpal" => "ms-appx:///Assets/Settings/Icons/CmdPal.png",
                "colorpicker" => "ms-appx:///Assets/Settings/Icons/ColorPicker.png",
                "cropandlock" => "ms-appx:///Assets/Settings/Icons/CropAndLock.png",
                "environmentvariables" => "ms-appx:///Assets/Settings/Icons/EnvironmentVariables.png",
                "fancyzones" => "ms-appx:///Assets/Settings/Icons/FancyZones.png",
                "filelocksmith" => "ms-appx:///Assets/Settings/Icons/FileLocksmith.png",
                "findmymouse" => "ms-appx:///Assets/Settings/Icons/FindMyMouse.png",
                "hosts" => "ms-appx:///Assets/Settings/Icons/Hosts.png",
                "imageresizer" => "ms-appx:///Assets/Settings/Icons/ImageResizer.png",
                "keyboardmanager" => "ms-appx:///Assets/Settings/Icons/KeyboardManager.png",
                "measuretool" => "ms-appx:///Assets/Settings/Icons/ScreenRuler.png",
                "mousehighlighter" => "ms-appx:///Assets/Settings/Icons/MouseHighlighter.png",
                "mousejump" => "ms-appx:///Assets/Settings/Icons/MouseJump.png",
                "mousepointer" => "ms-appx:///Assets/Settings/Icons/MouseCrosshairs.png",
                "mousepointeraccessibility" => "ms-appx:///Assets/Settings/Icons/MouseCrosshairs.png",
                "mousepointercrosshairs" => "ms-appx:///Assets/Settings/Icons/MouseCrosshairs.png",
                "mousewithoutborders" => "ms-appx:///Assets/Settings/Icons/MouseWithoutBorders.png",
                "newplus" => "ms-appx:///Assets/Settings/Icons/NewPlus.png",
                "peek" => "ms-appx:///Assets/Settings/Icons/Peek.png",
                "poweraccent" => "ms-appx:///Assets/Settings/Icons/QuickAccent.png",
                "powerlauncher" => "ms-appx:///Assets/Settings/Icons/PowerToysRun.png",
                "powerocr" => "ms-appx:///Assets/Settings/Icons/TextExtractor.png",
                "powerpreview" => "ms-appx:///Assets/Settings/Icons/PowerPreview.png",
                "powerrename" => "ms-appx:///Assets/Settings/Icons/PowerRename.png",
                "registrypreview" => "ms-appx:///Assets/Settings/Icons/RegistryPreview.png",
                "shortcutguide" => "ms-appx:///Assets/Settings/Icons/ShortcutGuide.png",
                "workspaces" => "ms-appx:///Assets/Settings/Icons/Workspaces.png",
                "zoomit" => "ms-appx:///Assets/Settings/Icons/ZoomIt.png",
                _ => "ms-appx:///Assets/Settings/Icons/PowerToys.png",
            };
        }

        private string GetConflictDescription(HotkeyConflictGroupData conflict, ModuleHotkeyData currentModule, bool isSystemConflict)
        {
            if (isSystemConflict)
            {
                return "Conflicts with system shortcut";
            }

            // For in-app conflicts, list other conflicting modules
            var otherModules = conflict.Modules
                .Where(m => m.ModuleName != currentModule.ModuleName)
                .Select(m => m.ModuleName)
                .ToList();

            if (otherModules.Count == 1)
            {
                return $"Conflicts with {otherModules[0]}";
            }
            else if (otherModules.Count > 1)
            {
                return $"Conflicts with: {string.Join(", ", otherModules)}";
            }

            return "Shortcut conflict detected";
        }

        private void SettingsCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is SettingsCard settingsCard && settingsCard.DataContext is ModuleHotkeyData moduleData)
            {
                var moduleName = moduleData.ModuleName;

                // Navigate to the module's settings page
                if (ModuleNavigationHelper.NavigateToModulePage(moduleName))
                {
                    // Successfully navigated, close the dialog
                    DialogCloseRequested?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    // If navigation fails, try to handle special cases
                    HandleSpecialModuleNavigation(moduleName);
                }
            }
        }

        private void HandleSpecialModuleNavigation(string moduleName)
        {
            // Handle special cases for modules that might have different navigation logic
            switch (moduleName?.ToLowerInvariant())
            {
                case "mouse highlighter":
                case "mouse jump":
                case "mouse pointer crosshairs":
                case "find my mouse":
                    // These are all part of MouseUtils
                    if (ModuleNavigationHelper.NavigateToModulePage("MouseHighlighter"))
                    {
                        DialogCloseRequested?.Invoke(this, EventArgs.Empty);
                    }

                    break;

                case "system":
                case "windows":
                    // System conflicts - cannot navigate to a specific page
                    // Show a message or do nothing
                    break;

                default:
                    // Try a fallback navigation or show an error message
                    System.Diagnostics.Debug.WriteLine($"Could not navigate to settings page for module: {moduleName}");
                    break;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
