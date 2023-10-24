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
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class AllAppsViewModel : Observable
    {
        public ObservableCollection<FlyoutMenuItem> FlyoutMenuItems { get; set; }

        private ISettingsRepository<GeneralSettings> _settingsRepository;
        private GeneralSettings generalSettingsConfig;
        private ResourceLoader resourceLoader;

        private Func<string, int> SendConfigMSG { get; }

        public AllAppsViewModel(ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            _settingsRepository = settingsRepository;
            generalSettingsConfig = settingsRepository.SettingsConfig;
            generalSettingsConfig.AddEnabledModuleChangeNotification(ModuleEnabledChangedOnSettingsPage);

            FlyoutMenuItems = new ObservableCollection<FlyoutMenuItem>();

            resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;

            AddFlyoutMenuItem("AlwaysOnTop", generalSettingsConfig.Enabled.AlwaysOnTop, "AlwaysOnTop/ModuleTitle", "AlwaysOnTop");
            AddFlyoutMenuItem("Awake", generalSettingsConfig.Enabled.Awake, "Awake/ModuleTitle", "Awake");
            AddFlyoutMenuItem("ColorPicker", generalSettingsConfig.Enabled.ColorPicker, "ColorPicker/ModuleTitle", "ColorPicker");
            AddFlyoutMenuItem("CropAndLock", generalSettingsConfig.Enabled.CropAndLock, "CropAndLock/ModuleTitle", "CropAndLock");
            AddFlyoutMenuItem("EnvironmentVariables", generalSettingsConfig.Enabled.EnvironmentVariables, "EnvironmentVariables/ModuleTitle", "EnvironmentVariables");
            AddFlyoutMenuItem("FancyZones", generalSettingsConfig.Enabled.FancyZones, "FancyZones/ModuleTitle", "FancyZones");
            AddFlyoutMenuItem("FileLocksmith", generalSettingsConfig.Enabled.FileLocksmith, "FileLocksmith/ModuleTitle", "FileLocksmith");
            AddFlyoutMenuItem("FindMyMouse", generalSettingsConfig.Enabled.FindMyMouse, "MouseUtils_FindMyMouse/Header", "FindMyMouse");
            AddFlyoutMenuItem("Hosts", generalSettingsConfig.Enabled.Hosts, "Hosts/ModuleTitle", "Hosts");
            AddFlyoutMenuItem("ImageResizer", generalSettingsConfig.Enabled.ImageResizer, "ImageResizer/ModuleTitle", "ImageResizer");
            AddFlyoutMenuItem("KeyboardManager", generalSettingsConfig.Enabled.KeyboardManager, "KeyboardManager/ModuleTitle", "KeyboardManager");
            AddFlyoutMenuItem("MouseHighlighter", generalSettingsConfig.Enabled.MouseHighlighter, "MouseUtils_MouseHighlighter/Header", "MouseHighlighter");
            AddFlyoutMenuItem("MouseJump", generalSettingsConfig.Enabled.MouseJump, "MouseUtils_MouseJump/Header", "MouseJump");
            AddFlyoutMenuItem("MousePointerCrosshairs", generalSettingsConfig.Enabled.MousePointerCrosshairs, "MouseUtils_MousePointerCrosshairs/Header", "MouseCrosshairs");
            AddFlyoutMenuItem("MouseWithoutBorders", generalSettingsConfig.Enabled.MouseWithoutBorders, "MouseWithoutBorders/ModuleTitle", "MouseWithoutBorders");
            AddFlyoutMenuItem("PastePlain", generalSettingsConfig.Enabled.PastePlain, "PastePlain/ModuleTitle", "PastePlain");
            AddFlyoutMenuItem("Peek", generalSettingsConfig.Enabled.Peek, "Peek/ModuleTitle", "Peek");
            AddFlyoutMenuItem("PowerRename", generalSettingsConfig.Enabled.PowerRename, "PowerRename/ModuleTitle", "PowerRename");
            AddFlyoutMenuItem("PowerLauncher", generalSettingsConfig.Enabled.PowerLauncher, "PowerLauncher/ModuleTitle", "PowerToysRun");
            AddFlyoutMenuItem("PowerAccent", generalSettingsConfig.Enabled.PowerAccent, "QuickAccent/ModuleTitle", "PowerAccent");
            AddFlyoutMenuItem("RegistryPreview", generalSettingsConfig.Enabled.RegistryPreview, "RegistryPreview/ModuleTitle", "RegistryPreview");
            AddFlyoutMenuItem("MeasureTool", generalSettingsConfig.Enabled.MeasureTool, "MeasureTool/ModuleTitle", "ScreenRuler");
            AddFlyoutMenuItem("ShortcutGuide", generalSettingsConfig.Enabled.ShortcutGuide, "ShortcutGuide/ModuleTitle", "ShortcutGuide");
            AddFlyoutMenuItem("PowerOCR", generalSettingsConfig.Enabled.PowerOCR, "TextExtractor/ModuleTitle", "PowerOCR");

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        private void AddFlyoutMenuItem(string moduleName, bool isModuleEnabled, string moduleLabelResourceName, string moduleFluentIconName)
        {
            GpoRuleConfigured gpo = GetModuleGpoConfiguration(moduleName);

            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString(moduleLabelResourceName),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && isModuleEnabled),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = moduleName,
                Icon = $"ms-appx:///Assets/Settings/FluentIcons/FluentIcons{moduleFluentIconName}.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });
        }

        private GpoRuleConfigured GetModuleGpoConfiguration(string moduleName)
        {
            switch (moduleName)
            {
                case "AlwaysOnTop": return GPOWrapper.GetConfiguredAlwaysOnTopEnabledValue();
                case "Awake": return GPOWrapper.GetConfiguredAwakeEnabledValue();
                case "ColorPicker": return GPOWrapper.GetConfiguredColorPickerEnabledValue();
                case "CropAndLock": return GPOWrapper.GetConfiguredCropAndLockEnabledValue();
                case "EnvironmentVariables": return GPOWrapper.GetConfiguredEnvironmentVariablesEnabledValue();
                case "FancyZones": return GPOWrapper.GetConfiguredFancyZonesEnabledValue();
                case "FileLocksmith": return GPOWrapper.GetConfiguredFileLocksmithEnabledValue();
                case "FindMyMouse": return GPOWrapper.GetConfiguredFindMyMouseEnabledValue();
                case "Hosts": return GPOWrapper.GetConfiguredHostsFileEditorEnabledValue();
                case "ImageResizer": return GPOWrapper.GetConfiguredImageResizerEnabledValue();
                case "KeyboardManager": return GPOWrapper.GetConfiguredKeyboardManagerEnabledValue();
                case "MouseHighlighter": return GPOWrapper.GetConfiguredMouseHighlighterEnabledValue();
                case "MouseJump": return GPOWrapper.GetConfiguredMouseJumpEnabledValue();
                case "MousePointerCrosshairs": return GPOWrapper.GetConfiguredMousePointerCrosshairsEnabledValue();
                case "MouseWithoutBorders": return GPOWrapper.GetConfiguredMouseWithoutBordersEnabledValue();
                case "PastePlain": return GPOWrapper.GetConfiguredPastePlainEnabledValue();
                case "Peek": return GPOWrapper.GetConfiguredPeekEnabledValue();
                case "PowerRename": return GPOWrapper.GetConfiguredPowerRenameEnabledValue();
                case "PowerLauncher": return GPOWrapper.GetConfiguredPowerLauncherEnabledValue();
                case "PowerAccent": return GPOWrapper.GetConfiguredQuickAccentEnabledValue();
                case "RegistryPreview": return GPOWrapper.GetConfiguredRegistryPreviewEnabledValue();
                case "MeasureTool": return GPOWrapper.GetConfiguredScreenRulerEnabledValue();
                case "ShortcutGuide": return GPOWrapper.GetConfiguredShortcutGuideEnabledValue();
                case "PowerOCR": return GPOWrapper.GetConfiguredTextExtractorEnabledValue();
                default: return GpoRuleConfigured.Unavailable;
            }
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
                    case "CropAndLock": item.IsEnabled = generalSettingsConfig.Enabled.CropAndLock; break;
                    case "EnvironmentVariables": item.IsEnabled = generalSettingsConfig.Enabled.EnvironmentVariables; break;
                    case "FancyZones": item.IsEnabled = generalSettingsConfig.Enabled.FancyZones; break;
                    case "FileLocksmith": item.IsEnabled = generalSettingsConfig.Enabled.FileLocksmith; break;
                    case "FindMyMouse": item.IsEnabled = generalSettingsConfig.Enabled.FindMyMouse; break;
                    case "Hosts": item.IsEnabled = generalSettingsConfig.Enabled.Hosts; break;
                    case "ImageResizer": item.IsEnabled = generalSettingsConfig.Enabled.ImageResizer; break;
                    case "KeyboardManager": item.IsEnabled = generalSettingsConfig.Enabled.KeyboardManager; break;
                    case "MouseHighlighter": item.IsEnabled = generalSettingsConfig.Enabled.MouseHighlighter; break;
                    case "MouseJump": item.IsEnabled = generalSettingsConfig.Enabled.MouseJump; break;
                    case "MousePointerCrosshairs": item.IsEnabled = generalSettingsConfig.Enabled.MousePointerCrosshairs; break;
                    case "MouseWithoutBorders": item.IsEnabled = generalSettingsConfig.Enabled.MouseWithoutBorders; break;
                    case "PastePlain": item.IsEnabled = generalSettingsConfig.Enabled.PastePlain; break;
                    case "Peek": item.IsEnabled = generalSettingsConfig.Enabled.Peek; break;
                    case "PowerRename": item.IsEnabled = generalSettingsConfig.Enabled.PowerRename; break;
                    case "PowerLauncher": item.IsEnabled = generalSettingsConfig.Enabled.PowerLauncher; break;
                    case "PowerAccent": item.IsEnabled = generalSettingsConfig.Enabled.PowerAccent; break;
                    case "RegistryPreview": item.IsEnabled = generalSettingsConfig.Enabled.RegistryPreview; break;
                    case "MeasureTool": item.IsEnabled = generalSettingsConfig.Enabled.MeasureTool; break;
                    case "ShortcutGuide": item.IsEnabled = generalSettingsConfig.Enabled.ShortcutGuide; break;
                    case "PowerOCR": item.IsEnabled = generalSettingsConfig.Enabled.PowerOCR; break;
                    case "VideoConference": item.IsEnabled = generalSettingsConfig.Enabled.VideoConference; break;
                }
            }
        }
    }
}
