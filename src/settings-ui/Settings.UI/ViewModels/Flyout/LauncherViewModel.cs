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
    public class LauncherViewModel : Observable
    {
        public bool IsUpdateAvailable { get; set; }

        public ObservableCollection<FlyoutMenuItem> FlyoutMenuItems { get; set; }

        private GeneralSettings generalSettingsConfig;
        private UpdatingSettings updatingSettingsConfig;

        public LauncherViewModel(ISettingsRepository<GeneralSettings> settingsRepository)
        {
            generalSettingsConfig = settingsRepository.SettingsConfig;

            FlyoutMenuItems = new ObservableCollection<FlyoutMenuItem>();
            if (generalSettingsConfig.Enabled.ColorPicker)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "ColorPicker", Tag = "ColorPicker", ToolTip = SettingsRepository<ColorPickerSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut.ToString(), Icon = "ms-appx:///Assets/FluentIcons/FluentIconsColorPicker.png" });
            }

            if (generalSettingsConfig.Enabled.FancyZones)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "FancyZones Editor", Tag = "FancyZones", ToolTip = SettingsRepository<FancyZonesSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.FancyzonesEditorHotkey.Value.ToString(), Icon = "ms-appx:///Assets/FluentIcons/FluentIconsFancyZones.png" });
            }

            if (generalSettingsConfig.Enabled.Hosts)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Hosts File Editor", Tag = "Hosts", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsHosts.png" });
            }

            if (generalSettingsConfig.Enabled.PowerLauncher)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "PowerToys Run", Tag = "PowerLauncher", ToolTip = SettingsRepository<PowerLauncherSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.OpenPowerLauncher.ToString(), Icon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerToysRun.png" });
            }

            if (generalSettingsConfig.Enabled.PowerOCR)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Text Extractor", Tag = "PowerOCR", ToolTip = SettingsRepository<PowerOcrSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut.ToString(), Icon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerOcr.png" });
            }

            if (generalSettingsConfig.Enabled.MeasureTool)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Screen Ruler", Tag = "MeasureTool", ToolTip = SettingsRepository<MeasureToolSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut.ToString(), Icon = "ms-appx:///Assets/FluentIcons/FluentIconsScreenRuler.png" });
            }

            if (generalSettingsConfig.Enabled.ShortcutGuide)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = "Shortcut Guide", Tag = "ShortcutGuide", ToolTip = SettingsRepository<ShortcutGuideSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.OpenShortcutGuide.ToString(), Icon = "ms-appx:///Assets/FluentIcons/FluentIconsShortcutGuide.png" });
            }

            if (updatingSettingsConfig == null)
            {
                updatingSettingsConfig = new UpdatingSettings();
            }

            updatingSettingsConfig = UpdatingSettings.LoadSettings();

            if (updatingSettingsConfig.State == UpdatingSettings.UpdatingState.ReadyToInstall || updatingSettingsConfig.State == UpdatingSettings.UpdatingState.ReadyToDownload)
            {
                IsUpdateAvailable = true;
            }
            else
            {
                IsUpdateAvailable = false;
            }
        }
    }
}
