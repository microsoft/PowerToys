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

            var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
            GpoRuleConfigured gpo;
            gpo = GPOWrapper.GetConfiguredAlwaysOnTopEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("AlwaysOnTop/ModuleTitle"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.AlwaysOnTop),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "AlwaysOnTop",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsAlwaysOnTop.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredAwakeEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("Awake/ModuleTitle"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.Awake),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "Awake",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsAwake.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredColorPickerEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("ColorPicker/ModuleTitle"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.ColorPicker),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "ColorPicker",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsColorPicker.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredCropAndLockEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("CropAndLock/ModuleTitle"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.CropAndLock),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "CropAndLock",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsCropAndLock.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredEnvironmentVariablesEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("EnvironmentVariables/ModuleTitle"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.EnvironmentVariables),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "EnvironmentVariables",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsEnvironmentVariables.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredFancyZonesEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("FancyZones/ModuleTitle"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.FancyZones),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "FancyZones",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsFancyZones.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredFileLocksmithEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("FileLocksmith/ModuleTitle"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.FileLocksmith),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "FileLocksmith",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsFileLocksmith.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredFindMyMouseEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("MouseUtils_FindMyMouse/Header"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.FindMyMouse),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "FindMyMouse",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsFindMyMouse.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredHostsFileEditorEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("Hosts/ModuleTitle"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.Hosts),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "Hosts",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsHosts.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredImageResizerEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("ImageResizer/ModuleTitle"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.ImageResizer),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "ImageResizer",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsImageResizer.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredKeyboardManagerEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("KeyboardManager/ModuleTitle"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.KeyboardManager),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "KeyboardManager",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsKeyboardManager.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredMouseHighlighterEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("MouseUtils_MouseHighlighter/Header"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.MouseHighlighter),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "MouseHighlighter",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsMouseHighlighter.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredMouseJumpEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("MouseUtils_MouseJump/Header"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.MouseJump),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "MouseJump",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsMouseJump.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredMousePointerCrosshairsEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("MouseUtils_MousePointerCrosshairs/Header"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.MousePointerCrosshairs),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "MousePointerCrosshairs",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsMouseCrosshairs.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredMouseWithoutBordersEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("MouseWithoutBorders/ModuleTitle"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.MouseWithoutBorders),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "MouseWithoutBorders",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsMouseWithoutBorders.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredPastePlainEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("PastePlain/ModuleTitle"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.PastePlain),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "PastePlain",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPastePlain.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredPeekEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("Peek/ModuleTitle"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.Peek),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "Peek",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPeek.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredPowerRenameEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("PowerRename/ModuleTitle"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.PowerRename),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "PowerRename",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPowerRename.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredPowerLauncherEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("PowerLauncher/ModuleTitle"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.PowerLauncher),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "PowerLauncher",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPowerToysRun.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredQuickAccentEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("QuickAccent/ModuleTitle"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.PowerAccent),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "PowerAccent",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPowerAccent.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredRegistryPreviewEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("RegistryPreview/ModuleTitle"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.RegistryPreview),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "RegistryPreview",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsRegistryPreview.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredScreenRulerEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("MeasureTool/ModuleTitle"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.MeasureTool),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "MeasureTool",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsScreenRuler.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredShortcutGuideEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("ShortcutGuide/ModuleTitle"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.ShortcutGuide),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "ShortcutGuide",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsShortcutGuide.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

            gpo = GPOWrapper.GetConfiguredTextExtractorEnabledValue();
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString("TextExtractor/ModuleTitle"),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.PowerOCR),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = "PowerOCR",
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPowerOCR.png",
                EnabledChangedCallback = EnabledChangedOnUI,
            });

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
