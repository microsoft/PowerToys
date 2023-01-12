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

        private GeneralSettings generalSettingsConfig;

        public AllAppsViewModel(ISettingsRepository<GeneralSettings> settingsRepository)
        {
            generalSettingsConfig = settingsRepository.SettingsConfig;

            FlyoutMenuItems = new ObservableCollection<FlyoutMenuItem>();
            FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Always On Top", IsEnabled = generalSettingsConfig.Enabled.AlwaysOnTop, Tag = "AlwaysOnTop", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsAlwaysOnTop.png" });
            FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Awake", IsEnabled = generalSettingsConfig.Enabled.Awake, Tag = "Awake", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsAwake.png" });
            FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Color Picker", IsEnabled = generalSettingsConfig.Enabled.ColorPicker, Tag = "ColorPicker", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsColorPicker.png" });
            FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "FancyZones", IsEnabled = generalSettingsConfig.Enabled.FancyZones, Tag = "FancyZones", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsFancyZones.png" });
            FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "File Locksmith", IsEnabled = generalSettingsConfig.Enabled.FileLocksmith, Tag = "FileLocksmith", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsFileLocksmith.png" });
            FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Find My Mouse", IsEnabled = generalSettingsConfig.Enabled.FindMyMouse, Tag = "FindMyMouse", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsFindMyMouse.png" });
            FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Hosts File Editor", IsEnabled = generalSettingsConfig.Enabled.Hosts, Tag = "Hosts", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsHosts.png" });
            FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Image Resizer", IsEnabled = generalSettingsConfig.Enabled.ImageResizer, Tag = "ImageResizer", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsImageResizer.png" });
            FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Keyboard Manager", IsEnabled = generalSettingsConfig.Enabled.KeyboardManager, Tag = "KBM", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsKeyboardManager.png" });
            FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Mouse Highlighter", IsEnabled = generalSettingsConfig.Enabled.MouseHighlighter, Tag = "MouseHightlighter", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsMouseHighlighter.png" });
            FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Mouse Pointer Crosshairs", IsEnabled = generalSettingsConfig.Enabled.MousePointerCrosshairs, Tag = "MouseCrosshairs", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsMouseCrosshairs.png" });
            FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "PowerRename", IsEnabled = generalSettingsConfig.Enabled.PowerRename, Tag = "PowerRename", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerRename.png" });
            FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "PowerToys Run", IsEnabled = generalSettingsConfig.Enabled.PowerLauncher, Tag = "PowerLauncher", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerLauncher.png" });
            FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Quick Accent", IsEnabled = generalSettingsConfig.Enabled.PowerAccent, Tag = "QuickAccent", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerAccent.png" });
            FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Screen Ruler", IsEnabled = generalSettingsConfig.Enabled.MeasureTool, Tag = "MeasureTool", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsScreenRuler.png" });
            FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Shortcut Guide", IsEnabled = generalSettingsConfig.Enabled.ShortcutGuide, Tag = "ShortcutGuide", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsShortcutGuide.png" });
            FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Text Extractor", IsEnabled = generalSettingsConfig.Enabled.PowerOCR, Tag = "PowerOCR", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerOCR.png" });
            FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Video Conference Mute", IsEnabled = generalSettingsConfig.Enabled.VideoConference, Tag = "VideoConference", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsVideoConferenceMute.png" });
        }
    }
}
