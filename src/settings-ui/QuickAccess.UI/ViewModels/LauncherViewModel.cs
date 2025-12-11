// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Threading;
using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.QuickAccess.Helpers;
using Microsoft.PowerToys.QuickAccess.Services;
using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.ApplicationModel.Resources;
using PowerToys.Interop;

namespace Microsoft.PowerToys.QuickAccess.ViewModels;

public sealed class LauncherViewModel : Observable
{
    private readonly IQuickAccessCoordinator _coordinator;
    private readonly ISettingsRepository<GeneralSettings> _settingsRepository;
    private readonly ResourceLoader _resourceLoader;
    private readonly DispatcherQueue _dispatcherQueue;
    private GeneralSettings _generalSettings;

    public ObservableCollection<QuickAccessItem> FlyoutMenuItems { get; }

    public bool IsUpdateAvailable { get; private set; }

    public LauncherViewModel(IQuickAccessCoordinator coordinator)
    {
        _coordinator = coordinator;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        var settingsUtils = SettingsUtils.Default;
        _settingsRepository = SettingsRepository<GeneralSettings>.GetInstance(settingsUtils);
        _generalSettings = _settingsRepository.SettingsConfig;
        _generalSettings.AddEnabledModuleChangeNotification(ModuleEnabledChanged);
        _settingsRepository.SettingsChanged += OnSettingsChanged;

        _resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
        FlyoutMenuItems = new ObservableCollection<QuickAccessItem>();

        AddFlyoutMenuItem(ModuleType.ColorPicker);
        AddFlyoutMenuItem(ModuleType.CmdPal);
        AddFlyoutMenuItem(ModuleType.EnvironmentVariables);
        AddFlyoutMenuItem(ModuleType.FancyZones);
        AddFlyoutMenuItem(ModuleType.Hosts);
        AddFlyoutMenuItem(ModuleType.PowerLauncher);
        AddFlyoutMenuItem(ModuleType.PowerOCR);
        AddFlyoutMenuItem(ModuleType.RegistryPreview);
        AddFlyoutMenuItem(ModuleType.MeasureTool);
        AddFlyoutMenuItem(ModuleType.ShortcutGuide);
        AddFlyoutMenuItem(ModuleType.Workspaces);

        var updatingSettings = UpdatingSettings.LoadSettings() ?? new UpdatingSettings();
        IsUpdateAvailable = updatingSettings.State is UpdatingSettings.UpdatingState.ReadyToInstall or UpdatingSettings.UpdatingState.ReadyToDownload;
    }

