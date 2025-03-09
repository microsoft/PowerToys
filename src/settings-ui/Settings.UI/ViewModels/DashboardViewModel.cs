// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Abstractions;
using System.Linq;
using System.Windows.Threading;

using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class DashboardViewModel : Observable
    {
        private const string JsonFileType = ".json";
        private IFileSystemWatcher _watcher;
        private DashboardModuleKBMItem _kbmItem;
        private Dispatcher dispatcher;

        public Func<string, int> SendConfigMSG { get; }

        public ObservableCollection<DashboardListItem> ActiveModules { get; set; } = new ObservableCollection<DashboardListItem>();

        public ObservableCollection<DashboardListItem> DisabledModules { get; set; } = new ObservableCollection<DashboardListItem>();

        public bool UpdateAvailable { get; set; }

        private List<DashboardListItem> _allModules;

        private ISettingsRepository<GeneralSettings> _settingsRepository;
        private GeneralSettings generalSettingsConfig;
        private Windows.ApplicationModel.Resources.ResourceLoader resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;

        public DashboardViewModel(ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            _settingsRepository = settingsRepository;
            generalSettingsConfig = settingsRepository.SettingsConfig;
            generalSettingsConfig.AddEnabledModuleChangeNotification(ModuleEnabledChangedOnSettingsPage);

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            _allModules = new List<DashboardListItem>();

            foreach (ModuleType moduleType in Enum.GetValues<ModuleType>())
            {
                AddDashboardListItem(moduleType);
            }

            ActiveModules = new ObservableCollection<DashboardListItem>(_allModules.Where(x => x.IsEnabled));
            DisabledModules = new ObservableCollection<DashboardListItem>(_allModules.Where(x => !x.IsEnabled));

            UpdatingSettings updatingSettingsConfig = UpdatingSettings.LoadSettings();
            UpdateAvailable = updatingSettingsConfig != null && (updatingSettingsConfig.State == UpdatingSettings.UpdatingState.ReadyToInstall || updatingSettingsConfig.State == UpdatingSettings.UpdatingState.ReadyToDownload);
        }

        private void AddDashboardListItem(ModuleType moduleType)
        {
            GpoRuleConfigured gpo = ModuleHelper.GetModuleGpoConfiguration(moduleType);
            _allModules.Add(new DashboardListItem()
            {
                Tag = moduleType,
                Label = resourceLoader.GetString(ModuleHelper.GetModuleLabelResourceName(moduleType)),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && ModuleHelper.GetIsModuleEnabled(generalSettingsConfig, moduleType)),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = ModuleHelper.GetModuleTypeFluentIconName(moduleType),
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItems(moduleType),
            });
            if (moduleType == ModuleType.KeyboardManager && gpo != GpoRuleConfigured.Disabled)
            {
                KeyboardManagerSettings kbmSettings = GetKBMSettings();
                _watcher = Library.Utilities.Helper.GetFileWatcher(KeyboardManagerSettings.ModuleName, kbmSettings.Properties.ActiveConfiguration.Value + JsonFileType, () => LoadKBMSettingsFromJson());
            }
        }

        private void LoadKBMSettingsFromJson()
        {
            try
            {
                KeyboardManagerProfile kbmProfile = GetKBMProfile();
                _kbmItem.RemapKeys = kbmProfile?.RemapKeys.InProcessRemapKeys;
                _kbmItem.RemapShortcuts = KeyboardManagerViewModel.CombineShortcutLists(kbmProfile?.RemapShortcuts.GlobalRemapShortcuts, kbmProfile?.RemapShortcuts.AppSpecificRemapShortcuts);
                dispatcher.Invoke(new Action(() => UpdateKBMItems()));
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load KBM settings: {ex.Message}");
            }
        }

        private void UpdateKBMItems()
        {
            _kbmItem.NotifyPropertyChanged(nameof(_kbmItem.RemapKeys));
            _kbmItem.NotifyPropertyChanged(nameof(_kbmItem.RemapShortcuts));
        }

        private KeyboardManagerProfile GetKBMProfile()
        {
            KeyboardManagerSettings kbmSettings = GetKBMSettings();
            const string PowerToyName = KeyboardManagerSettings.ModuleName;
            string fileName = kbmSettings.Properties.ActiveConfiguration.Value + JsonFileType;
            return new SettingsUtils().GetSettingsOrDefault<KeyboardManagerProfile>(PowerToyName, fileName);
        }

        private KeyboardManagerSettings GetKBMSettings()
        {
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<KeyboardManagerSettings> moduleSettingsRepository = SettingsRepository<KeyboardManagerSettings>.GetInstance(settingsUtils);
            return moduleSettingsRepository.SettingsConfig;
        }

        private void EnabledChangedOnUI(DashboardListItem dashboardListItem)
        {
            Views.ShellPage.UpdateGeneralSettingsCallback(dashboardListItem.Tag, dashboardListItem.IsEnabled);

            if (dashboardListItem.Tag == ModuleType.NewPlus && dashboardListItem.IsEnabled == true)
            {
                var settingsUtils = new SettingsUtils();
                var settings = NewPlusViewModel.LoadSettings(settingsUtils);
                NewPlusViewModel.CopyTemplateExamples(settings.Properties.TemplateLocation.Value);
            }
        }

        public void ModuleEnabledChangedOnSettingsPage()
        {
            try
            {
                ActiveModules.Clear();
                DisabledModules.Clear();

                generalSettingsConfig = _settingsRepository.SettingsConfig;
                foreach (DashboardListItem item in _allModules)
                {
                    item.IsEnabled = ModuleHelper.GetIsModuleEnabled(generalSettingsConfig, item.Tag);
                    if (item.IsEnabled)
                    {
                        ActiveModules.Add(item);
                    }
                    else
                    {
                        DisabledModules.Add(item);
                    }
                }

                OnPropertyChanged(nameof(ActiveModules));
                OnPropertyChanged(nameof(DisabledModules));
            }
            catch (Exception ex)
            {
                Logger.LogError($"Updating active/disabled modules list failed: {ex.Message}");
            }
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItems(ModuleType moduleType)
        {
            return moduleType switch
            {
                ModuleType.AdvancedPaste => GetModuleItemsAdvancedPaste(),
                ModuleType.AlwaysOnTop => GetModuleItemsAlwaysOnTop(),
                ModuleType.Awake => GetModuleItemsAwake(),
                ModuleType.ColorPicker => GetModuleItemsColorPicker(),
                ModuleType.CropAndLock => GetModuleItemsCropAndLock(),
                ModuleType.EnvironmentVariables => GetModuleItemsEnvironmentVariables(),
                ModuleType.FancyZones => GetModuleItemsFancyZones(),
                ModuleType.FileLocksmith => GetModuleItemsFileLocksmith(),
                ModuleType.FindMyMouse => GetModuleItemsFindMyMouse(),
                ModuleType.Hosts => GetModuleItemsHosts(),
                ModuleType.ImageResizer => GetModuleItemsImageResizer(),
                ModuleType.KeyboardManager => GetModuleItemsKeyboardManager(),
                ModuleType.MouseHighlighter => GetModuleItemsMouseHighlighter(),
                ModuleType.MouseJump => GetModuleItemsMouseJump(),
                ModuleType.MousePointerCrosshairs => GetModuleItemsMousePointerCrosshairs(),
                ModuleType.MouseWithoutBorders => GetModuleItemsMouseWithoutBorders(),
                ModuleType.Peek => GetModuleItemsPeek(),
                ModuleType.PowerRename => GetModuleItemsPowerRename(),
                ModuleType.PowerLauncher => GetModuleItemsPowerLauncher(),
                ModuleType.PowerAccent => GetModuleItemsPowerAccent(),
                ModuleType.Workspaces => GetModuleItemsWorkspaces(),
                ModuleType.RegistryPreview => GetModuleItemsRegistryPreview(),
                ModuleType.MeasureTool => GetModuleItemsMeasureTool(),
                ModuleType.ShortcutGuide => GetModuleItemsShortcutGuide(),
                ModuleType.PowerOCR => GetModuleItemsPowerOCR(),
                ModuleType.NewPlus => GetModuleItemsNewPlus(),
                ModuleType.ZoomIt => GetModuleItemsZoomIt(),
                _ => new ObservableCollection<DashboardModuleItem>(), // never called, all values listed above
            };
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsAlwaysOnTop()
        {
            ISettingsRepository<AlwaysOnTopSettings> moduleSettingsRepository = SettingsRepository<AlwaysOnTopSettings>.GetInstance(new SettingsUtils());
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("AlwaysOnTop_ShortDescription"), Shortcut = moduleSettingsRepository.SettingsConfig.Properties.Hotkey.Value.GetKeysList() },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsAwake()
        {
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleTextItem() { Label = resourceLoader.GetString("Awake_ShortDescription") },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsColorPicker()
        {
            ISettingsRepository<ColorPickerSettings> moduleSettingsRepository = SettingsRepository<ColorPickerSettings>.GetInstance(new SettingsUtils());
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.ActivationShortcut;
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("ColorPicker_ShortDescription"), Shortcut = hotkey.GetKeysList() },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsCropAndLock()
        {
            ISettingsRepository<CropAndLockSettings> moduleSettingsRepository = SettingsRepository<CropAndLockSettings>.GetInstance(new SettingsUtils());
            var settings = moduleSettingsRepository.SettingsConfig;
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("CropAndLock_Thumbnail"), Shortcut = settings.Properties.ThumbnailHotkey.Value.GetKeysList() },
                new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("CropAndLock_Reparent"), Shortcut = settings.Properties.ReparentHotkey.Value.GetKeysList() },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsEnvironmentVariables()
        {
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleButtonItem() { ButtonTitle = resourceLoader.GetString("EnvironmentVariables_LaunchButtonControl/Header"), IsButtonDescriptionVisible = true, ButtonDescription = resourceLoader.GetString("EnvironmentVariables_LaunchButtonControl/Description"), ButtonGlyph = "\uEA37", ButtonClickHandler = EnvironmentVariablesLaunchClicked },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsFancyZones()
        {
            ISettingsRepository<FancyZonesSettings> moduleSettingsRepository = SettingsRepository<FancyZonesSettings>.GetInstance(new SettingsUtils());
            var settings = moduleSettingsRepository.SettingsConfig;
            string activationMode = $"{resourceLoader.GetString(settings.Properties.FancyzonesShiftDrag.Value ? "FancyZones_ShiftDragCheckBoxControl_Header/Content" : "FancyZones_ActivationNoShiftDrag")}.";
            if (settings.Properties.FancyzonesMouseSwitch.Value)
            {
                activationMode += $" {resourceLoader.GetString("FancyZones_MouseDragCheckBoxControl_Header/Content")}.";
            }

            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleTextItem() { Label = activationMode },
                new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("FancyZones_OpenEditor"), Shortcut = settings.Properties.FancyzonesEditorHotkey.Value.GetKeysList() },
                new DashboardModuleButtonItem() { ButtonTitle = resourceLoader.GetString("FancyZones_LaunchEditorButtonControl/Header"), IsButtonDescriptionVisible = true, ButtonDescription = resourceLoader.GetString("FancyZones_LaunchEditorButtonControl/Description"), ButtonGlyph = "\uEB3C", ButtonClickHandler = FancyZoneLaunchClicked },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsFileLocksmith()
        {
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleTextItem() { Label = resourceLoader.GetString("FileLocksmith_ShortDescription") },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsFindMyMouse()
        {
            ISettingsRepository<FindMyMouseSettings> moduleSettingsRepository = SettingsRepository<FindMyMouseSettings>.GetInstance(new SettingsUtils());
            string shortDescription = resourceLoader.GetString("FindMyMouse_ShortDescription");
            var settings = moduleSettingsRepository.SettingsConfig;
            var activationMethod = settings.Properties.ActivationMethod.Value;
            var list = new List<DashboardModuleItem>();
            if (activationMethod == 3)
            {
                var hotkey = settings.Properties.ActivationShortcut;
                list.Add(new DashboardModuleShortcutItem() { Label = shortDescription, Shortcut = hotkey.GetKeysList() });
            }
            else
            {
                switch (activationMethod)
                {
                    case 2: shortDescription += $". {resourceLoader.GetString("Dashboard_Activation")}: {resourceLoader.GetString("MouseUtils_FindMyMouse_ActivationShakeMouse/Content")}"; break;
                    case 1: shortDescription += $". {resourceLoader.GetString("Dashboard_Activation")}: {resourceLoader.GetString("MouseUtils_FindMyMouse_ActivationDoubleRightControlPress/Content")}"; break;
                    case 0:
                    default: shortDescription += $". {resourceLoader.GetString("Dashboard_Activation")}: {resourceLoader.GetString("MouseUtils_FindMyMouse_ActivationDoubleControlPress/Content")}"; break;
                }

                list.Add(new DashboardModuleTextItem() { Label = shortDescription });
            }

            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsHosts()
        {
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleButtonItem() { ButtonTitle = resourceLoader.GetString("Hosts_LaunchButtonControl/Header"), IsButtonDescriptionVisible = true, ButtonDescription = resourceLoader.GetString("Hosts_LaunchButtonControl/Description"), ButtonGlyph = "\uEA37", ButtonClickHandler = HostLaunchClicked },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsImageResizer()
        {
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleTextItem() { Label = resourceLoader.GetString("ImageResizer_ShortDescription") },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsKeyboardManager()
        {
            KeyboardManagerProfile kbmProfile = GetKBMProfile();
            _kbmItem = new DashboardModuleKBMItem() { RemapKeys = kbmProfile?.RemapKeys.InProcessRemapKeys, RemapShortcuts = KeyboardManagerViewModel.CombineShortcutLists(kbmProfile?.RemapShortcuts.GlobalRemapShortcuts, kbmProfile?.RemapShortcuts.AppSpecificRemapShortcuts) };

            _kbmItem.RemapKeys = _kbmItem.RemapKeys.Concat(kbmProfile?.RemapKeysToText.InProcessRemapKeys).ToList();

            var shortcutsToTextRemappings = KeyboardManagerViewModel.CombineShortcutLists(kbmProfile?.RemapShortcutsToText.GlobalRemapShortcuts, kbmProfile?.RemapShortcutsToText.AppSpecificRemapShortcuts);

            _kbmItem.RemapShortcuts = _kbmItem.RemapShortcuts.Concat(shortcutsToTextRemappings).ToList();

            var list = new List<DashboardModuleItem>
            {
                _kbmItem,
                new DashboardModuleButtonItem() { ButtonTitle = resourceLoader.GetString("KeyboardManager_RemapKeyboardButton/Header"), IsButtonDescriptionVisible = true, ButtonDescription = resourceLoader.GetString("KeyboardManager_RemapKeyboardButton/Description"), ButtonGlyph = "\uE92E", ButtonClickHandler = KbmKeyLaunchClicked },
                new DashboardModuleButtonItem() { ButtonTitle = resourceLoader.GetString("KeyboardManager_RemapShortcutsButton/Header"), IsButtonDescriptionVisible = true, ButtonDescription = resourceLoader.GetString("KeyboardManager_RemapShortcutsButton/Description"), ButtonGlyph = "\uE92E", ButtonClickHandler = KbmShortcutLaunchClicked },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsMouseHighlighter()
        {
            ISettingsRepository<MouseHighlighterSettings> moduleSettingsRepository = SettingsRepository<MouseHighlighterSettings>.GetInstance(new SettingsUtils());
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("MouseHighlighter_ShortDescription"), Shortcut = moduleSettingsRepository.SettingsConfig.Properties.ActivationShortcut.GetKeysList() },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsMouseJump()
        {
            ISettingsRepository<MouseJumpSettings> moduleSettingsRepository = SettingsRepository<MouseJumpSettings>.GetInstance(new SettingsUtils());
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("MouseJump_ShortDescription"), Shortcut = moduleSettingsRepository.SettingsConfig.Properties.ActivationShortcut.GetKeysList() },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsMousePointerCrosshairs()
        {
            ISettingsRepository<MousePointerCrosshairsSettings> moduleSettingsRepository = SettingsRepository<MousePointerCrosshairsSettings>.GetInstance(new SettingsUtils());
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("MouseCrosshairs_ShortDescription"), Shortcut = moduleSettingsRepository.SettingsConfig.Properties.ActivationShortcut.GetKeysList() },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsMouseWithoutBorders()
        {
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleTextItem() { Label = resourceLoader.GetString("MouseWithoutBorders_ShortDescription") },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsAdvancedPaste()
        {
            ISettingsRepository<AdvancedPasteSettings> moduleSettingsRepository = SettingsRepository<AdvancedPasteSettings>.GetInstance(new SettingsUtils());
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("AdvancedPasteUI_Shortcut/Header"), Shortcut = moduleSettingsRepository.SettingsConfig.Properties.AdvancedPasteUIShortcut.GetKeysList() },
                new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("PasteAsPlainText_Shortcut/Header"), Shortcut = moduleSettingsRepository.SettingsConfig.Properties.PasteAsPlainTextShortcut.GetKeysList() },
            };

            if (moduleSettingsRepository.SettingsConfig.Properties.PasteAsMarkdownShortcut.GetKeysList().Count > 0)
            {
                list.Add(new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("PasteAsMarkdown_Shortcut/Header"), Shortcut = moduleSettingsRepository.SettingsConfig.Properties.PasteAsMarkdownShortcut.GetKeysList() });
            }

            if (moduleSettingsRepository.SettingsConfig.Properties.PasteAsJsonShortcut.GetKeysList().Count > 0)
            {
                list.Add(new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("PasteAsJson_Shortcut/Header"), Shortcut = moduleSettingsRepository.SettingsConfig.Properties.PasteAsJsonShortcut.GetKeysList() });
            }

            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsPeek()
        {
            ISettingsRepository<PeekSettings> moduleSettingsRepository = SettingsRepository<PeekSettings>.GetInstance(new SettingsUtils());
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("Peek_ShortDescription"), Shortcut = moduleSettingsRepository.SettingsConfig.Properties.ActivationShortcut.GetKeysList() },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsPowerRename()
        {
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleTextItem() { Label = resourceLoader.GetString("PowerRename_ShortDescription") },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsPowerLauncher()
        {
            ISettingsRepository<PowerLauncherSettings> moduleSettingsRepository = SettingsRepository<PowerLauncherSettings>.GetInstance(new SettingsUtils());
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("Run_ShortDescription"), Shortcut = moduleSettingsRepository.SettingsConfig.Properties.OpenPowerLauncher.GetKeysList() },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsPowerAccent()
        {
            string shortDescription = resourceLoader.GetString("PowerAccent_ShortDescription");
            var settingsUtils = new SettingsUtils();
            PowerAccentSettings moduleSettings = settingsUtils.GetSettingsOrDefault<PowerAccentSettings>(PowerAccentSettings.ModuleName);
            var activationMethod = moduleSettings.Properties.ActivationKey;
            switch (activationMethod)
            {
                case Library.Enumerations.PowerAccentActivationKey.LeftRightArrow: shortDescription += $". {resourceLoader.GetString("Dashboard_Activation")}: {resourceLoader.GetString("QuickAccent_Activation_Key_Arrows/Content")}"; break;
                case Library.Enumerations.PowerAccentActivationKey.Space: shortDescription += $". {resourceLoader.GetString("Dashboard_Activation")}: {resourceLoader.GetString("QuickAccent_Activation_Key_Space/Content")}"; break;
                case Library.Enumerations.PowerAccentActivationKey.Both: shortDescription += $". {resourceLoader.GetString("Dashboard_Activation")}: {resourceLoader.GetString("QuickAccent_Activation_Key_Either/Content")}"; break;
            }

            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleTextItem() { Label = shortDescription },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsWorkspaces()
        {
            ISettingsRepository<WorkspacesSettings> moduleSettingsRepository = SettingsRepository<WorkspacesSettings>.GetInstance(new SettingsUtils());
            var settings = moduleSettingsRepository.SettingsConfig;

            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("Workspaces_ShortDescription"), Shortcut = settings.Properties.Hotkey.Value.GetKeysList() },
                new DashboardModuleButtonItem() { ButtonTitle = resourceLoader.GetString("Workspaces_LaunchEditorButtonControl/Header"), IsButtonDescriptionVisible = true, ButtonDescription = resourceLoader.GetString("FancyZones_LaunchEditorButtonControl/Description"), ButtonGlyph = "\uEB3C", ButtonClickHandler = WorkspacesLaunchClicked },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsRegistryPreview()
        {
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleButtonItem() { ButtonTitle = resourceLoader.GetString("RegistryPreview_LaunchButtonControl/Header"), ButtonGlyph = "\uEA37",  ButtonClickHandler = RegistryPreviewLaunchClicked },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsMeasureTool()
        {
            ISettingsRepository<MeasureToolSettings> moduleSettingsRepository = SettingsRepository<MeasureToolSettings>.GetInstance(new SettingsUtils());
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("ScreenRuler_ShortDescription"), Shortcut = moduleSettingsRepository.SettingsConfig.Properties.ActivationShortcut.GetKeysList() },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsShortcutGuide()
        {
            ISettingsRepository<ShortcutGuideSettings> moduleSettingsRepository = SettingsRepository<ShortcutGuideSettings>.GetInstance(new SettingsUtils());

            var shortcut = moduleSettingsRepository.SettingsConfig.Properties.UseLegacyPressWinKeyBehavior.Value
                ? new List<object> { 92 } // Right Windows key code
                : moduleSettingsRepository.SettingsConfig.Properties.OpenShortcutGuide.GetKeysList();

            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("ShortcutGuide_ShortDescription"), Shortcut = shortcut },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsPowerOCR()
        {
            ISettingsRepository<PowerOcrSettings> moduleSettingsRepository = SettingsRepository<PowerOcrSettings>.GetInstance(new SettingsUtils());
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("PowerOcr_ShortDescription"), Shortcut = moduleSettingsRepository.SettingsConfig.Properties.ActivationShortcut.GetKeysList() },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsNewPlus()
        {
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleTextItem() { Label = resourceLoader.GetString("NewPlus_Product_Description/Description") },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsZoomIt()
        {
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleTextItem() { Label = resourceLoader.GetString("ZoomIt_ShortDescription") },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        internal void SWVersionButtonClicked()
        {
            NavigationService.Navigate(typeof(GeneralPage));
        }

        private void EnvironmentVariablesLaunchClicked(object sender, RoutedEventArgs e)
        {
            var settingsUtils = new SettingsUtils();
            var environmentVariablesViewModel = new EnvironmentVariablesViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), SettingsRepository<EnvironmentVariablesSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage, App.IsElevated);
            environmentVariablesViewModel.Launch();
        }

        private void HostLaunchClicked(object sender, RoutedEventArgs e)
        {
            var settingsUtils = new SettingsUtils();
            var hostsViewModel = new HostsViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), SettingsRepository<HostsSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage, App.IsElevated);
            hostsViewModel.Launch();
        }

        private void FancyZoneLaunchClicked(object sender, RoutedEventArgs e)
        {
            // send message to launch the zones editor;
            SendConfigMSG("{\"action\":{\"FancyZones\":{\"action_name\":\"ToggledFZEditor\", \"value\":\"\"}}}");
        }

        private void WorkspacesLaunchClicked(object sender, RoutedEventArgs e)
        {
            // send message to launch the Workspaces editor;
            SendConfigMSG("{\"action\":{\"Workspaces\":{\"action_name\":\"LaunchEditor\", \"value\":\"\"}}}");
        }

        private void KbmKeyLaunchClicked(object sender, RoutedEventArgs e)
        {
            var settingsUtils = new SettingsUtils();
            var kbmViewModel = new KeyboardManagerViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage, KeyboardManagerPage.FilterRemapKeysList);
            kbmViewModel.OnRemapKeyboard();
        }

        private void KbmShortcutLaunchClicked(object sender, RoutedEventArgs e)
        {
            var settingsUtils = new SettingsUtils();
            var kbmViewModel = new KeyboardManagerViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage, KeyboardManagerPage.FilterRemapKeysList);
            kbmViewModel.OnEditShortcut();
        }

        private void RegistryPreviewLaunchClicked(object sender, RoutedEventArgs e)
        {
            var actionName = "Launch";
            SendConfigMSG("{\"action\":{\"RegistryPreview\":{\"action_name\":\"" + actionName + "\", \"value\":\"\"}}}");
        }

        internal void DashboardListItemClick(object sender)
        {
            Button button = sender as Button;
            if (button == null)
            {
                return;
            }

            if (!(button.Tag is ModuleType))
            {
                return;
            }

            ModuleType moduleType = (ModuleType)button.Tag;

            NavigationService.Navigate(ModuleHelper.GetModulePageType(moduleType));
        }
    }
}
