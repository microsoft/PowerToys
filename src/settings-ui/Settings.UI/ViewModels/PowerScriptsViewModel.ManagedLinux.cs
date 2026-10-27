// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public partial class PowerScriptsViewModel
{
    private bool _isLinuxContainerBusy;
    private bool _isLinuxContainerReady;
    private string _linuxContainerStatus = GetResourceString("PowerScripts_LinuxNotChecked");
    private string _linuxContainerImage;

    public bool CanEditLinuxContainerImage => !_isLinuxContainerBusy;

    public bool CanPrepareLinuxContainer => !_isLinuxContainerBusy && !string.IsNullOrWhiteSpace(LinuxContainerImage);

    public bool IsLinuxContainerBusy => _isLinuxContainerBusy;

    public string LinuxContainerStatusTitle { get; private set; } = GetResourceString("PowerScripts_LinuxImageStatusTitle");

    public InfoBarSeverity LinuxContainerStatusSeverity { get; private set; } = InfoBarSeverity.Informational;

    public string LinuxContainerImage
    {
        get => _linuxContainerImage;
        set
        {
            var image = value ?? string.Empty;
            if (_linuxContainerImage == image)
            {
                return;
            }

            _linuxContainerImage = image;
            SavePythonSettings();
            _isLinuxContainerReady = false;
            _linuxContainerStatus = GetResourceString(string.IsNullOrWhiteSpace(image)
                ? "PowerScripts_LinuxImageRequired"
                : "PowerScripts_LinuxNotChecked");
            LinuxContainerStatusTitle = GetResourceString("PowerScripts_LinuxImageStatusTitle");
            LinuxContainerStatusSeverity = InfoBarSeverity.Informational;
            OnPropertyChanged();
            NotifyLinuxState();
        }
    }

    public bool IsLinuxContainerReady => _isLinuxContainerReady;

    public string LinuxContainerStatus => _linuxContainerStatus;

    public async Task RefreshLinuxContainerAsync(bool prepare = false)
    {
        if (_isLinuxContainerBusy)
        {
            return;
        }

        var image = LinuxContainerImage;
        if (string.IsNullOrWhiteSpace(image))
        {
            _isLinuxContainerReady = false;
            _linuxContainerStatus = GetResourceString("PowerScripts_LinuxImageRequired");
            LinuxContainerStatusTitle = GetResourceString("PowerScripts_LinuxImageStatusTitle");
            LinuxContainerStatusSeverity = InfoBarSeverity.Informational;
            NotifyLinuxState();
            return;
        }

        _isLinuxContainerBusy = true;
        _isLinuxContainerReady = false;
        _linuxContainerStatus = GetResourceString(prepare ? "PowerScripts_LinuxPreparing" : "PowerScripts_LinuxChecking");
        LinuxContainerStatusTitle = GetResourceString(prepare ? "PowerScripts_LinuxImagePreparingTitle" : "PowerScripts_LinuxImageCheckingTitle");
        LinuxContainerStatusSeverity = InfoBarSeverity.Informational;
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

            if (!string.Equals(image, LinuxContainerImage, StringComparison.Ordinal))
            {
                return;
            }

            if (!string.Equals(image, status.Image, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(GetResourceString("PowerScripts_SettingsInvalidResponse"));
            }

            _isLinuxContainerReady = status.IsReady;
            _linuxContainerStatus = status.Message;
            LinuxContainerStatusTitle = GetResourceString(status.IsReady ? "PowerScripts_LinuxImageReady" : "PowerScripts_LinuxImageNotReady");
            LinuxContainerStatusSeverity = status.IsReady ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        }
        catch (Exception ex)
        {
            _linuxContainerStatus = ex.Message;
            LinuxContainerStatusTitle = GetResourceString("PowerScripts_LinuxImageNotReady");
            LinuxContainerStatusSeverity = InfoBarSeverity.Error;
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
        OnPropertyChanged(nameof(CanEditLinuxContainerImage));
        OnPropertyChanged(nameof(IsLinuxContainerReady));
        OnPropertyChanged(nameof(IsLinuxContainerBusy));
        OnPropertyChanged(nameof(LinuxContainerStatus));
        OnPropertyChanged(nameof(LinuxContainerStatusTitle));
        OnPropertyChanged(nameof(LinuxContainerStatusSeverity));
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
            summary = string.IsNullOrWhiteSpace(LinuxContainerImage)
                ? GetResourceString("PowerScripts_LinuxImageRequired")
                : GetResourceString("PowerScripts_ExecutionLinux")
                    .Replace("{0}", LinuxContainerImage, StringComparison.Ordinal);
            isolation = GetResourceString("PowerScripts_ExecutionLinuxControls")
                .Replace("{0}", network, StringComparison.Ordinal);
            if (!script.IsMxcEffectivelyEnabled || !script.IsFilesystemRestrictionEffective ||
                script.IsUiRestrictionEffective || script.IsLeastPrivilegeRestrictionEffective ||
                !_mxcRiskAccepted)
            {
                blocked = GetResourceString("PowerScripts_ExecutionLinuxBlocked");
            }
            else if (string.IsNullOrWhiteSpace(LinuxContainerImage))
            {
                blocked = GetResourceString("PowerScripts_LinuxImageRequired");
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

        public required string Image { get; set; }

        public required string Message { get; set; }
    }
}