    private void OnSettingsChanged(GeneralSettings newSettings)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            ModuleEnabledChanged();
        });
    }

    private void AddFlyoutMenuItem(ModuleType moduleType)
    {
        if (ModuleHelper.GetModuleGpoConfiguration(moduleType) == GpoRuleConfigured.Disabled)
        {
            return;
        }

        FlyoutMenuItems.Add(new QuickAccessItem
        {
            Title = _resourceLoader.GetString(ModuleHelper.GetModuleLabelResourceName(moduleType)),
            Tag = moduleType,
            Visible = ModuleHelper.GetIsModuleEnabled(_generalSettings, moduleType),
            Description = GetModuleToolTip(moduleType),
            Icon = ModuleHelper.GetModuleTypeFluentIconName(moduleType),
            Command = new RelayCommand(() => LaunchModule(moduleType)),
        });
    }

    private void LaunchModule(ModuleType moduleType)
    {
        bool moduleRun = true;

        switch (moduleType)
        {
            case ModuleType.ColorPicker:
                using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowColorPickerSharedEvent()))
                {
                    eventHandle.Set();
                }

                break;
            case ModuleType.EnvironmentVariables:
                {
                    bool launchAdmin = SettingsRepository<EnvironmentVariablesSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.LaunchAdministrator;
                    bool isElevated = _coordinator?.IsRunnerElevated ?? false;
                    string eventName = !isElevated && launchAdmin
                        ? Constants.ShowEnvironmentVariablesAdminSharedEvent()
                        : Constants.ShowEnvironmentVariablesSharedEvent();

                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName))
                    {
                        eventHandle.Set();
                    }
                }

                break;
            case ModuleType.FancyZones:
                using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.FZEToggleEvent()))
                {
                    eventHandle.Set();
                }

                break;
            case ModuleType.Hosts:
                {
                    bool launchAdmin = SettingsRepository<HostsSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.LaunchAdministrator;
                    bool isElevated = _coordinator?.IsRunnerElevated ?? false;
                    string eventName = !isElevated && launchAdmin
                        ? Constants.ShowHostsAdminSharedEvent()
                        : Constants.ShowHostsSharedEvent();

                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName))
                    {
                        eventHandle.Set();
                    }
                }

                break;
            case ModuleType.PowerLauncher:
                using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.PowerLauncherSharedEvent()))
                {
                    eventHandle.Set();
                }

                break;
            case ModuleType.PowerOCR:
                using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowPowerOCRSharedEvent()))
                {
                    eventHandle.Set();
                }

                break;
            case ModuleType.RegistryPreview:
                using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.RegistryPreviewTriggerEvent()))
                {
                    eventHandle.Set();
                }

                break;
            case ModuleType.MeasureTool:
                using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.MeasureToolTriggerEvent()))
                {
                    eventHandle.Set();
                }

                break;
            case ModuleType.ShortcutGuide:
                using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShortcutGuideTriggerEvent()))
                {
                    eventHandle.Set();
                }

                break;
            case ModuleType.CmdPal:
                using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowCmdPalEvent()))
                {
                    eventHandle.Set();
                }

                break;
            case ModuleType.Workspaces:
                using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.WorkspacesLaunchEditorEvent()))
                {
                    eventHandle.Set();
                }

                break;
            default:
                moduleRun = false;
                break;
        }

        if (moduleRun)
        {
            _coordinator?.OnModuleLaunched(moduleType);
        }

        _coordinator?.HideFlyout();
    }

    private string GetModuleToolTip(ModuleType moduleType)
    {
        return moduleType switch
        {
            ModuleType.ColorPicker => SettingsRepository<ColorPickerSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ActivationShortcut.ToString(),
            ModuleType.FancyZones => SettingsRepository<FancyZonesSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.FancyzonesEditorHotkey.Value.ToString(),
            ModuleType.PowerLauncher => SettingsRepository<PowerLauncherSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.OpenPowerLauncher.ToString(),
            ModuleType.PowerOCR => SettingsRepository<PowerOcrSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ActivationShortcut.ToString(),
            ModuleType.Workspaces => SettingsRepository<WorkspacesSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.Hotkey.Value.ToString(),
            ModuleType.MeasureTool => SettingsRepository<MeasureToolSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ActivationShortcut.ToString(),
            ModuleType.ShortcutGuide => GetShortcutGuideToolTip(),
            _ => string.Empty,
        };
    }

    private string GetShortcutGuideToolTip()
    {
        var shortcutGuideSettings = SettingsRepository<ShortcutGuideSettings>.GetInstance(SettingsUtils.Default).SettingsConfig;
        return shortcutGuideSettings.Properties.UseLegacyPressWinKeyBehavior.Value
            ? "Win"
            : shortcutGuideSettings.Properties.OpenShortcutGuide.ToString();
    }

    private void ModuleEnabledChanged()
    {
        _generalSettings = _settingsRepository.SettingsConfig;
        _generalSettings.AddEnabledModuleChangeNotification(ModuleEnabledChanged);
        foreach (var item in FlyoutMenuItems)
        {
            if (item.Tag is ModuleType moduleType)
            {
                item.Visible = ModuleHelper.GetIsModuleEnabled(_generalSettings, moduleType);
            }
        }
    }
}
