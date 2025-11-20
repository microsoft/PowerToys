// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.QuickAccess.Helpers;
using Microsoft.PowerToys.QuickAccess.Services;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.QuickAccess.ViewModels;

public sealed class LauncherViewModel : Observable
{
    private readonly IQuickAccessCoordinator _coordinator;
    private readonly ISettingsRepository<GeneralSettings> _settingsRepository;
    private readonly ResourceLoader _resourceLoader;
    private GeneralSettings _generalSettings;

    public ObservableCollection<FlyoutMenuItem> FlyoutMenuItems { get; }

    public bool IsUpdateAvailable { get; private set; }

    public LauncherViewModel(IQuickAccessCoordinator coordinator)
    {
        _coordinator = coordinator;
        var settingsUtils = new SettingsUtils();
        _settingsRepository = SettingsRepository<GeneralSettings>.GetInstance(settingsUtils);
        _generalSettings = _settingsRepository.SettingsConfig;
        _generalSettings.AddEnabledModuleChangeNotification(ModuleEnabledChanged);

        _resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
        FlyoutMenuItems = new ObservableCollection<FlyoutMenuItem>();

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

    private void AddFlyoutMenuItem(ModuleType moduleType)
    {
        if (ModuleHelper.GetModuleGpoConfiguration(moduleType) == GpoRuleConfigured.Disabled)
        {
            return;
        }

        FlyoutMenuItems.Add(new FlyoutMenuItem
        {
            Label = _resourceLoader.GetString(ModuleHelper.GetModuleLabelResourceName(moduleType)),
            Tag = moduleType,
            Visible = ModuleHelper.GetIsModuleEnabled(_generalSettings, moduleType),
            ToolTip = GetModuleToolTip(moduleType),
            Icon = ModuleHelper.GetModuleTypeFluentIconName(moduleType),
        });
    }

    private string GetModuleToolTip(ModuleType moduleType)
    {
        return moduleType switch
        {
            ModuleType.ColorPicker => SettingsRepository<ColorPickerSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut.ToString(),
            ModuleType.FancyZones => SettingsRepository<FancyZonesSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.FancyzonesEditorHotkey.Value.ToString(),
            ModuleType.PowerLauncher => SettingsRepository<PowerLauncherSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.OpenPowerLauncher.ToString(),
            ModuleType.PowerOCR => SettingsRepository<PowerOcrSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut.ToString(),
            ModuleType.Workspaces => SettingsRepository<WorkspacesSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.Hotkey.Value.ToString(),
            ModuleType.MeasureTool => SettingsRepository<MeasureToolSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut.ToString(),
            ModuleType.ShortcutGuide => GetShortcutGuideToolTip(),
            _ => string.Empty,
        };
    }

    private string GetShortcutGuideToolTip()
    {
        var shortcutGuideSettings = SettingsRepository<ShortcutGuideSettings>.GetInstance(new SettingsUtils()).SettingsConfig;
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
            item.Visible = ModuleHelper.GetIsModuleEnabled(_generalSettings, item.Tag);
        }
    }
}
