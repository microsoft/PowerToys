// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class DashboardViewModel : Observable
    {
        private bool _isAddPanelVisible;
        private string _addButtonGlyph;

        public Func<string, int> SendConfigMSG { get; }

        public ObservableCollection<DashboardListItem> ActiveModules { get; set; } = new ObservableCollection<DashboardListItem>();

        public ObservableCollection<DashboardListItem> AllModules { get; set; } = new ObservableCollection<DashboardListItem>();

        public bool IsAddPanelVisible
        {
            get => _isAddPanelVisible;
            set
            {
                if (_isAddPanelVisible != value)
                {
                    _isAddPanelVisible = value;
                    OnPropertyChanged(nameof(IsAddPanelVisible));
                }
            }
        }

        public string AddButtonGlyph
        {
            get => _addButtonGlyph;
            set
            {
                if (_addButtonGlyph != value)
                {
                    _addButtonGlyph = value;
                    OnPropertyChanged(nameof(AddButtonGlyph));
                }
            }
        }

        public bool UpdateAvailable { get; set; }

        private List<DashboardListItem> _allModules;

        private ISettingsRepository<GeneralSettings> _settingsRepository;
        private GeneralSettings generalSettingsConfig;
        private Windows.ApplicationModel.Resources.ResourceLoader resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;

        public DashboardViewModel(ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            _settingsRepository = settingsRepository;
            generalSettingsConfig = settingsRepository.SettingsConfig;
            generalSettingsConfig.AddEnabledModuleChangeNotification(ModuleEnabledChangedOnSettingsPage);

            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            IsAddPanelVisible = false;
            AddButtonGlyph = "\uECC8";

            _allModules = new List<DashboardListItem>();

            GpoRuleConfigured gpo;
            gpo = GPOWrapper.GetConfiguredAlwaysOnTopEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "AlwaysOnTop",
                Label = resourceLoader.GetString("AlwaysOnTop/ModuleTitle"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.AlwaysOnTop,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsAlwaysOnTop.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsAlwaysOnTop(),
            });

            gpo = GPOWrapper.GetConfiguredAwakeEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "Awake",
                Label = resourceLoader.GetString("Awake/ModuleTitle"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.Awake,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsAwake.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsAwake(),
            });

            gpo = GPOWrapper.GetConfiguredColorPickerEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "ColorPicker",
                Label = resourceLoader.GetString("ColorPicker/ModuleTitle"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.ColorPicker,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsColorPicker.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsColorPicker(),
            });

            gpo = GPOWrapper.GetConfiguredCropAndLockEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "CropAndLock",
                Label = resourceLoader.GetString("CropAndLock/ModuleTitle"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.CropAndLock,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsCropAndLock.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsCropAndLock(),
            });

            gpo = GPOWrapper.GetConfiguredFancyZonesEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "FancyZones",
                Label = resourceLoader.GetString("FancyZones/ModuleTitle"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.FancyZones,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsFancyZones.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsFancyZones(),
            });

            gpo = GPOWrapper.GetConfiguredFileLocksmithEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "FileLocksmith",
                Label = resourceLoader.GetString("FileLocksmith/ModuleTitle"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.FileLocksmith,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsFileLocksmith.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsFileLocksmith(),
            });

            gpo = GPOWrapper.GetConfiguredFindMyMouseEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "FindMyMouse",
                Label = resourceLoader.GetString("MouseUtils_FindMyMouse/Header"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.FindMyMouse,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsFindMyMouse.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsFindMyMouse(),
            });

            gpo = GPOWrapper.GetConfiguredHostsFileEditorEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "Hosts",
                Label = resourceLoader.GetString("Hosts/ModuleTitle"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.Hosts,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsHosts.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsHosts(),
            });

            gpo = GPOWrapper.GetConfiguredImageResizerEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "ImageResizer",
                Label = resourceLoader.GetString("ImageResizer/ModuleTitle"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.ImageResizer,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsImageResizer.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsImageResizer(),
            });

            gpo = GPOWrapper.GetConfiguredKeyboardManagerEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "KeyboardManager",
                Label = resourceLoader.GetString("KeyboardManager/ModuleTitle"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.KeyboardManager,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsKeyboardManager.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsKeyboardManager(),
            });

            gpo = GPOWrapper.GetConfiguredMouseHighlighterEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "MouseHighlighter",
                Label = resourceLoader.GetString("MouseUtils_MouseHighlighter/Header"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.MouseHighlighter,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsMouseHighlighter.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsMouseHighlighter(),
            });

            gpo = GPOWrapper.GetConfiguredMouseJumpEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "MouseJump",
                Label = resourceLoader.GetString("MouseUtils_MouseJump/Header"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.MouseJump,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsMouseJump.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsMouseJump(),
            });

            gpo = GPOWrapper.GetConfiguredMousePointerCrosshairsEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "MousePointerCrosshairs",
                Label = resourceLoader.GetString("MouseUtils_MousePointerCrosshairs/Header"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.MousePointerCrosshairs,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsMouseCrosshairs.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsMouseCrosshairs(),
            });

            gpo = GPOWrapper.GetConfiguredMouseWithoutBordersEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "MouseWithoutBorders",
                Label = resourceLoader.GetString("MouseWithoutBorders/ModuleTitle"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.MouseWithoutBorders,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsMouseWithoutBorders.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsMouseWithoutBorders(),
            });

            gpo = GPOWrapper.GetConfiguredPastePlainEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "PastePlain",
                Label = resourceLoader.GetString("PastePlain/ModuleTitle"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.PastePlain,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPastePlain.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsPastePlain(),
            });

            gpo = GPOWrapper.GetConfiguredPeekEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "Peek",
                Label = resourceLoader.GetString("Peek/ModuleTitle"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.Peek,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPeek.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsPeek(),
            });

            gpo = GPOWrapper.GetConfiguredPowerRenameEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "PowerRename",
                Label = resourceLoader.GetString("PowerRename/ModuleTitle"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.PowerRename,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPowerRename.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsPowerRename(),
            });

            gpo = GPOWrapper.GetConfiguredPowerLauncherEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "PowerLauncher",
                Label = resourceLoader.GetString("PowerLauncher/ModuleTitle"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.PowerLauncher,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPowerToysRun.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsRun(),
            });

            gpo = GPOWrapper.GetConfiguredQuickAccentEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "PowerAccent",
                Label = resourceLoader.GetString("QuickAccent/ModuleTitle"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.PowerAccent,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPowerAccent.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsPowerAccent(),
            });

            gpo = GPOWrapper.GetConfiguredRegistryPreviewEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "RegistryPreview",
                Label = resourceLoader.GetString("RegistryPreview/ModuleTitle"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.RegistryPreview,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsRegistryPreview.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsRegistryPreview(),
            });

            gpo = GPOWrapper.GetConfiguredScreenRulerEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "MeasureTool",
                Label = resourceLoader.GetString("MeasureTool/ModuleTitle"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.MeasureTool,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsScreenRuler.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsScreenRuler(),
            });

            gpo = GPOWrapper.GetConfiguredShortcutGuideEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "ShortcutGuide",
                Label = resourceLoader.GetString("ShortcutGuide/ModuleTitle"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.ShortcutGuide,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsShortcutGuide.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsShortcutGuide(),
            });

            gpo = GPOWrapper.GetConfiguredTextExtractorEnabledValue();
            _allModules.Add(new DashboardListItem()
            {
                Tag = "PowerOCR",
                Label = resourceLoader.GetString("TextExtractor/ModuleTitle"),
                IsEnabled = gpo != GpoRuleConfigured.Disabled && generalSettingsConfig.Enabled.PowerOCR,
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPowerOCR.png",
                EnabledChangedCallback = EnabledChangedOnUI,
                DashboardModuleItems = GetModuleItemsPowerOCR(),
            });

            ActiveModules = new ObservableCollection<DashboardListItem>(_allModules.Where(x => x.IsEnabled));
            AllModules = new ObservableCollection<DashboardListItem>(_allModules);

            UpdatingSettings updatingSettingsConfig = UpdatingSettings.LoadSettings();
            UpdateAvailable = updatingSettingsConfig != null && (updatingSettingsConfig.State == UpdatingSettings.UpdatingState.ReadyToInstall || updatingSettingsConfig.State == UpdatingSettings.UpdatingState.ReadyToDownload);
        }

        internal void SettingsButtonClicked(object sender)
        {
            Button button = sender as Button;
            if (button == null)
            {
                return;
            }

            string tag = button.Tag as string;
            if (tag == null)
            {
                return;
            }

            Type type = null;
            switch (tag)
            {
                case "AlwaysOnTop": type = typeof(AlwaysOnTopPage); break;
                case "Awake": type = typeof(AwakePage); break;
                case "ColorPicker": type = typeof(ColorPickerPage); break;
                case "CropAndLock": type = typeof(CropAndLockPage); break;
                case "FancyZones": type = typeof(FancyZonesPage); break;
                case "FileLocksmith": type = typeof(FileLocksmithPage); break;
                case "FindMyMouse": type = typeof(MouseUtilsPage); break;
                case "Hosts": type = typeof(HostsPage); break;
                case "ImageResizer": type = typeof(ImageResizerPage); break;
                case "KeyboardManager": type = typeof(KeyboardManagerPage); break;
                case "MouseHighlighter": type = typeof(MouseUtilsPage); break;
                case "MouseJump": type = typeof(MouseUtilsPage); break;
                case "MousePointerCrosshairs": type = typeof(MouseUtilsPage); break;
                case "MouseWithoutBorders": type = typeof(MouseWithoutBordersPage); break;
                case "PastePlain": type = typeof(PastePlainPage); break;
                case "Peek": type = typeof(PeekPage); break;
                case "PowerRename": type = typeof(PowerRenamePage); break;
                case "PowerLauncher": type = typeof(PowerLauncherPage); break;
                case "PowerAccent": type = typeof(PowerAccentPage); break;
                case "RegistryPreview": type = typeof(RegistryPreviewPage); break;
                case "MeasureTool": type = typeof(MeasureToolPage); break;
                case "ShortcutGuide": type = typeof(ShortcutGuidePage); break;
                case "PowerOCR": type = typeof(PowerOcrPage); break;
                case "VideoConference": type = typeof(VideoConferencePage); break;
            }

            NavigationService.Navigate(type);
        }

        private void EnabledChangedOnUI(DashboardListItem dashboardListItem)
        {
            Views.ShellPage.UpdateGeneralSettingsCallback(dashboardListItem.Tag, dashboardListItem.IsEnabled);
        }

        public void ModuleEnabledChangedOnSettingsPage()
        {
            ActiveModules.Clear();
            generalSettingsConfig = _settingsRepository.SettingsConfig;
            foreach (DashboardListItem item in _allModules)
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

                if (item.IsEnabled)
                {
                    ActiveModules.Add(item);
                }
            }

            OnPropertyChanged(nameof(ActiveModules));
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsAlwaysOnTop()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<AlwaysOnTopSettings> moduleSettingsRepository = SettingsRepository<AlwaysOnTopSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.Hotkey.Value;
            list.Add(new DashboardModuleItem() { Label = resourceLoader.GetString("AlwaysOnTop_ShortDescription"), IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsAwake()
        {
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("Awake_ShortDescription") },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsColorPicker()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<ColorPickerSettings> moduleSettingsRepository = SettingsRepository<ColorPickerSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.ActivationShortcut;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("ColorPicker_ShortDescription"), IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsCropAndLock()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<CropAndLockSettings> moduleSettingsRepository = SettingsRepository<CropAndLockSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkeyThumbnail = settings.Properties.ThumbnailHotkey.Value;
            var hotkeyReparent = settings.Properties.ReparentHotkey.Value;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("CropAndLock_Thumbnail"), IsShortcutVisible = true, Shortcut = hotkeyThumbnail.GetKeysList() });
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("CropAndLock_Reparent"), IsShortcutVisible = true, Shortcut = hotkeyReparent.GetKeysList() });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsFancyZones()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<FancyZonesSettings> moduleSettingsRepository = SettingsRepository<FancyZonesSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.FancyzonesEditorHotkey.Value;
            var hotkeyPrev = settings.Properties.FancyzonesPrevTabHotkey.Value;
            var hotkeyNext = settings.Properties.FancyzonesNextTabHotkey.Value;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("FancyZones_OpenEditor"), IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("FancyZones_PreviousLayout"), IsShortcutVisible = true, Shortcut = hotkeyPrev.GetKeysList() });
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("FancyZones_NextLayout"), IsShortcutVisible = true, Shortcut = hotkeyNext.GetKeysList() });
            list.Add(new DashboardModuleItem() { IsButtonVisible = true, ButtonTitle = resourceLoader.GetString("FancyZones_LaunchEditorButtonControl/Header"), IsButtonDescriptionVisible = true, ButtonDescription = resourceLoader.GetString("FancyZones_LaunchEditorButtonControl/Description"), ButtonGlyph = "\uEB3C", ButtonClickHandler = FancyZoneLaunchClicked });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsFileLocksmith()
        {
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("FileLocksmith_ShortDescription") },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsFindMyMouse()
        {
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<FindMyMouseSettings> moduleSettingsRepository = SettingsRepository<FindMyMouseSettings>.GetInstance(settingsUtils);
            string shortDescription = resourceLoader.GetString("FindMyMouse_ShortDescription");
            var settings = moduleSettingsRepository.SettingsConfig;
            var activationMethod = settings.Properties.ActivationMethod.Value;
            var list = new List<DashboardModuleItem>();
            if (activationMethod == 3)
            {
                var hotkey = settings.Properties.ActivationShortcut;
                list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = shortDescription, IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
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

                list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = shortDescription });
            }

            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsHosts()
        {
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleItem() { IsButtonVisible = true, ButtonTitle = resourceLoader.GetString("Hosts_LaunchButtonControl/Header"), IsButtonDescriptionVisible = true, ButtonDescription = resourceLoader.GetString("Hosts_LaunchButtonControl/Description"), ButtonGlyph = "\uEA37", ButtonClickHandler = HostLaunchClicked },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsImageResizer()
        {
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("ImageResizer_ShortDescription") },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsKeyboardManager()
        {
            const string JsonFileType = ".json";
            const string PowerToyName = KeyboardManagerSettings.ModuleName;
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<KeyboardManagerSettings> moduleSettingsRepository = SettingsRepository<KeyboardManagerSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            string fileName = settings.Properties.ActiveConfiguration.Value + JsonFileType;
            KeyboardManagerProfile kbmProfile = settingsUtils.GetSettingsOrDefault<KeyboardManagerProfile>(PowerToyName, fileName);
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleItem() { IsButtonVisible = true, ButtonTitle = resourceLoader.GetString("KeyboardManager_RemapKeyboardButton/Header"), IsButtonDescriptionVisible = true, ButtonDescription = resourceLoader.GetString("KeyboardManager_RemapKeyboardButton/Description"), ButtonGlyph = "\uE92E", ButtonClickHandler = KbmKeyLaunchClicked },
                new DashboardModuleItem() { RemapKeys = kbmProfile?.RemapKeys.InProcessRemapKeys },
                new DashboardModuleItem() { IsButtonVisible = true, ButtonTitle = resourceLoader.GetString("KeyboardManager_RemapShortcutsButton/Header"), IsButtonDescriptionVisible = true, ButtonDescription = resourceLoader.GetString("KeyboardManager_RemapShortcutsButton/Description"), ButtonGlyph = "\uE92E", ButtonClickHandler = KbmShortcutLaunchClicked },
                new DashboardModuleItem() { RemapShortcuts = KeyboardManagerViewModel.CombineShortcutLists(kbmProfile?.RemapShortcuts.GlobalRemapShortcuts, kbmProfile?.RemapShortcuts.AppSpecificRemapShortcuts) },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsMouseHighlighter()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<MouseHighlighterSettings> moduleSettingsRepository = SettingsRepository<MouseHighlighterSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.ActivationShortcut;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("MouseHighlighter_ShortDescription"), IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsMouseJump()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<MouseJumpSettings> moduleSettingsRepository = SettingsRepository<MouseJumpSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.ActivationShortcut;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("MouseJump_ShortDescription"), IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsMouseCrosshairs()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<MousePointerCrosshairsSettings> moduleSettingsRepository = SettingsRepository<MousePointerCrosshairsSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.ActivationShortcut;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("MouseCrosshairs_ShortDescription"), IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsMouseWithoutBorders()
        {
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("MouseWithoutBorders_ShortDescription") },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsPastePlain()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<PastePlainSettings> moduleSettingsRepository = SettingsRepository<PastePlainSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.ActivationShortcut;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("PastePlain_ShortDescription"), IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsPeek()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<PeekSettings> moduleSettingsRepository = SettingsRepository<PeekSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.ActivationShortcut;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("Peek_ShortDescription"), IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsPowerRename()
        {
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("PowerRename_ShortDescription") },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsRun()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<PowerLauncherSettings> moduleSettingsRepository = SettingsRepository<PowerLauncherSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.OpenPowerLauncher;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("Run_ShortDescription"), IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
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

            var list = new List<DashboardModuleItem>();
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = shortDescription });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsRegistryPreview()
        {
            var list = new List<DashboardModuleItem>
            {
                new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("RegistryPreview_ShortDescription") },
                new DashboardModuleItem() { IsButtonVisible = true, ButtonTitle = resourceLoader.GetString("RegistryPreview_LaunchButtonControl/Header"), ButtonGlyph = "\uEA37",  ButtonClickHandler = RegistryPreviewLaunchClicked },
            };
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsScreenRuler()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<MeasureToolSettings> moduleSettingsRepository = SettingsRepository<MeasureToolSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.ActivationShortcut;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("ScreenRuler_ShortDescription"), IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsShortcutGuide()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<ShortcutGuideSettings> moduleSettingsRepository = SettingsRepository<ShortcutGuideSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.OpenShortcutGuide;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("ShortcutGuide_ShortDescription"), IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsPowerOCR()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<PowerOcrSettings> moduleSettingsRepository = SettingsRepository<PowerOcrSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.ActivationShortcut;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = resourceLoader.GetString("PowerOcr_ShortDescription"), IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });

            return new ObservableCollection<DashboardModuleItem>(list);
        }

        internal void SWVersionButtonClicked()
        {
            NavigationService.Navigate(typeof(GeneralPage));
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

        internal void AddButtonClick()
        {
            if (IsAddPanelVisible)
            {
                IsAddPanelVisible = false;
                AddButtonGlyph = "\uECC8";
            }
            else
            {
                IsAddPanelVisible = true;
                AddButtonGlyph = "\uE930";
            }
        }
    }
}
