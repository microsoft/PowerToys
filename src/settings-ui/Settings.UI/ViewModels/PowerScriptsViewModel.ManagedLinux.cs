// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public partial class PowerScriptsViewModel
{
    private bool _isLinuxContainerBusy;
    private bool _isLinuxContainerReady;
    private string _linuxContainerStatus = GetResourceString("PowerScripts_LinuxNotChecked");

    public bool CanPrepareLinuxContainer => !_isLinuxContainerBusy;

    public bool IsLinuxContainerReady => _isLinuxContainerReady;

    public string LinuxContainerStatus => _linuxContainerStatus;

    public async Task RefreshLinuxContainerAsync(bool prepare = false)
    {
        if (_isLinuxContainerBusy)
        {
            return;
        }

        _isLinuxContainerBusy = true;
        _isLinuxContainerReady = false;
        _linuxContainerStatus = GetResourceString(prepare ? "PowerScripts_LinuxPreparing" : "PowerScripts_LinuxChecking");
        NotifyLinuxState();
        try
        {
            var json = await InvokeHostAsync(
                new[] { prepare ? "linux-container-setup" : "linux-container-status" },
                commandTimeout: TimeSpan.FromMinutes(prepare ? 12 : 1));
            var status = JsonSerializer.Deserialize<LinuxContainerSnapshot>(json, JsonOptions)
                ?? throw new InvalidOperationException(GetResourceString("PowerScripts_SettingsInvalidResponse"));
            if (string.IsNullOrWhiteSpace(status.Message))
            {
                throw new InvalidOperationException(GetResourceString("PowerScripts_SettingsInvalidResponse"));
            }

            _isLinuxContainerReady = status.IsReady;
            _linuxContainerStatus = status.Message;
        }
        catch (Exception ex)
        {
            _linuxContainerStatus = ex.Message;
            ManagedCommon.Logger.LogError("Unable to prepare or check the PowerScripts Linux environment.", ex);
        }
        finally
        {
            _isLinuxContainerBusy = false;
            NotifyLinuxState();
        }
    }

    private void NotifyLinuxState()
    {
        OnPropertyChanged(nameof(CanPrepareLinuxContainer));
        OnPropertyChanged(nameof(IsLinuxContainerReady));
        OnPropertyChanged(nameof(LinuxContainerStatus));
        RefreshScriptMxcStates();
    }

    private void ApplyExecutionSummary(PowerScriptListItem script)
    {
        if (script.HasExplicitExecution)
        {
            script.SetExecutionSummary(
                GetResourceString("PowerScripts_ExecutionDescriptor"),
                GetResourceString("PowerScripts_ExecutionDescriptorPolicy"),
                string.Empty,
                managedLinux: false);
            return;
        }

        var python = string.Equals(script.Runtime, "python", StringComparison.OrdinalIgnoreCase);
        string summary;
        string blocked = string.Empty;
        var isolation = script.MxcEffectiveDisplay;
        if (!python || IsWindowsMode)
        {
            summary = GetResourceString(python ? "PowerScripts_ExecutionWindowsPython" : "PowerScripts_ExecutionPowerShell");
        }
        else if (IsWslMode)
        {
            var distribution = string.IsNullOrWhiteSpace(WslDistribution)
                ? GetResourceString("PowerScripts_WslDefaultDistribution")
                : WslDistribution;
            summary = GetResourceString("PowerScripts_ExecutionWsl")
                .Replace("{0}", distribution, StringComparison.Ordinal);
            isolation = GetResourceString("PowerScripts_MxcEffectiveDisabled");
        }
        else if (IsManagedLinuxMode)
        {
            var network = GetResourceString(script.IsNetworkRestrictionEffective
                ? "PowerScripts_LinuxNetworkBlocked"
                : "PowerScripts_LinuxNetworkAllowed");
            summary = GetResourceString("PowerScripts_ExecutionLinux");
            isolation = GetResourceString("PowerScripts_ExecutionLinuxControls")
                .Replace("{0}", network, StringComparison.Ordinal);
            if (!script.IsMxcEffectivelyEnabled || !script.IsFilesystemRestrictionEffective ||
                script.IsUiRestrictionEffective || script.IsLeastPrivilegeRestrictionEffective ||
                !_mxcRiskAccepted)
            {
                blocked = GetResourceString("PowerScripts_ExecutionLinuxBlocked");
            }
            else if (!IsLinuxContainerReady)
            {
                blocked = GetResourceString("PowerScripts_ExecutionLinuxSetupRequired");
            }
        }
        else
        {
            summary = GetResourceString("PowerScripts_EnvironmentDisabledHelp");
            blocked = summary;
            isolation = summary;
        }

        if (!string.IsNullOrWhiteSpace(blocked))
        {
            isolation = GetResourceString("PowerScripts_ExecutionBlocked");
        }

        script.SetExecutionSummary(summary, isolation, blocked, python && IsManagedLinuxMode);
    }

    private sealed class LinuxContainerSnapshot
    {
        public required bool IsReady { get; set; }

        public required string Message { get; set; }
    }
}
