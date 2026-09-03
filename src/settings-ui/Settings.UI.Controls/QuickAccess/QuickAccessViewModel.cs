// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.ApplicationModel.Resources;
using PowerToys.Interop;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public partial class QuickAccessViewModel : Observable
    {
        private readonly ISettingsRepository<GeneralSettings> _settingsRepository;

        // Pulling in KBMSettingsRepository separately as we need to listen to changes in the
        // UseNewEditor property to determine the visibility of the KeyboardManager quick access item.
        private readonly SettingsRepository<KeyboardManagerSettings> _kbmSettingsRepository;
        private readonly IQuickAccessLauncher _launcher;
        private readonly Func<ModuleType, bool> _isModuleGpoDisabled;
        private readonly Func<ModuleType, bool> _isModuleGpoEnabled;
        private readonly ResourceLoader _resourceLoader;
        private readonly DispatcherQueue _dispatcherQueue;
        private GeneralSettings _generalSettings;

        public ObservableCollection<QuickAccessItem> Items { get; } = new();

        public QuickAccessViewModel(
            ISettingsRepository<GeneralSettings> settingsRepository,
            IQuickAccessLauncher launcher,
            Func<ModuleType, bool> isModuleGpoDisabled,
            Func<ModuleType, bool> isModuleGpoEnabled,
            ResourceLoader resourceLoader)
        {
            _settingsRepository = settingsRepository;
            _launcher = launcher;
            _isModuleGpoDisabled = isModuleGpoDisabled;
            _isModuleGpoEnabled = isModuleGpoEnabled;
            _resourceLoader = resourceLoader;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            _generalSettings = _settingsRepository.SettingsConfig;
            _generalSettings.AddEnabledModuleChangeNotification(ModuleEnabledChanged);
            _settingsRepository.SettingsChanged += OnSettingsChanged;

            _kbmSettingsRepository = SettingsRepository<KeyboardManagerSettings>.GetInstance(SettingsUtils.Default);
            _kbmSettingsRepository.SettingsChanged += OnKbmSettingsChanged;

            InitializeItems();
        }

        private void OnSettingsChanged(GeneralSettings newSettings)
        {
            if (_dispatcherQueue != null)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    _generalSettings = newSettings;
                    _generalSettings.AddEnabledModuleChangeNotification(ModuleEnabledChanged);
                    RefreshItemsVisibility();
                });
            }
        }

        private void InitializeItems()
        {
            AddFlyoutMenuItem(ModuleType.ColorPicker);
            AddFlyoutMenuItem(ModuleType.CmdPal);
            AddFlyoutMenuItem(ModuleType.EnvironmentVariables);
            AddFlyoutMenuItem(ModuleType.FancyZones);
            AddFlyoutMenuItem(ModuleType.Hosts);
            AddFlyoutMenuItem(ModuleType.KeyboardManager);
            AddFlyoutMenuItem(ModuleType.LaserPointer, LaserPointerActions.ShareWindow);
            AddFlyoutMenuItem(ModuleType.LaserPointer, LaserPointerActions.StopSharing);
            AddFlyoutMenuItem(ModuleType.LightSwitch);
            AddFlyoutMenuItem(ModuleType.MouseWithoutBorders);
            AddFlyoutMenuItem(ModuleType.PowerDisplay);
            AddFlyoutMenuItem(ModuleType.PowerLauncher);
            AddFlyoutMenuItem(ModuleType.PowerOCR);
            AddFlyoutMenuItem(ModuleType.RegistryPreview);
            AddFlyoutMenuItem(ModuleType.MeasureTool);
            AddFlyoutMenuItem(ModuleType.ShortcutGuide);
            AddFlyoutMenuItem(ModuleType.Workspaces);
        }

        private void AddFlyoutMenuItem(ModuleType moduleType, string? action = null)
        {
            if (_isModuleGpoDisabled(moduleType))
            {
                return;
            }

            Items.Add(new QuickAccessItem
            {
                Title = GetItemTitle(moduleType, action),
                Tag = moduleType,
                CommandParameter = action,
                Visible = GetItemVisibility(moduleType, action),
                Description = GetModuleToolTip(moduleType),
                Icon = GetItemIcon(moduleType, action),
                Command = new RelayCommand(() => _launcher.Launch(moduleType, action)),
            });
        }

        private void ModuleEnabledChanged()
        {
            if (_dispatcherQueue != null)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    _generalSettings = _settingsRepository.SettingsConfig;
                    _generalSettings.AddEnabledModuleChangeNotification(ModuleEnabledChanged);
                    RefreshItemsVisibility();
                });
            }
        }

        private void RefreshItemsVisibility()
        {
            foreach (var item in Items)
            {
                if (item.Tag is ModuleType moduleType)
                {
                    item.Visible = GetItemVisibility(moduleType, item.CommandParameter as string);
                }
            }
        }

        private void OnKbmSettingsChanged(KeyboardManagerSettings newSettings)
        {
            if (_dispatcherQueue != null)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    RefreshItemsVisibility();
                });
            }
        }

        private bool GetItemVisibility(ModuleType moduleType, string? action = null)
        {
            // Generally, if gpo is enabled or if module enabled, then quick access item is visible.
            bool visible = _isModuleGpoEnabled(moduleType) || Microsoft.PowerToys.Settings.UI.Library.Helpers.ModuleHelper.GetIsModuleEnabled(_generalSettings, moduleType);

            // For KeyboardManager Quick Access item is only shown when using the new editor
            if (moduleType == ModuleType.KeyboardManager)
            {
                visible = visible && _kbmSettingsRepository.SettingsConfig.Properties.UseNewEditor;
            }

            // Nothing to stop when nothing is being shared, so that entry only appears
            // once there is something for it to act on.
            if (moduleType == ModuleType.LaserPointer && action == LaserPointerActions.StopSharing)
            {
                visible = visible && IsSharableWindowOn();
            }

            return visible;
        }

        // Items are normally just the module's name, because activating one launches that
        // module. Laser Pointer is the exception: its entry toggles the sharable window
        // rather than the laser, so it says what it does - and, because it is a toggle,
        // which way it will go.
        private string GetItemTitle(ModuleType moduleType, string? action = null)
        {
            string resourceName = moduleType switch
            {
                ModuleType.LaserPointer => action == LaserPointerActions.StopSharing
                    ? "QuickAccess_LaserPointer_StopSharing/Title"
                    : "QuickAccess_LaserPointer_ShareWindow/Title",
                _ => Microsoft.PowerToys.Settings.UI.Library.Helpers.ModuleHelper.GetModuleLabelResourceName(moduleType),
            };

            return _resourceLoader.GetString(resourceName);
        }

        private string GetItemIcon(ModuleType moduleType, string? action = null)
        {
            if (moduleType == ModuleType.LaserPointer)
            {
                // A window with a laser on it rather than the module's own icon, because
                // these entries are about the sharable window. Sharing shows the laser
                // striking a live window; stopping shows it greyed with a stop badge.
                return action == LaserPointerActions.StopSharing
                    ? "ms-appx:///Assets/Settings/Icons/LaserPointerStopSharing.png"
                    : "ms-appx:///Assets/Settings/Icons/LaserPointerShareWindow.png";
            }

            return Microsoft.PowerToys.Settings.UI.Library.Helpers.ModuleHelper.GetModuleTypeFluentIconName(moduleType);
        }

        // The window belongs to the Laser Pointer module, which lives in the runner
        // process, so its state arrives through a named event it keeps signalled while the
        // window is up. The handle is not held on to: if the module goes away the name
        // does too, and a stale open handle would keep reporting the last state forever.
        private static bool IsSharableWindowOn()
        {
            try
            {
                using var state = EventWaitHandle.OpenExisting(Constants.LaserPointerPresenterActiveEvent());
                return state.WaitOne(0);
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // No event means the module is not running, which is off either way.
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        // Called when the flyout is about to be shown: anything whose label depends on
        // live state has to be re-read, because the items themselves are built once.
        public void RefreshDynamicItems()
        {
            RefreshItemsVisibility();
        }

        private string GetModuleToolTip(ModuleType moduleType)
        {
            return moduleType switch
            {
                ModuleType.ColorPicker => SettingsRepository<ColorPickerSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ActivationShortcut.ToString(),
                ModuleType.FancyZones => SettingsRepository<FancyZonesSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.FancyzonesEditorHotkey.Value.ToString(),
                ModuleType.PowerDisplay => SettingsRepository<PowerDisplaySettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ActivationShortcut.ToString(),
                ModuleType.KeyboardManager => SettingsRepository<KeyboardManagerSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.DefaultEditorShortcut.ToString(),
                ModuleType.LaserPointer => SettingsRepository<LaserPointerSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.PresenterActivationShortcut.ToString(),
                ModuleType.LightSwitch => SettingsRepository<LightSwitchSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ToggleThemeHotkey.Value.ToString(),
                ModuleType.PowerLauncher => SettingsRepository<PowerLauncherSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.OpenPowerLauncher.ToString(),
                ModuleType.PowerOCR => SettingsRepository<PowerOcrSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ActivationShortcut.ToString(),
                ModuleType.Workspaces => SettingsRepository<WorkspacesSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.Hotkey.Value.ToString(),
                ModuleType.MeasureTool => SettingsRepository<MeasureToolSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ActivationShortcut.ToString(),
                ModuleType.ShortcutGuide => SettingsRepository<ShortcutGuideSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.OpenShortcutGuide.ToString(),
                ModuleType.MouseWithoutBorders => SettingsRepository<MouseWithoutBordersSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ReconnectShortcut.ToString(),
                _ => string.Empty,
            };
        }
    }
}
