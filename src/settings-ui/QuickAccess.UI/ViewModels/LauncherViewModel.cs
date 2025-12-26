// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.QuickAccess.Services;
using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.QuickAccess.ViewModels;

public sealed class LauncherViewModel : Observable
{
    private readonly IQuickAccessCoordinator _coordinator;
    private readonly ISettingsRepository<GeneralSettings> _settingsRepository;
    private readonly ResourceLoader _resourceLoader;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly QuickAccessViewModel _quickAccessViewModel;

    public ObservableCollection<QuickAccessItem> FlyoutMenuItems => _quickAccessViewModel.Items;

    public bool IsUpdateAvailable { get; private set; }

    public LauncherViewModel(IQuickAccessCoordinator coordinator)
    {
        _coordinator = coordinator;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        var settingsUtils = SettingsUtils.Default;
        _settingsRepository = SettingsRepository<GeneralSettings>.GetInstance(settingsUtils);

        _resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;

        _quickAccessViewModel = new QuickAccessViewModel(
            _settingsRepository,
            new Microsoft.PowerToys.QuickAccess.Services.QuickAccessLauncher(_coordinator),
            moduleType => Helpers.ModuleGpoHelper.GetModuleGpoConfiguration(moduleType) == GpoRuleConfigured.Disabled,
            _resourceLoader);
        var updatingSettings = UpdatingSettings.LoadSettings() ?? new UpdatingSettings();
        IsUpdateAvailable = updatingSettings.State is UpdatingSettings.UpdatingState.ReadyToInstall or UpdatingSettings.UpdatingState.ReadyToDownload;
    }
}
