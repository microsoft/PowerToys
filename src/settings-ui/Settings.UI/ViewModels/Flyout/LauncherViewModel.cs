// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.UI.Xaml;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class LauncherViewModel : Observable
    {
        public bool IsUpdateAvailable { get; set; }

        public ObservableCollection<FlyoutMenuItem> FlyoutMenuItems { get; set; }

        private GeneralSettings generalSettingsConfig;
        private UpdatingSettings updatingSettingsConfig;
        private ISettingsRepository<GeneralSettings> _settingsRepository;

        public LauncherViewModel(ISettingsRepository<GeneralSettings> settingsRepository)
        {
            _settingsRepository = settingsRepository;
            generalSettingsConfig = settingsRepository.SettingsConfig;
            generalSettingsConfig.AddEnabledModuleChangeNotification(ModuleEnabledChanged);

            FlyoutMenuItems = new ObservableCollection<FlyoutMenuItem>()
            {
                new FlyoutMenuItem()
                {
                    Label = "ColorPicker",
                    Tag = "ColorPicker",
                    Visible = generalSettingsConfig.Enabled.ColorPicker,
                    ToolTip = SettingsRepository<ColorPickerSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut.ToString(),
                    Icon = "ms-appx:///Assets/FluentIcons/FluentIconsColorPicker.png",
                },
                new FlyoutMenuItem()
                {
                    Label = "FancyZones Editor",
                    Tag = "FancyZones",
                    Visible = generalSettingsConfig.Enabled.FancyZones,
                    ToolTip = SettingsRepository<FancyZonesSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.FancyzonesEditorHotkey.Value.ToString(),
                    Icon = "ms-appx:///Assets/FluentIcons/FluentIconsFancyZones.png",
                },
                new FlyoutMenuItem()
                {
                    Label = "Hosts File Editor",
                    Tag = "Hosts",
                    Visible = generalSettingsConfig.Enabled.Hosts,
                    Icon = "ms-appx:///Assets/FluentIcons/FluentIconsHosts.png",
                },
                new FlyoutMenuItem()
                {
                    Label = "PowerToys Run",
                    Tag = "PowerLauncher",
                    Visible = generalSettingsConfig.Enabled.PowerLauncher,
                    ToolTip = SettingsRepository<PowerLauncherSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.OpenPowerLauncher.ToString(),
                    Icon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerToysRun.png",
                },
                new FlyoutMenuItem()
                {
                    Label = "Text Extractor",
                    Tag = "PowerOCR",
                    Visible = generalSettingsConfig.Enabled.PowerOCR,
                    ToolTip = SettingsRepository<PowerOcrSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut.ToString(),
                    Icon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerOcr.png",
                },
                new FlyoutMenuItem()
                {
                    Label = "Screen Ruler",
                    Tag = "MeasureTool",
                    Visible = generalSettingsConfig.Enabled.MeasureTool,
                    ToolTip = SettingsRepository<MeasureToolSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut.ToString(),
                    Icon = "ms-appx:///Assets/FluentIcons/FluentIconsScreenRuler.png",
                },
                new FlyoutMenuItem()
                {
                    Label = "Shortcut Guide",
                    Tag = "ShortcutGuide",
                    Visible = generalSettingsConfig.Enabled.ShortcutGuide,
                    ToolTip = SettingsRepository<ShortcutGuideSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.OpenShortcutGuide.ToString(),
                    Icon = "ms-appx:///Assets/FluentIcons/FluentIconsShortcutGuide.png",
                },
            };

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

        private void ModuleEnabledChanged()
        {
            generalSettingsConfig = _settingsRepository.SettingsConfig;
            generalSettingsConfig.AddEnabledModuleChangeNotification(ModuleEnabledChanged);
            foreach (FlyoutMenuItem item in FlyoutMenuItems)
            {
                switch (item.Tag)
                {
                    case "ColorPicker": item.Visible = generalSettingsConfig.Enabled.ColorPicker; break;
                    case "FancyZones": item.Visible = generalSettingsConfig.Enabled.FancyZones; break;
                    case "Hosts": item.Visible = generalSettingsConfig.Enabled.Hosts; break;
                    case "PowerLauncher": item.Visible = generalSettingsConfig.Enabled.PowerLauncher; break;
                    case "PowerOCR": item.Visible = generalSettingsConfig.Enabled.PowerOCR; break;
                    case "MeasureTool": item.Visible = generalSettingsConfig.Enabled.MeasureTool; break;
                    case "ShortcutGuide": item.Visible = generalSettingsConfig.Enabled.ShortcutGuide; break;
                }
            }
        }
    }
}
