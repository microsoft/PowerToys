// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Flyout;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Windows.ApplicationModel.Resources;

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

            FlyoutMenuItems = new ObservableCollection<FlyoutMenuItem>();

            ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();

            if (GPOWrapper.GetConfiguredAlwaysOnTopEnabledValue() != GpoRuleConfigured.Disabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("AlwaysOnTop/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.AlwaysOnTop, Tag = "AlwaysOnTop", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsAlwaysOnTop.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if (GPOWrapper.GetConfiguredAwakeEnabledValue() != GpoRuleConfigured.Disabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("Awake/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.Awake, Tag = "Awake", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsAwake.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if (GPOWrapper.GetConfiguredColorPickerEnabledValue() != GpoRuleConfigured.Disabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("ColorPicker/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.ColorPicker, Tag = "ColorPicker", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsColorPicker.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if (GPOWrapper.GetConfiguredFancyZonesEnabledValue() != GpoRuleConfigured.Disabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("FancyZones/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.FancyZones, Tag = "FancyZones", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsFancyZones.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if (GPOWrapper.GetConfiguredFileLocksmithEnabledValue() != GpoRuleConfigured.Disabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("FileLocksmith/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.FileLocksmith, Tag = "FileLocksmith", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsFileLocksmith.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if (GPOWrapper.GetConfiguredFindMyMouseEnabledValue() != GpoRuleConfigured.Disabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("MouseUtils_FindMyMouse/Header"), IsEnabled = generalSettingsConfig.Enabled.FindMyMouse, Tag = "FindMyMouse", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsFindMyMouse.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if (GPOWrapper.GetConfiguredHostsFileEditorEnabledValue() != GpoRuleConfigured.Disabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("Hosts/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.Hosts, Tag = "Hosts", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsHosts.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if (GPOWrapper.GetConfiguredImageResizerEnabledValue() != GpoRuleConfigured.Disabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("ImageResizer/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.ImageResizer, Tag = "ImageResizer", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsImageResizer.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if (GPOWrapper.GetConfiguredKeyboardManagerEnabledValue() != GpoRuleConfigured.Disabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("KeyboardManager/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.KeyboardManager, Tag = "KeyboardManager", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsKeyboardManager.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if (GPOWrapper.GetConfiguredMouseHighlighterEnabledValue() != GpoRuleConfigured.Disabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("MouseUtils_MouseHighlighter/Header"), IsEnabled = generalSettingsConfig.Enabled.MouseHighlighter, Tag = "MouseHighlighter", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsMouseHighlighter.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if (GPOWrapper.GetConfiguredMousePointerCrosshairsEnabledValue() != GpoRuleConfigured.Disabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("MouseUtils_MousePointerCrosshairs/Header"), IsEnabled = generalSettingsConfig.Enabled.MousePointerCrosshairs, Tag = "MousePointerCrosshairs", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsMouseCrosshairs.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if (GPOWrapper.GetConfiguredPowerRenameEnabledValue() != GpoRuleConfigured.Disabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("PowerRename/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.PowerRename, Tag = "PowerRename", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerRename.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if (GPOWrapper.GetConfiguredPowerLauncherEnabledValue() != GpoRuleConfigured.Disabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("PowerLauncher/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.PowerLauncher, Tag = "PowerLauncher", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerToysRun.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if (GPOWrapper.GetConfiguredQuickAccentEnabledValue() != GpoRuleConfigured.Disabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("QuickAccent/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.PowerAccent, Tag = "PowerAccent", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerAccent.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if (GPOWrapper.GetConfiguredScreenRulerEnabledValue() != GpoRuleConfigured.Disabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("MeasureTool/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.MeasureTool, Tag = "MeasureTool", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsScreenRuler.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if (GPOWrapper.GetConfiguredShortcutGuideEnabledValue() != GpoRuleConfigured.Disabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("ShortcutGuide/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.ShortcutGuide, Tag = "ShortcutGuide", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsShortcutGuide.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if (GPOWrapper.GetConfiguredTextExtractorEnabledValue() != GpoRuleConfigured.Disabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("TextExtractor/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.PowerOCR, Tag = "PowerOCR", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerOCR.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        private void EnabledChangedOnUI(FlyoutMenuItem flyoutMenuItem)
        {
            if (Views.ShellPage.UpdateGeneralSettingsCallback(flyoutMenuItem.Tag, flyoutMenuItem.IsEnabled))
            {
                Views.ShellPage.DisableFlyoutHidingCallback();
            }
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
