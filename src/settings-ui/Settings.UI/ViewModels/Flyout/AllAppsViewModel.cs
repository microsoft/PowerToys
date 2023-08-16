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
            if ((gpo = GPOWrapper.GetConfiguredAlwaysOnTopEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("AlwaysOnTop/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.AlwaysOnTop, Tag = "AlwaysOnTop", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsAlwaysOnTop.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredAwakeEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("Awake/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.Awake, Tag = "Awake", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsAwake.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredColorPickerEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("ColorPicker/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.ColorPicker, Tag = "ColorPicker", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsColorPicker.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredCropAndLockEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("CropAndLock/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.CropAndLock, Tag = "CropAndLock", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsCropAndLock.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredFancyZonesEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("FancyZones/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.FancyZones, Tag = "FancyZones", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsFancyZones.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredFileLocksmithEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("FileLocksmith/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.FileLocksmith, Tag = "FileLocksmith", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsFileLocksmith.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredFindMyMouseEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("MouseUtils_FindMyMouse/Header"), IsEnabled = generalSettingsConfig.Enabled.FindMyMouse, Tag = "FindMyMouse", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsFindMyMouse.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredHostsFileEditorEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("Hosts/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.Hosts, Tag = "Hosts", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsHosts.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredImageResizerEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("ImageResizer/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.ImageResizer, Tag = "ImageResizer", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsImageResizer.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredKeyboardManagerEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("KeyboardManager/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.KeyboardManager, Tag = "KeyboardManager", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsKeyboardManager.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredMouseHighlighterEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("MouseUtils_MouseHighlighter/Header"), IsEnabled = generalSettingsConfig.Enabled.MouseHighlighter, Tag = "MouseHighlighter", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsMouseHighlighter.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredMouseJumpEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("MouseUtils_MouseJump/Header"), IsEnabled = generalSettingsConfig.Enabled.MouseJump, Tag = "MouseJump", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsMouseJump.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredMousePointerCrosshairsEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("MouseUtils_MousePointerCrosshairs/Header"), IsEnabled = generalSettingsConfig.Enabled.MousePointerCrosshairs, Tag = "MousePointerCrosshairs", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsMouseCrosshairs.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredMouseWithoutBordersEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("MouseWithoutBorders/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.MouseWithoutBorders, Tag = "MouseWithoutBorders", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsMouseWithoutBorders.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredPastePlainEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("PastePlain/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.PastePlain, Tag = "PastePlain", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPastePlain.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredPeekEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("Peek/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.Peek, Tag = "Peek", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPeek.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredPowerRenameEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("PowerRename/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.PowerRename, Tag = "PowerRename", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPowerRename.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredPowerLauncherEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("PowerLauncher/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.PowerLauncher, Tag = "PowerLauncher", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPowerToysRun.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredQuickAccentEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("QuickAccent/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.PowerAccent, Tag = "PowerAccent", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPowerAccent.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredRegistryPreviewEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("RegistryPreview/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.RegistryPreview, Tag = "RegistryPreview", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsRegistryPreview.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredScreenRulerEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("MeasureTool/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.MeasureTool, Tag = "MeasureTool", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsScreenRuler.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredShortcutGuideEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("ShortcutGuide/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.ShortcutGuide, Tag = "ShortcutGuide", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsShortcutGuide.png", EnabledChangedCallback = EnabledChangedOnUI });
            }

            if ((gpo = GPOWrapper.GetConfiguredTextExtractorEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                FlyoutMenuItems.Add(new FlyoutMenuItem() { Label = resourceLoader.GetString("TextExtractor/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.PowerOCR, Tag = "PowerOCR", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPowerOCR.png", EnabledChangedCallback = EnabledChangedOnUI });
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
                    case "CropAndLock": item.IsEnabled = generalSettingsConfig.Enabled.CropAndLock; break;
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
