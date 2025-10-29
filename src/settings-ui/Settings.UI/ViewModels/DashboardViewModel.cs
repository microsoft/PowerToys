// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.WinUI.Controls;
using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class DashboardViewModel : PageViewModelBase
    {
        protected override string ModuleName => "Dashboard";

        private Dispatcher dispatcher;

        public Func<string, int> SendConfigMSG { get; }

        public ObservableCollection<DashboardListItem> AllModules { get; set; } = new ObservableCollection<DashboardListItem>();

        public ObservableCollection<DashboardListItem> ShortcutModules { get; set; } = new ObservableCollection<DashboardListItem>();

        public ObservableCollection<DashboardListItem> ActionModules { get; set; } = new ObservableCollection<DashboardListItem>();

        private AllHotkeyConflictsData _allHotkeyConflictsData = new AllHotkeyConflictsData();

        public AllHotkeyConflictsData AllHotkeyConflictsData
        {
            get => _allHotkeyConflictsData;
            set
            {
                if (Set(ref _allHotkeyConflictsData, value))
                {
                    OnPropertyChanged();
                }
            }
        }

        public string PowerToysVersion
        {
            get
            {
                return Helper.GetProductVersion();
            }
        }

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

            foreach (ModuleType moduleType in Enum.GetValues<ModuleType>())
            {
                AddDashboardListItem(moduleType);
            }

            GetShortcutModules();
        }

        protected override void OnConflictsUpdated(object sender, AllHotkeyConflictsEventArgs e)
        {
            dispatcher.BeginInvoke(() =>
            {
                var allConflictData = e.Conflicts;
                foreach (var inAppConflict in allConflictData.InAppConflicts)
                {
                    var hotkey = inAppConflict.Hotkey;
                    var hotkeySetting = new HotkeySettings(hotkey.Win, hotkey.Ctrl, hotkey.Alt, hotkey.Shift, hotkey.Key);
                    inAppConflict.ConflictIgnored = HotkeyConflictIgnoreHelper.IsIgnoringConflicts(hotkeySetting);
                }

                foreach (var systemConflict in allConflictData.SystemConflicts)
                {
                    var hotkey = systemConflict.Hotkey;
                    var hotkeySetting = new HotkeySettings(hotkey.Win, hotkey.Ctrl, hotkey.Alt, hotkey.Shift, hotkey.Key);
                    systemConflict.ConflictIgnored = HotkeyConflictIgnoreHelper.IsIgnoringConflicts(hotkeySetting);
                }

                AllHotkeyConflictsData = e.Conflicts ?? new AllHotkeyConflictsData();
            });
        }

        private void RequestConflictData()
        {
            // Request current conflicts data
            GlobalHotkeyConflictManager.Instance?.RequestAllConflicts();
        }

        private void AddDashboardListItem(ModuleType moduleType)
        {
            GpoRuleConfigured gpo = ModuleHelper.GetModuleGpoConfiguration(moduleType);
            var newItem = new DashboardListItem()
            {
                Tag = moduleType,
                Label = resourceLoader.GetString(ModuleHelper.GetModuleLabelResourceName(moduleType)),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && ModuleHelper.GetIsModuleEnabled(generalSettingsConfig, moduleType)),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = ModuleHelper.GetModuleTypeFluentIconName(moduleType),
                DashboardModuleItems = GetModuleItems(moduleType),
            };

            AllModules.Add(newItem);
            newItem.EnabledChangedCallback = EnabledChangedOnUI;
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

            // Request updated conflicts after module state change
            RequestConflictData();
        }

        public void ModuleEnabledChangedOnSettingsPage()
        {
            try
            {
                GetShortcutModules();

                OnPropertyChanged(nameof(ShortcutModules));

                // Request updated conflicts after module state change
                RequestConflictData();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Updating active/disabled modules list failed: {ex.Message}");
            }
        }

        private void GetShortcutModules()
        {
            ShortcutModules.Clear();
            ActionModules.Clear();

            foreach (var x in AllModules.Where(x => x.IsEnabled))
            {
                var filteredItems = x.DashboardModuleItems
                    .Where(m => m is DashboardModuleShortcutItem || m is DashboardModuleActivationItem)
                    .ToList();

                if (filteredItems.Count != 0)
                {
                    var newItem = new DashboardListItem
                    {
                        Icon = x.Icon,
                        IsLocked = x.IsLocked,
                        Label = x.Label,
                        Tag = x.Tag,
                        IsEnabled = x.IsEnabled,
                        DashboardModuleItems = new ObservableCollection<DashboardModuleItem>(filteredItems),
                    };

                    ShortcutModules.Add(newItem);
                    newItem.EnabledChangedCallback = x.EnabledChangedCallback;
                }
            }

            foreach (var x in AllModules.Where(x => x.IsEnabled))
            {
                var filteredItems = x.DashboardModuleItems
                    .Where(m => m is DashboardModuleButtonItem)
                    .ToList();

                if (filteredItems.Count != 0)
                {
                    var newItem = new DashboardListItem
                    {
                        Icon = x.Icon,
                        IsLocked = x.IsLocked,
                        Label = x.Label,
                        Tag = x.Tag,
                        IsEnabled = x.IsEnabled,
                        DashboardModuleItems = new ObservableCollection<DashboardModuleItem>(filteredItems),
                    };

                    ActionModules.Add(newItem);
                    newItem.EnabledChangedCallback = x.EnabledChangedCallback;
                }
            }
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItems(ModuleType moduleType)
        {
            return moduleType switch
            {
                ModuleType.AdvancedPaste => GetModuleItemsAdvancedPaste(),
                ModuleType.AlwaysOnTop => GetModuleItemsAlwaysOnTop(),
                ModuleType.CmdPal => GetModuleItemsCmdPal(),
                ModuleType.ColorPicker => GetModuleItemsColorPicker(),
                ModuleType.CropAndLock => GetModuleItemsCropAndLock(),
                ModuleType.EnvironmentVariables => GetModuleItemsEnvironmentVariables(),
                ModuleType.FancyZones => GetModuleItemsFancyZones(),
                ModuleType.FindMyMouse => GetModuleItemsFindMyMouse(),
                ModuleType.Hosts => GetModuleItemsHosts(),
                ModuleType.LightSwitch => GetModuleItemsLightSwitch(),
                ModuleType.MouseHighlighter => GetModuleItemsMouseHighlighter(),
                ModuleType.MouseJump => GetModuleItemsMouseJump(),
                ModuleType.MousePointerCrosshairs => GetModuleItemsMousePointerCrosshairs(),
                ModuleType.Peek => GetModuleItemsPeek(),
                ModuleType.PowerLauncher => GetModuleItemsPowerLauncher(),
                ModuleType.PowerAccent => GetModuleItemsPowerAccent(),
                ModuleType.Workspaces => GetModuleItemsWorkspaces(),
                ModuleType.RegistryPreview => GetModuleItemsRegistryPreview(),
                ModuleType.MeasureTool => GetModuleItemsMeasureTool(),
                ModuleType.ShortcutGuide => GetModuleItemsShortcutGuide(),
                ModuleType.PowerOCR => GetModuleItemsPowerOCR(),
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

        private ObservableCollection<DashboardModuleItem> GetModuleItemsCmdPal()
        {
            var hotkey = new CmdPalProperties().Hotkey;

            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("CmdPal_ActivationDescription"), Shortcut = hotkey.GetKeysList() },
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

        private ObservableCollection<DashboardModuleItem> GetModuleItemsLightSwitch()
        {
            ISettingsRepository<LightSwitchSettings> moduleSettingsRepository = SettingsRepository<LightSwitchSettings>.GetInstance(new SettingsUtils());
            var settings = moduleSettingsRepository.SettingsConfig;
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("LightSwitch_ForceDarkMode"), Shortcut = settings.Properties.ToggleThemeHotkey.Value.GetKeysList() },
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
                new DashboardModuleButtonItem() { ButtonTitle = resourceLoader.GetString("EnvironmentVariables_LaunchButtonControl/Header"), IsButtonDescriptionVisible = true, ButtonDescription = resourceLoader.GetString("EnvironmentVariables_LaunchButtonControl/Description"), ButtonGlyph = "ms-appx:///Assets/Settings/Icons/EnvironmentVariables.png", ButtonClickHandler = EnvironmentVariablesLaunchClicked },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsFancyZones()
        {
            ISettingsRepository<FancyZonesSettings> moduleSettingsRepository = SettingsRepository<FancyZonesSettings>.GetInstance(new SettingsUtils());
            var settings = moduleSettingsRepository.SettingsConfig;
            string activationMode = $"{resourceLoader.GetString(settings.Properties.FancyzonesShiftDrag.Value ? "FancyZones_ActivationShiftDrag" : "FancyZones_ActivationNoShiftDrag")}.";

            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleActivationItem() { Label = resourceLoader.GetString("Activate_Zones"), Activation = activationMode },
                new DashboardModuleShortcutItem() { Label = resourceLoader.GetString("FancyZones_OpenEditor"), Shortcut = settings.Properties.FancyzonesEditorHotkey.Value.GetKeysList() },
                new DashboardModuleButtonItem() { ButtonTitle = resourceLoader.GetString("FancyZones_LaunchEditorButtonControl/Header"), IsButtonDescriptionVisible = true, ButtonDescription = resourceLoader.GetString("FancyZones_LaunchEditorButtonControl/Description"), ButtonGlyph = "ms-appx:///Assets/Settings/Icons/FancyZones.png", ButtonClickHandler = FancyZoneLaunchClicked },
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
                string activation = string.Empty;
                switch (activationMethod)
                {
                    case 2: activation = resourceLoader.GetString("MouseUtils_FindMyMouse_ActivationShakeMouse/Content"); break;
                    case 1: activation = resourceLoader.GetString("MouseUtils_FindMyMouse_ActivationDoubleRightControlPress/Content"); break;
                    case 0:
                    default: activation = resourceLoader.GetString("MouseUtils_FindMyMouse_ActivationDoubleControlPress/Content"); break;
                }

                list.Add(new DashboardModuleActivationItem() { Label = resourceLoader.GetString("Dashboard_Activation"), Activation = activation });
            }

            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsHosts()
        {
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleButtonItem() { ButtonTitle = resourceLoader.GetString("Hosts_LaunchButtonControl/Header"), IsButtonDescriptionVisible = true, ButtonDescription = resourceLoader.GetString("Hosts_LaunchButtonControl/Description"), ButtonGlyph = "ms-appx:///Assets/Settings/Icons/Hosts.png", ButtonClickHandler = HostLaunchClicked },
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
            var settingsUtils = new SettingsUtils();
            PowerAccentSettings moduleSettings = settingsUtils.GetSettingsOrDefault<PowerAccentSettings>(PowerAccentSettings.ModuleName);
            var activationMethod = moduleSettings.Properties.ActivationKey;
            string activation = string.Empty;
            switch (activationMethod)
            {
                case Library.Enumerations.PowerAccentActivationKey.LeftRightArrow: activation = resourceLoader.GetString("QuickAccent_Activation_Key_Arrows/Content"); break;
                case Library.Enumerations.PowerAccentActivationKey.Space: activation = resourceLoader.GetString("QuickAccent_Activation_Key_Space/Content"); break;
                case Library.Enumerations.PowerAccentActivationKey.Both: activation = resourceLoader.GetString("QuickAccent_Activation_Key_Either/Content"); break;
            }

            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleActivationItem() { Label = resourceLoader.GetString("Dashboard_Activation"), Activation = activation },
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
                new DashboardModuleButtonItem() { ButtonTitle = resourceLoader.GetString("Workspaces_LaunchEditorButtonControl/Header"), IsButtonDescriptionVisible = true, ButtonDescription = resourceLoader.GetString("FancyZones_LaunchEditorButtonControl/Description"), ButtonGlyph = "ms-appx:///Assets/Settings/Icons/Workspaces.png", ButtonClickHandler = WorkspacesLaunchClicked },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsRegistryPreview()
        {
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleButtonItem() { ButtonTitle = resourceLoader.GetString("RegistryPreview_LaunchButtonControl/Header"), ButtonGlyph = "ms-appx:///Assets/Settings/Icons/RegistryPreview.png",  ButtonClickHandler = RegistryPreviewLaunchClicked },
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

        private void RegistryPreviewLaunchClicked(object sender, RoutedEventArgs e)
        {
            var actionName = "Launch";
            SendConfigMSG("{\"action\":{\"RegistryPreview\":{\"action_name\":\"" + actionName + "\", \"value\":\"\"}}}");
        }

        internal void DashboardListItemClick(object sender)
        {
            if (sender is SettingsCard card && card.Tag is ModuleType moduleType)
            {
                NavigationService.Navigate(ModuleHelper.GetModulePageType(moduleType));
            }
        }
    }
}
