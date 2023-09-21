// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Linq.Expressions;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Media.Animation;
using CommunityToolkit.WinUI.UI.Controls.TextToolbarSymbols;
using global::PowerToys.GPOWrapper;
using interop;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class DashboardViewModel : Observable
    {
        public ObservableCollection<DashboardListItem> ActiveModules { get; set; } = new ObservableCollection<DashboardListItem>();

        public ObservableCollection<DashboardListItem> AllModules { get; set; } = new ObservableCollection<DashboardListItem>();

        public bool IsUptoDate { get; set; }

        public Brush SWVersionBackgroundColor { get; set; }

        public string IsUptoDateIconGlyph
        {
            get
            {
                if (IsUptoDate)
                {
                    return "\uE930";
                }
                else
                {
                    return "\uEA39";
                }
            }
        }

        public string SWVersionText { get; set; }

        private List<DashboardListItem> _allModules;

        private ISettingsRepository<GeneralSettings> _settingsRepository;
        private GeneralSettings generalSettingsConfig;

        public DashboardViewModel(ISettingsRepository<GeneralSettings> settingsRepository)
        {
            _settingsRepository = settingsRepository;
            generalSettingsConfig = settingsRepository.SettingsConfig;
            generalSettingsConfig.AddEnabledModuleChangeNotification(ModuleEnabledChangedOnSettingsPage);

            _allModules = new List<DashboardListItem>();

            var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
            GpoRuleConfigured gpo;
            if ((gpo = GPOWrapper.GetConfiguredAlwaysOnTopEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("AlwaysOnTop/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.AlwaysOnTop, Tag = "AlwaysOnTop", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsAlwaysOnTop.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsAlwaysOnTop() });
            }

            if ((gpo = GPOWrapper.GetConfiguredAwakeEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("Awake/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.Awake, Tag = "Awake", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsAwake.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsAwake() });
            }

            if ((gpo = GPOWrapper.GetConfiguredColorPickerEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("ColorPicker/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.ColorPicker, Tag = "ColorPicker", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsColorPicker.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsColorPicker() });
            }

            if ((gpo = GPOWrapper.GetConfiguredCropAndLockEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("CropAndLock/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.CropAndLock, Tag = "CropAndLock", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsCropAndLock.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsCropAndLock() });
            }

            if ((gpo = GPOWrapper.GetConfiguredFancyZonesEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("FancyZones/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.FancyZones, Tag = "FancyZones", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsFancyZones.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsFancyZones() });
            }

            if ((gpo = GPOWrapper.GetConfiguredFileLocksmithEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("FileLocksmith/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.FileLocksmith, Tag = "FileLocksmith", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsFileLocksmith.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsFileLocksmith() });
            }

            if ((gpo = GPOWrapper.GetConfiguredFindMyMouseEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("MouseUtils_FindMyMouse/Header"), IsEnabled = generalSettingsConfig.Enabled.FindMyMouse, Tag = "FindMyMouse", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsFindMyMouse.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsFindMyMouse() });
            }

            if ((gpo = GPOWrapper.GetConfiguredHostsFileEditorEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("Hosts/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.Hosts, Tag = "Hosts", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsHosts.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsHosts() });
            }

            if ((gpo = GPOWrapper.GetConfiguredImageResizerEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("ImageResizer/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.ImageResizer, Tag = "ImageResizer", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsImageResizer.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsImageResizer() });
            }

            if ((gpo = GPOWrapper.GetConfiguredKeyboardManagerEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("KeyboardManager/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.KeyboardManager, Tag = "KeyboardManager", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsKeyboardManager.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsKeyboardManager() });
            }

            if ((gpo = GPOWrapper.GetConfiguredMouseHighlighterEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("MouseUtils_MouseHighlighter/Header"), IsEnabled = generalSettingsConfig.Enabled.MouseHighlighter, Tag = "MouseHighlighter", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsMouseHighlighter.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsMouseHighlighter() });
            }

            if ((gpo = GPOWrapper.GetConfiguredMouseJumpEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("MouseUtils_MouseJump/Header"), IsEnabled = generalSettingsConfig.Enabled.MouseJump, Tag = "MouseJump", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsMouseJump.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsMouseJump() });
            }

            if ((gpo = GPOWrapper.GetConfiguredMousePointerCrosshairsEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("MouseUtils_MousePointerCrosshairs/Header"), IsEnabled = generalSettingsConfig.Enabled.MousePointerCrosshairs, Tag = "MousePointerCrosshairs", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsMouseCrosshairs.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsMouseCrosshairs() });
            }

            if ((gpo = GPOWrapper.GetConfiguredMouseWithoutBordersEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("MouseWithoutBorders/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.MouseWithoutBorders, Tag = "MouseWithoutBorders", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsMouseWithoutBorders.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsMouseWithoutBorders() });
            }

            if ((gpo = GPOWrapper.GetConfiguredPastePlainEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("PastePlain/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.PastePlain, Tag = "PastePlain", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPastePlain.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsPastePlain() });
            }

            if ((gpo = GPOWrapper.GetConfiguredPeekEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("Peek/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.Peek, Tag = "Peek", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPeek.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsPeek() });
            }

            if ((gpo = GPOWrapper.GetConfiguredPowerRenameEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("PowerRename/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.PowerRename, Tag = "PowerRename", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPowerRename.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsPowerRename() });
            }

            if ((gpo = GPOWrapper.GetConfiguredPowerLauncherEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("PowerLauncher/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.PowerLauncher, Tag = "PowerLauncher", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPowerToysRun.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsRun() });
            }

            if ((gpo = GPOWrapper.GetConfiguredQuickAccentEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("QuickAccent/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.PowerAccent, Tag = "PowerAccent", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPowerAccent.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsPowerAccent() });
            }

            if ((gpo = GPOWrapper.GetConfiguredRegistryPreviewEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("RegistryPreview/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.RegistryPreview, Tag = "RegistryPreview", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsRegistryPreview.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsRegistryPreview() });
            }

            if ((gpo = GPOWrapper.GetConfiguredScreenRulerEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("MeasureTool/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.MeasureTool, Tag = "MeasureTool", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsScreenRuler.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsScreenRuler() });
            }

            if ((gpo = GPOWrapper.GetConfiguredShortcutGuideEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("ShortcutGuide/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.ShortcutGuide, Tag = "ShortcutGuide", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsShortcutGuide.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsShortcutGuide() });
            }

            if ((gpo = GPOWrapper.GetConfiguredTextExtractorEnabledValue()) != GpoRuleConfigured.Disabled && gpo != GpoRuleConfigured.Enabled)
            {
                _allModules.Add(new DashboardListItem() { Label = resourceLoader.GetString("TextExtractor/ModuleTitle"), IsEnabled = generalSettingsConfig.Enabled.PowerOCR, Tag = "PowerOCR", Icon = "ms-appx:///Assets/Settings/FluentIcons/FluentIconsPowerOCR.png", EnabledChangedCallback = EnabledChangedOnUI, DashboardModuleItems = GetModuleItemsPowerOCR() });
            }

            ActiveModules = new ObservableCollection<DashboardListItem>(_allModules.Where(x => x.IsEnabled));
            AllModules = new ObservableCollection<DashboardListItem>(_allModules);

            UpdatingSettings updatingSettingsConfig = UpdatingSettings.LoadSettings();
            IsUptoDate = updatingSettingsConfig != null && updatingSettingsConfig.State == UpdatingSettings.UpdatingState.UpToDate;
            SWVersionBackgroundColor = new SolidColorBrush(IsUptoDate ? Colors.DarkGreen : Colors.Orange);
            SWVersionText = Helper.GetProductVersion() + " - " + resourceLoader.GetString(IsUptoDate ? "Dashboard/Latest" : "Dashboard/Outdated");
        }

        private void EnabledChangedOnUI(DashboardListItem dashboardListItem)
        {
            Views.ShellPage.UpdateGeneralSettingsCallback(dashboardListItem.Tag, dashboardListItem.IsEnabled);
        }

        public void ModuleEnabledChangedOnSettingsPage()
        {
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
            }

            ActiveModules = new ObservableCollection<DashboardListItem>(_allModules.Where(x => x.IsEnabled));
            OnPropertyChanged(nameof(ActiveModules));
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsAlwaysOnTop()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<AlwaysOnTopSettings> moduleSettingsRepository = SettingsRepository<AlwaysOnTopSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.Hotkey.Value;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Pinning a window", IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsAwake()
        {
            var list = new List<DashboardModuleItem>();
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Keep your PC awake" });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsColorPicker()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<ColorPickerSettings> moduleSettingsRepository = SettingsRepository<ColorPickerSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.ActivationShortcut;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Pick a color", IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
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
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Thumbnail", IsShortcutVisible = true, Shortcut = hotkeyThumbnail.GetKeysList() });
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Reparent", IsShortcutVisible = true, Shortcut = hotkeyReparent.GetKeysList() });
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
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Open editor", IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Previous Layout", IsShortcutVisible = true, Shortcut = hotkeyPrev.GetKeysList() });
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Next Layout", IsShortcutVisible = true, Shortcut = hotkeyNext.GetKeysList() });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsFileLocksmith()
        {
            var list = new List<DashboardModuleItem>();
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Shows which processes are using the selected files" });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsFindMyMouse()
        {
            var list = new List<DashboardModuleItem>();
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Finds the mouse" });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsHosts()
        {
            var list = new List<DashboardModuleItem>();
            list.Add(new DashboardModuleItem() { IsButtonVisible = true, ButtonTitle = "Launch" });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsImageResizer()
        {
            var list = new List<DashboardModuleItem>();
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Select Image Resizer in right-click context menu" });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsKeyboardManager()
        {
            var list = new List<DashboardModuleItem>();
            list.Add(new DashboardModuleItem() { IsButtonVisible = true, ButtonTitle = "Remap a key" });
            list.Add(new DashboardModuleItem() { IsButtonVisible = true, ButtonTitle = "Remap a shortcut" });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsMouseHighlighter()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<MouseHighlighterSettings> moduleSettingsRepository = SettingsRepository<MouseHighlighterSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.ActivationShortcut;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Highlights clicks", IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsMouseJump()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<MouseJumpSettings> moduleSettingsRepository = SettingsRepository<MouseJumpSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.ActivationShortcut;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Quickly move the mouse pointer", IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsMouseCrosshairs()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<MousePointerCrosshairsSettings> moduleSettingsRepository = SettingsRepository<MousePointerCrosshairsSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.ActivationShortcut;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Draw crosshairs centered on the mouse pointer", IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsMouseWithoutBorders()
        {
            var list = new List<DashboardModuleItem>();
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Move your cursor across multiple devices" });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsPastePlain()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<PastePlainSettings> moduleSettingsRepository = SettingsRepository<PastePlainSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.ActivationShortcut;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Paste clipboard content without formatting", IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsPeek()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<PeekSettings> moduleSettingsRepository = SettingsRepository<PeekSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.ActivationShortcut;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Quick and easy previewer", IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsPowerRename()
        {
            var list = new List<DashboardModuleItem>();
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Select Power Rename in right-click context menu" });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsRun()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<PowerLauncherSettings> moduleSettingsRepository = SettingsRepository<PowerLauncherSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.OpenPowerLauncher;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "A quick launcher", IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsPowerAccent()
        {
            var list = new List<DashboardModuleItem>();
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "An alternative way to type accented characters" });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsRegistryPreview()
        {
            var list = new List<DashboardModuleItem>();
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Visualize and edit Windows Registry files" });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsScreenRuler()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<MeasureToolSettings> moduleSettingsRepository = SettingsRepository<MeasureToolSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.ActivationShortcut;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Measure pixels on your screen", IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsShortcutGuide()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<ShortcutGuideSettings> moduleSettingsRepository = SettingsRepository<ShortcutGuideSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.OpenShortcutGuide;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Shows a help overlay with Windows shortcuts", IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });
            return new ObservableCollection<DashboardModuleItem>(list);
        }

        private ObservableCollection<DashboardModuleItem> GetModuleItemsPowerOCR()
        {
            var list = new List<DashboardModuleItem>();
            var settingsUtils = new SettingsUtils();
            ISettingsRepository<PowerOcrSettings> moduleSettingsRepository = SettingsRepository<PowerOcrSettings>.GetInstance(settingsUtils);
            var settings = moduleSettingsRepository.SettingsConfig;
            var hotkey = settings.Properties.ActivationShortcut;
            list.Add(new DashboardModuleItem() { IsLabelVisible = true, Label = "Copy text from anywhere on screen", IsShortcutVisible = true, Shortcut = hotkey.GetKeysList() });

            return new ObservableCollection<DashboardModuleItem>(list);
        }

        internal void SWVersionButtonClicked()
        {
            NavigationService.Navigate(typeof(GeneralPage));
        }
    }
}
