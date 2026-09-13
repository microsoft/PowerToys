// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public partial class QuickAccessViewModel : Observable, IDisposable
    {
        private readonly ISettingsRepository<GeneralSettings> _settingsRepository;

        // Pulling in KBMSettingsRepository separately as we need to listen to changes in the
        // UseNewEditor property to determine the visibility of the KeyboardManager quick access item.
        private readonly ISettingsRepository<KeyboardManagerSettings> _kbmSettingsRepository;
        private readonly IQuickAccessLauncher _launcher;
        private readonly Func<ModuleType, bool> _isModuleGpoDisabled;
        private readonly Func<ModuleType, bool> _isModuleGpoEnabled;
        private readonly Func<string, string> _getString;
        private readonly Func<ModuleType, string> _getModuleToolTip;
        private readonly Action<Action> _enqueue;
        private readonly Action _enabledChangedCallback;
        private GeneralSettings _generalSettings;
        private volatile bool _isDisposed;

        public ObservableCollection<QuickAccessItem> Items { get; } = new();

        public QuickAccessViewModel(
            ISettingsRepository<GeneralSettings> settingsRepository,
            IQuickAccessLauncher launcher,
            Func<ModuleType, bool> isModuleGpoDisabled,
            Func<ModuleType, bool> isModuleGpoEnabled,
            ResourceLoader resourceLoader)
            : this(
                settingsRepository,
                SettingsRepository<KeyboardManagerSettings>.GetInstance(SettingsUtils.Default),
                launcher,
                isModuleGpoDisabled,
                isModuleGpoEnabled,
                resourceLoader.GetString,
                GetModuleToolTip,
                CreateDispatcher())
        {
        }

        internal QuickAccessViewModel(
            ISettingsRepository<GeneralSettings> settingsRepository,
            ISettingsRepository<KeyboardManagerSettings> kbmSettingsRepository,
            IQuickAccessLauncher launcher,
            Func<ModuleType, bool> isModuleGpoDisabled,
            Func<ModuleType, bool> isModuleGpoEnabled,
            Func<string, string> getString,
            Func<ModuleType, string> getModuleToolTip,
            Action<Action> enqueue)
        {
            _settingsRepository = settingsRepository;
            _kbmSettingsRepository = kbmSettingsRepository;
            _launcher = launcher;
            _isModuleGpoDisabled = isModuleGpoDisabled;
            _isModuleGpoEnabled = isModuleGpoEnabled;
            _getString = getString;
            _getModuleToolTip = getModuleToolTip;
            _enqueue = enqueue;
            _enabledChangedCallback = ModuleEnabledChanged;

            _generalSettings = _settingsRepository.SettingsConfig;
            InitializeItems();

            _generalSettings.AddEnabledModuleChangeNotification(_enabledChangedCallback);
            _settingsRepository.SettingsChanged += OnSettingsChanged;
            _kbmSettingsRepository.SettingsChanged += OnKbmSettingsChanged;
        }

        private static Action<Action> CreateDispatcher()
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            return action => dispatcherQueue?.TryEnqueue(() => action());
        }

        private void OnSettingsChanged(GeneralSettings newSettings)
        {
            QueueRefresh();
        }

        private void QueueRefresh()
        {
            if (_isDisposed)
            {
                return;
            }

            _enqueue(() =>
            {
                if (_isDisposed)
                {
                    return;
                }

                // Notifications may already have been superseded while waiting on the UI thread.
                var settings = _settingsRepository.SettingsConfig;
                if (!ReferenceEquals(_generalSettings, settings))
                {
                    _generalSettings.RemoveEnabledModuleChangeNotification(_enabledChangedCallback);
                    _generalSettings = settings;
                    _generalSettings.AddEnabledModuleChangeNotification(_enabledChangedCallback);
                }

                RefreshItemsVisibility();
            });
        }

        private void InitializeItems()
        {
            AddFlyoutMenuItem(ModuleType.ColorPicker);
            AddFlyoutMenuItem(ModuleType.CmdPal);
            AddFlyoutMenuItem(ModuleType.EnvironmentVariables);
            AddFlyoutMenuItem(ModuleType.FancyZones);
            AddFlyoutMenuItem(ModuleType.Hosts);
            AddFlyoutMenuItem(ModuleType.KeyboardManager);
            AddFlyoutMenuItem(ModuleType.LightSwitch);
            AddFlyoutMenuItem(ModuleType.MousePointerCrosshairs);
            AddFlyoutMenuItem(ModuleType.MouseWithoutBorders);
            AddFlyoutMenuItem(ModuleType.PowerDisplay);
            AddFlyoutMenuItem(ModuleType.PowerLauncher);
            AddFlyoutMenuItem(ModuleType.PowerOCR);
            AddFlyoutMenuItem(ModuleType.RegistryPreview);
            AddFlyoutMenuItem(ModuleType.MeasureTool);
            AddFlyoutMenuItem(ModuleType.ShortcutGuide);
            AddFlyoutMenuItem(ModuleType.Workspaces);
        }

        private void AddFlyoutMenuItem(ModuleType moduleType)
        {
            if (_isModuleGpoDisabled(moduleType))
            {
                return;
            }

            Items.Add(new QuickAccessItem
            {
                Title = _getString(Microsoft.PowerToys.Settings.UI.Library.Helpers.ModuleHelper.GetModuleLabelResourceName(moduleType)),
                Tag = moduleType,
                Visible = GetItemVisibility(moduleType),
                Description = _getModuleToolTip(moduleType),
                Icon = Microsoft.PowerToys.Settings.UI.Library.Helpers.ModuleHelper.GetModuleTypeFluentIconName(moduleType),
                Command = new RelayCommand(() => _launcher.Launch(moduleType)),
            });
        }

        private void ModuleEnabledChanged()
        {
            QueueRefresh();
        }

        private void RefreshItemsVisibility()
        {
            foreach (var item in Items)
            {
                if (item.Tag is ModuleType moduleType)
                {
                    bool visible = GetItemVisibility(moduleType);

                    item.Visible = visible;
                }
            }
        }

        private void OnKbmSettingsChanged(KeyboardManagerSettings newSettings)
        {
            QueueRefresh();
        }

        private bool GetItemVisibility(ModuleType moduleType)
        {
            // Generally, if gpo is enabled or if module enabled, then quick access item is visible.
            bool visible = _isModuleGpoEnabled(moduleType) || Microsoft.PowerToys.Settings.UI.Library.Helpers.ModuleHelper.GetIsModuleEnabled(_generalSettings, moduleType);

            // For KeyboardManager Quick Access item is only shown when using the new editor
            if (moduleType == ModuleType.KeyboardManager)
            {
                visible = visible && _kbmSettingsRepository.SettingsConfig.Properties.UseNewEditor;
            }

            return visible;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _settingsRepository.SettingsChanged -= OnSettingsChanged;
            _kbmSettingsRepository.SettingsChanged -= OnKbmSettingsChanged;
            _generalSettings.RemoveEnabledModuleChangeNotification(_enabledChangedCallback);
            GC.SuppressFinalize(this);
        }

        private static string GetModuleToolTip(ModuleType moduleType)
        {
            return moduleType switch
            {
                ModuleType.ColorPicker => SettingsRepository<ColorPickerSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ActivationShortcut.ToString(),
                ModuleType.FancyZones => SettingsRepository<FancyZonesSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.FancyzonesEditorHotkey.Value.ToString(),
                ModuleType.PowerDisplay => SettingsRepository<PowerDisplaySettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ActivationShortcut.ToString(),
                ModuleType.KeyboardManager => SettingsRepository<KeyboardManagerSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.DefaultEditorShortcut.ToString(),
                ModuleType.LightSwitch => SettingsRepository<LightSwitchSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ToggleThemeHotkey.Value.ToString(),
                ModuleType.PowerLauncher => SettingsRepository<PowerLauncherSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.OpenPowerLauncher.ToString(),
                ModuleType.PowerOCR => SettingsRepository<PowerOcrSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ActivationShortcut.ToString(),
                ModuleType.Workspaces => SettingsRepository<WorkspacesSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.Hotkey.Value.ToString(),
                ModuleType.MeasureTool => SettingsRepository<MeasureToolSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ActivationShortcut.ToString(),
                ModuleType.ShortcutGuide => SettingsRepository<ShortcutGuideSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.OpenShortcutGuide.ToString(),
                ModuleType.MouseWithoutBorders => SettingsRepository<MouseWithoutBordersSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ReconnectShortcut.ToString(),
                ModuleType.MousePointerCrosshairs => SettingsRepository<MousePointerCrosshairsSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ActivationShortcut.ToString(),
                _ => string.Empty,
            };
        }
    }
}
