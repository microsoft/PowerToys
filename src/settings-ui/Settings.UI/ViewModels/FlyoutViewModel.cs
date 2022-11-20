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
    public class FlyoutViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        public ObservableCollection<FlyoutMenuItem> FlyoutMenuItems { get; set; }

        public FlyoutViewModel(ISettingsRepository<GeneralSettings> settingsRepository)
        {
            GeneralSettingsConfig = settingsRepository.SettingsConfig;
            FlyoutMenuItems = new ObservableCollection<FlyoutMenuItem>();
            if (GeneralSettingsConfig.Enabled.ColorPicker)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "ColorPicker", Tag = "ColorPicker", ToolTip = SettingsRepository<ColorPickerSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut.GetKeysList().ToString(), Icon = "ms-appx:///Assets/FluentIcons/FluentIconsColorPicker.png" });
            }

            if (GeneralSettingsConfig.Enabled.FancyZones)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "FancyZones Editor", Tag = "FancyZones", ToolTip = SettingsRepository<FancyZonesSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.FancyzonesEditorHotkey.Value.GetKeysList().ToString(), Icon = "ms-appx:///Assets/FluentIcons/FluentIconsFancyZones.png" });
            }

            if (GeneralSettingsConfig.Enabled.Hosts)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Hosts File Editor", Tag = "Hosts", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsHosts.png" });
            }

            if (GeneralSettingsConfig.Enabled.PowerLauncher)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "PowerToys Run", Tag = "PowerLauncher", ToolTip = SettingsRepository<PowerLauncherSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.OpenPowerLauncher.GetKeysList().ToString(), Icon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerToysRun.png" });
            }

            if (GeneralSettingsConfig.Enabled.MeasureTool)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Screen Ruler", Tag = "MeasureTool", ToolTip = SettingsRepository<MeasureToolSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut.GetKeysList().ToString(), Icon = "ms-appx:///Assets/FluentIcons/FluentIconsScreenRuler.png" });
            }

            if (GeneralSettingsConfig.Enabled.ShortcutGuide)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Shortcut Guide", Tag = "ShortcutGuide", ToolTip = SettingsRepository<ShortcutGuideSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.OpenShortcutGuide.GetKeysList().ToString(), Icon = "ms-appx:///Assets/FluentIcons/FluentIconsShortcutGuide.png" });
            }

            if (GeneralSettingsConfig.Enabled.PowerOCR)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Text Extractor", Tag = "PowerOCR", ToolTip = SettingsRepository<PowerOcrSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut.GetKeysList().ToString(), Icon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerOcr.png" });
            }
        }
    }

#pragma warning disable SA1402 // File may only contain a single type
    public class FlyoutMenuItem
#pragma warning restore SA1402 // File may only contain a single type
    {
        public string Label { get; set; }

        public string Icon { get; set; }

        public string ToolTip { get; set; }

        public string Tag { get; set; }
    }
}
