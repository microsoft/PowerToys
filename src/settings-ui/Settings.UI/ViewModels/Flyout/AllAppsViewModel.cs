// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class AllAppsViewModel : Observable
    {
        public ObservableCollection<FlyoutMenuItem> FlyoutMenuItems { get; set; }

        private ISettingsRepository<GeneralSettings> _settingsRepository;
        private GeneralSettings generalSettingsConfig;

        private Func<string, int> SendConfigMSG { get; }

        public AllAppsViewModel(ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            _settingsRepository = settingsRepository;
            generalSettingsConfig = settingsRepository.SettingsConfig;
            generalSettingsConfig.AddEnabledModuleChangeNotification(ModuleEnabledChangedOnSettingsPage);

            FlyoutMenuItems = new ObservableCollection<FlyoutMenuItem>
            {
                new FlyoutMenuItem() { Label = "Always On Top", IsEnabled = generalSettingsConfig.Enabled.AlwaysOnTop, Tag = "AlwaysOnTop", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsAlwaysOnTop.png", EnabledChangedCallback = EnabledChangedOnUI },
                new FlyoutMenuItem() { Label = "Awake", IsEnabled = generalSettingsConfig.Enabled.Awake, Tag = "Awake", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsAwake.png", EnabledChangedCallback = EnabledChangedOnUI },
                new FlyoutMenuItem() { Label = "Color Picker", IsEnabled = generalSettingsConfig.Enabled.ColorPicker, Tag = "ColorPicker", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsColorPicker.png", EnabledChangedCallback = EnabledChangedOnUI },
                new FlyoutMenuItem() { Label = "FancyZones", IsEnabled = generalSettingsConfig.Enabled.FancyZones, Tag = "FancyZones", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsFancyZones.png", EnabledChangedCallback = EnabledChangedOnUI },
                new FlyoutMenuItem() { Label = "File Locksmith", IsEnabled = generalSettingsConfig.Enabled.FileLocksmith, Tag = "FileLocksmith", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsFileLocksmith.png", EnabledChangedCallback = EnabledChangedOnUI },
                new FlyoutMenuItem() { Label = "Find My Mouse", IsEnabled = generalSettingsConfig.Enabled.FindMyMouse, Tag = "FindMyMouse", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsFindMyMouse.png", EnabledChangedCallback = EnabledChangedOnUI },
                new FlyoutMenuItem() { Label = "Hosts File Editor", IsEnabled = generalSettingsConfig.Enabled.Hosts, Tag = "Hosts", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsHosts.png", EnabledChangedCallback = EnabledChangedOnUI },
                new FlyoutMenuItem() { Label = "Image Resizer", IsEnabled = generalSettingsConfig.Enabled.ImageResizer, Tag = "ImageResizer", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsImageResizer.png", EnabledChangedCallback = EnabledChangedOnUI },
                new FlyoutMenuItem() { Label = "Keyboard Manager", IsEnabled = generalSettingsConfig.Enabled.KeyboardManager, Tag = "KBM", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsKeyboardManager.png", EnabledChangedCallback = EnabledChangedOnUI },
                new FlyoutMenuItem() { Label = "Mouse Highlighter", IsEnabled = generalSettingsConfig.Enabled.MouseHighlighter, Tag = "MouseHightlighter", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsMouseHighlighter.png", EnabledChangedCallback = EnabledChangedOnUI },
                new FlyoutMenuItem() { Label = "Mouse Pointer Crosshairs", IsEnabled = generalSettingsConfig.Enabled.MousePointerCrosshairs, Tag = "MouseCrosshairs", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsMouseCrosshairs.png", EnabledChangedCallback = EnabledChangedOnUI },
                new FlyoutMenuItem() { Label = "PowerRename", IsEnabled = generalSettingsConfig.Enabled.PowerRename, Tag = "PowerRename", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerRename.png", EnabledChangedCallback = EnabledChangedOnUI },
                new FlyoutMenuItem() { Label = "PowerToys Run", IsEnabled = generalSettingsConfig.Enabled.PowerLauncher, Tag = "PowerLauncher", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerLauncher.png", EnabledChangedCallback = EnabledChangedOnUI },
                new FlyoutMenuItem() { Label = "Quick Accent", IsEnabled = generalSettingsConfig.Enabled.PowerAccent, Tag = "QuickAccent", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerAccent.png", EnabledChangedCallback = EnabledChangedOnUI },
                new FlyoutMenuItem() { Label = "Screen Ruler", IsEnabled = generalSettingsConfig.Enabled.MeasureTool, Tag = "MeasureTool", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsScreenRuler.png", EnabledChangedCallback = EnabledChangedOnUI },
                new FlyoutMenuItem() { Label = "Shortcut Guide", IsEnabled = generalSettingsConfig.Enabled.ShortcutGuide, Tag = "ShortcutGuide", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsShortcutGuide.png", EnabledChangedCallback = EnabledChangedOnUI },
                new FlyoutMenuItem() { Label = "Text Extractor", IsEnabled = generalSettingsConfig.Enabled.PowerOCR, Tag = "PowerOCR", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerOCR.png", EnabledChangedCallback = EnabledChangedOnUI },
                new FlyoutMenuItem() { Label = "Video Conference Mute", IsEnabled = generalSettingsConfig.Enabled.VideoConference, Tag = "VideoConference", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsVideoConferenceMute.png", EnabledChangedCallback = EnabledChangedOnUI },
            };

            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        private void EnabledChangedOnUI(FlyoutMenuItem flyoutMenuItem)
        {
            generalSettingsConfig = _settingsRepository.SettingsConfig;
            generalSettingsConfig.AddEnabledModuleChangeNotification(ModuleEnabledChangedOnSettingsPage);
            switch (flyoutMenuItem.Tag)
            {
                case "AlwaysOnTop": generalSettingsConfig.Enabled.AlwaysOnTop = flyoutMenuItem.IsEnabled; break;
                case "Awake": generalSettingsConfig.Enabled.Awake = flyoutMenuItem.IsEnabled; break;
                case "ColorPicker": generalSettingsConfig.Enabled.ColorPicker = flyoutMenuItem.IsEnabled; break;
                case "FancyZones": generalSettingsConfig.Enabled.FancyZones = flyoutMenuItem.IsEnabled; break;
                case "FileLocksmith": generalSettingsConfig.Enabled.FileLocksmith = flyoutMenuItem.IsEnabled; break;
                case "FindMyMouse": generalSettingsConfig.Enabled.FindMyMouse = flyoutMenuItem.IsEnabled; break;
                case "Hosts": generalSettingsConfig.Enabled.Hosts = flyoutMenuItem.IsEnabled; break;
                case "ImageResizer": generalSettingsConfig.Enabled.ImageResizer = flyoutMenuItem.IsEnabled; break;
                case "KeyboardManager": generalSettingsConfig.Enabled.KeyboardManager = flyoutMenuItem.IsEnabled; break;
                case "MouseHighlighter": generalSettingsConfig.Enabled.MouseHighlighter = flyoutMenuItem.IsEnabled; break;
                case "MousePointerCrosshairs": generalSettingsConfig.Enabled.MousePointerCrosshairs = flyoutMenuItem.IsEnabled; break;
                case "PowerRename": generalSettingsConfig.Enabled.PowerRename = flyoutMenuItem.IsEnabled; break;
                case "PowerLauncher": generalSettingsConfig.Enabled.PowerLauncher = flyoutMenuItem.IsEnabled; break;
                case "PowerAccent": generalSettingsConfig.Enabled.PowerAccent = flyoutMenuItem.IsEnabled; break;
                case "MeasureTool": generalSettingsConfig.Enabled.MeasureTool = flyoutMenuItem.IsEnabled; break;
                case "ShortcutGuide": generalSettingsConfig.Enabled.ShortcutGuide = flyoutMenuItem.IsEnabled; break;
                case "PowerOCR": generalSettingsConfig.Enabled.PowerOCR = flyoutMenuItem.IsEnabled; break;
                case "VideoConference": generalSettingsConfig.Enabled.VideoConference = flyoutMenuItem.IsEnabled; break;
            }

            var outgoing = new OutGoingGeneralSettings(generalSettingsConfig);
            SendConfigMSG(outgoing.ToString());
        }

        private void ModuleEnabledChangedOnSettingsPage()
        {
            generalSettingsConfig = _settingsRepository.SettingsConfig;
            generalSettingsConfig.AddEnabledModuleChangeNotification(ModuleEnabledChangedOnSettingsPage);
            foreach (FlyoutMenuItem item in FlyoutMenuItems)
            {
                switch (item.Tag)
                {
                    case "AlwaysOnTop": item.IsEnabled = generalSettingsConfig.Enabled.AlwaysOnTop; break;
                    case "Awake": item.IsEnabled = generalSettingsConfig.Enabled.Awake; break;
                    case "ColorPicker": item.IsEnabled = generalSettingsConfig.Enabled.ColorPicker; break;
                    case "FancyZones": item.IsEnabled = generalSettingsConfig.Enabled.FancyZones; break;
                    case "FileLocksmith": item.IsEnabled = generalSettingsConfig.Enabled.FileLocksmith; break;
                    case "FindMyMouse": item.IsEnabled = generalSettingsConfig.Enabled.FindMyMouse; break;
                    case "Hosts": item.IsEnabled = generalSettingsConfig.Enabled.Hosts; break;
                    case "ImageResizer": item.IsEnabled = generalSettingsConfig.Enabled.ImageResizer; break;
                    case "KeyboardManager": item.IsEnabled = generalSettingsConfig.Enabled.KeyboardManager; break;
                    case "MouseHighlighter": item.IsEnabled = generalSettingsConfig.Enabled.MouseHighlighter; break;
                    case "MousePointerCrosshairs": item.IsEnabled = generalSettingsConfig.Enabled.MousePointerCrosshairs; break;
                    case "PowerRename": item.IsEnabled = generalSettingsConfig.Enabled.PowerRename; break;
                    case "PowerLauncher": item.IsEnabled = generalSettingsConfig.Enabled.PowerLauncher; break;
                    case "PowerAccent": item.IsEnabled = generalSettingsConfig.Enabled.PowerAccent; break;
                    case "MeasureTool": item.IsEnabled = generalSettingsConfig.Enabled.MeasureTool; break;
                    case "ShortcutGuide": item.IsEnabled = generalSettingsConfig.Enabled.ShortcutGuide; break;
                    case "PowerOCR": item.IsEnabled = generalSettingsConfig.Enabled.PowerOCR; break;
                    case "VideoConference": item.IsEnabled = generalSettingsConfig.Enabled.VideoConference; break;
                }
            }
        }
    }
}
