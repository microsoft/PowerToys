// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Common.UI;
using ManagedCommon;
using Microsoft.PowerToys.QuickAccess.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using PowerToys.Interop;

namespace Microsoft.PowerToys.QuickAccess.Services;

internal sealed class QuickAccessCoordinator : IQuickAccessCoordinator, IDisposable
{
    private readonly MainWindow _window;
    private readonly QuickAccessLaunchContext _launchContext;
    private readonly SettingsUtils _settingsUtils = SettingsUtils.Default;
    private readonly object _generalSettingsLock = new();
    private readonly object _ipcLock = new();
    private TwoWayPipeMessageIPCManaged? _ipcManager;
    private bool _ipcUnavailableLogged;

    public QuickAccessCoordinator(MainWindow window, QuickAccessLaunchContext launchContext)
    {
        _window = window;
        _launchContext = launchContext;
        InitializeIpc();
    }

    public bool IsRunnerElevated => false; // TODO: wire up real elevation state.

    public void HideFlyout()
    {
        _window.RequestHide();
    }

    public void OpenSettings()
    {
        Common.UI.SettingsDeepLink.OpenSettings(Common.UI.SettingsDeepLink.SettingsWindow.Dashboard, true);
        _window.RequestHide();
    }

    public void OpenGeneralSettingsForUpdates()
    {
        Common.UI.SettingsDeepLink.OpenSettings(Common.UI.SettingsDeepLink.SettingsWindow.Overview, true);
        _window.RequestHide();
    }

    public Task<bool> ShowDocumentationAsync()
    {
        Logger.LogInfo("QuickAccessCoordinator.ShowDocumentationAsync is not yet connected.");
        return Task.FromResult(false);
    }

    public void NotifyUserSettingsInteraction()
    {
        Logger.LogDebug("QuickAccessCoordinator.NotifyUserSettingsInteraction invoked.");
    }

    public bool UpdateModuleEnabled(ModuleType moduleType, bool isEnabled)
    {
        GeneralSettings? updatedSettings = null;
        lock (_generalSettingsLock)
        {
            var repository = SettingsRepository<GeneralSettings>.GetInstance(_settingsUtils);
            var generalSettings = repository.SettingsConfig;
            var current = ModuleHelper.GetIsModuleEnabled(generalSettings, moduleType);
            if (current == isEnabled)
            {
                return false;
            }

            ModuleHelper.SetIsModuleEnabled(generalSettings, moduleType, isEnabled);
            _settingsUtils.SaveSettings(generalSettings.ToJsonString());
            Logger.LogInfo($"QuickAccess updated module '{moduleType}' enabled state to {isEnabled}.");
            updatedSettings = generalSettings;
        }

        if (updatedSettings != null)
        {
            SendGeneralSettingsUpdate(updatedSettings);
        }

        return true;
    }

    public void ReportBug()
    {
        if (!TrySendIpcMessage("{\"bugreport\": 0 }", "bug report request"))
        {
            Logger.LogWarning("QuickAccessCoordinator: failed to dispatch bug report request; IPC unavailable.");
        }
    }

    public void OnModuleLaunched(ModuleType moduleType)
    {
        Logger.LogInfo($"QuickAccessLauncher invoked module {moduleType}.");
    }

    public void Dispose()
    {
        DisposeIpc();
    }

    private void InitializeIpc()
    {
        if (string.IsNullOrEmpty(_launchContext.RunnerPipeName) || string.IsNullOrEmpty(_launchContext.AppPipeName))
        {
            Logger.LogWarning("QuickAccessCoordinator: IPC pipe names not provided. Runner will not receive updates.");
            return;
        }

        try
        {
            _ipcManager = new TwoWayPipeMessageIPCManaged(_launchContext.AppPipeName, _launchContext.RunnerPipeName, OnIpcMessageReceived);
            _ipcManager.Start();
            _ipcUnavailableLogged = false;
        }
        catch (Exception ex)
        {
            Logger.LogError("QuickAccessCoordinator: failed to start IPC channel to runner.", ex);
            DisposeIpc();
        }
    }

    private void OnIpcMessageReceived(string message)
    {
        Logger.LogDebug($"QuickAccessCoordinator received IPC payload: {message}");
    }

    private void SendGeneralSettingsUpdate(GeneralSettings updatedSettings)
    {
        string payload;
        try
        {
            payload = new OutGoingGeneralSettings(updatedSettings).ToString();
        }
        catch (Exception ex)
        {
            Logger.LogError("QuickAccessCoordinator: failed to serialize general settings payload.", ex);
            return;
        }

        TrySendIpcMessage(payload, "general settings update");
    }

    private bool TrySendIpcMessage(string payload, string operationDescription)
    {
        lock (_ipcLock)
        {
            if (_ipcManager == null)
            {
                if (!_ipcUnavailableLogged)
                {
                    _ipcUnavailableLogged = true;
                    Logger.LogWarning($"QuickAccessCoordinator: unable to send {operationDescription} because IPC is not available.");
                }

                return false;
            }

            try
            {
                _ipcManager.Send(payload);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"QuickAccessCoordinator: failed to send {operationDescription}.", ex);
                return false;
            }
        }
    }

    private void DisposeIpc()
    {
        lock (_ipcLock)
        {
            if (_ipcManager == null)
            {
                return;
            }

            try
            {
                _ipcManager.End();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"QuickAccessCoordinator: exception while shutting down IPC. {ex.Message}");
            }

            _ipcManager.Dispose();
            _ipcManager = null;
            _ipcUnavailableLogged = false;
        }
    }
}
