// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.WinGet.Models;
using Microsoft.CmdPal.Common.WinGet.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Management.Deployment;

namespace Microsoft.CmdPal.Ext.WinGet.Pages;

public partial class InstallPackageCommand : InvokableCommand
{
    private readonly IWinGetPackageManagerService _winGetPackageManagerService;
    private readonly IWinGetOperationTrackerService _winGetOperationTrackerService;
    private readonly TaskScheduler _uiScheduler;
    private readonly CatalogPackage _package;
    private readonly string _packageId;
    private readonly string _packageName;
    private readonly StatusMessage _installBanner = new();
    private readonly object _invokeLock = new();
    private Task? _installTask;
    private bool _trackerSubscriptionActive;

    public PackageInstallCommandState InstallCommandState { get; private set; }

    public event EventHandler<InstallPackageCommand>? InstallStateChanged;

    private static readonly CompositeFormat UninstallingPackage = CompositeFormat.Parse(Properties.Resources.winget_uninstalling_package);
    private static readonly CompositeFormat InstallingPackage = CompositeFormat.Parse(Properties.Resources.winget_installing_package);
    private static readonly CompositeFormat InstallPackageFinished = CompositeFormat.Parse(Properties.Resources.winget_install_package_finished);
    private static readonly CompositeFormat UninstallPackageFinished = CompositeFormat.Parse(Properties.Resources.winget_uninstall_package_finished);
    private static readonly CompositeFormat QueuedPackageDownload = CompositeFormat.Parse(Properties.Resources.winget_queued_package_download);
    private static readonly CompositeFormat InstallPackageFinishing = CompositeFormat.Parse(Properties.Resources.winget_install_package_finishing);
    private static readonly CompositeFormat QueuedPackageUninstall = CompositeFormat.Parse(Properties.Resources.winget_queued_package_uninstall);
    private static readonly CompositeFormat UninstallPackageFinishing = CompositeFormat.Parse(Properties.Resources.winget_uninstall_package_finishing);
    private static readonly CompositeFormat DownloadProgress = CompositeFormat.Parse(Properties.Resources.winget_download_progress);

    internal bool SkipDependencies { get; set; }

    public InstallPackageCommand(
        IWinGetPackageManagerService winGetPackageManagerService,
        IWinGetOperationTrackerService winGetOperationTrackerService,
        TaskScheduler uiScheduler,
        CatalogPackage package,
        PackageInstallCommandState isInstalled)
    {
        _winGetPackageManagerService = winGetPackageManagerService;
        _winGetOperationTrackerService = winGetOperationTrackerService;
        _uiScheduler = uiScheduler;
        _package = package;
        _packageId = package.Id;
        _packageName = package.Name ?? package.Id;
        InstallCommandState = isInstalled;
        UpdateAppearance();
    }

    internal void FakeChangeStatus()
    {
        InstallCommandState = InstallCommandState switch
        {
            PackageInstallCommandState.Install => PackageInstallCommandState.Uninstall,
            PackageInstallCommandState.Update => PackageInstallCommandState.Uninstall,
            PackageInstallCommandState.Uninstall => PackageInstallCommandState.Install,
            _ => throw new NotImplementedException(),
        };
        UpdateAppearance();
    }

    private void UpdateAppearance()
    {
        Icon = InstallCommandState switch
        {
            PackageInstallCommandState.Install => Icons.DownloadIcon,
            PackageInstallCommandState.Update => Icons.UpdateIcon,
            PackageInstallCommandState.Uninstall => Icons.DeleteIcon,
            _ => throw new NotImplementedException(),
        };
        Name = InstallCommandState switch
        {
            PackageInstallCommandState.Install => Properties.Resources.winget_install_name,
            PackageInstallCommandState.Update => Properties.Resources.winget_update_name,
            PackageInstallCommandState.Uninstall => Properties.Resources.winget_uninstall_name,
            _ => throw new NotImplementedException(),
        };
    }

    public override ICommandResult Invoke()
    {
        lock (_invokeLock)
        {
            if (_installTask is not null)
            {
                return CommandResult.KeepOpen();
            }

            if (InstallCommandState == PackageInstallCommandState.Uninstall)
            {
                _installBanner.State = MessageState.Info;
                _installBanner.Message = string.Format(CultureInfo.CurrentCulture, UninstallingPackage, _package.Name);
                WinGetExtensionHost.Instance.ShowStatus(_installBanner, StatusContext.Extension);
                _installTask = RunUninstallAsync();
            }
            else if (InstallCommandState is PackageInstallCommandState.Install or
                     PackageInstallCommandState.Update)
            {
                _installBanner.State = MessageState.Info;
                _installBanner.Message = string.Format(CultureInfo.CurrentCulture, InstallingPackage, _package.Name);
                WinGetExtensionHost.Instance.ShowStatus(_installBanner, StatusContext.Extension);
                _installTask = RunInstallAsync();
            }

            return CommandResult.KeepOpen();
        }
    }

    private async Task RunInstallAsync()
    {
        SubscribeToTracker();

        try
        {
            var result = await _winGetPackageManagerService.InstallPackageAsync(_package, SkipDependencies).ConfigureAwait(false);
            await CompleteInstallOperationAsync(result).ConfigureAwait(false);
        }
        finally
        {
            UnsubscribeFromTracker();
        }
    }

    private async Task RunUninstallAsync()
    {
        SubscribeToTracker();

        try
        {
            var result = await _winGetPackageManagerService.UninstallPackageAsync(_package).ConfigureAwait(false);
            await CompleteInstallOperationAsync(result).ConfigureAwait(false);
        }
        finally
        {
            UnsubscribeFromTracker();
        }
    }

    private Task CompleteInstallOperationAsync(WinGetPackageOperationResult result)
    {
        if (result.Succeeded)
        {
            _installBanner.Message = InstallCommandState == PackageInstallCommandState.Uninstall ?
                string.Format(CultureInfo.CurrentCulture, UninstallPackageFinished, _package.Name) :
                string.Format(CultureInfo.CurrentCulture, InstallPackageFinished, _package.Name);

            _installBanner.Progress = null;
            _installBanner.State = MessageState.Success;
            _installTask = null;

            _ = Task.Run(async () =>
            {
                await Task.Delay(2500).ConfigureAwait(false);

                if (_installTask is null)
                {
                    WinGetExtensionHost.Instance.HideStatus(_installBanner);
                }
            });
            InstallStateChanged?.Invoke(this, this);
        }
        else
        {
            _installBanner.State = MessageState.Error;
            _installBanner.Progress = null;
            _installBanner.Message = result.ErrorMessage ?? string.Empty;
            _installTask = null;
        }

        return Task.CompletedTask;
    }

    private static string FormatBytes(ulong bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        return bytes >= GB
            ? $"{bytes / (double)GB:F2} GB"
            : bytes >= MB ?
                $"{bytes / (double)MB:F2} MB"
                : bytes >= KB
                    ? $"{bytes / (double)KB:F2} KB"
                    : $"{bytes} bytes";
    }

    private void SubscribeToTracker()
    {
        if (_trackerSubscriptionActive)
        {
            return;
        }

        _trackerSubscriptionActive = true;
        _winGetOperationTrackerService.OperationStarted += OnTrackedOperationChanged;
        _winGetOperationTrackerService.OperationUpdated += OnTrackedOperationChanged;
    }

    private void UnsubscribeFromTracker()
    {
        if (!_trackerSubscriptionActive)
        {
            return;
        }

        _trackerSubscriptionActive = false;
        _winGetOperationTrackerService.OperationStarted -= OnTrackedOperationChanged;
        _winGetOperationTrackerService.OperationUpdated -= OnTrackedOperationChanged;
    }

    private void OnTrackedOperationChanged(object? sender, WinGetPackageOperationEventArgs e)
    {
        if (!IsMatchingTrackedOperation(e.Operation))
        {
            return;
        }

        _ = Task.Factory.StartNew(
            () => ApplyTrackedOperationToBanner(e.Operation),
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            _uiScheduler);
    }

    private bool IsMatchingTrackedOperation(WinGetPackageOperation operation)
    {
        var expectedKind = InstallCommandState == PackageInstallCommandState.Uninstall
            ? WinGetPackageOperationKind.Uninstall
            : WinGetPackageOperationKind.Install;

        return operation.Kind == expectedKind
            && string.Equals(operation.PackageId, _packageId, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyTrackedOperationToBanner(WinGetPackageOperation operation)
    {
        switch (operation.State)
        {
            case WinGetPackageOperationState.Queued:
                _installBanner.Message = InstallCommandState == PackageInstallCommandState.Uninstall
                    ? string.Format(CultureInfo.CurrentCulture, QueuedPackageUninstall, _packageName)
                    : string.Format(CultureInfo.CurrentCulture, QueuedPackageDownload, _packageName);
                _installBanner.Progress = new ProgressState() { IsIndeterminate = true };
                break;
            case WinGetPackageOperationState.Downloading:
                if (operation.BytesRequired > 0 && operation.BytesDownloaded.HasValue)
                {
                    var downloadText = string.Format(
                        CultureInfo.CurrentCulture,
                        DownloadProgress,
                        FormatBytes(operation.BytesDownloaded.Value),
                        FormatBytes(operation.BytesRequired.Value));
                    _installBanner.Progress ??= new ProgressState() { IsIndeterminate = false };
                    ((ProgressState)_installBanner.Progress).ProgressPercent = operation.ProgressPercent ?? 0;
                    _installBanner.Message = downloadText;
                }
                else
                {
                    _installBanner.Progress = new ProgressState() { IsIndeterminate = true };
                }

                break;
            case WinGetPackageOperationState.Installing:
                _installBanner.Message = string.Format(CultureInfo.CurrentCulture, InstallingPackage, _packageName);
                _installBanner.Progress = new ProgressState() { IsIndeterminate = true };
                break;
            case WinGetPackageOperationState.Uninstalling:
                _installBanner.Message = string.Format(CultureInfo.CurrentCulture, UninstallingPackage, _packageName);
                _installBanner.Progress = new ProgressState() { IsIndeterminate = true };
                break;
            case WinGetPackageOperationState.PostProcessing:
                _installBanner.Message = InstallCommandState == PackageInstallCommandState.Uninstall
                    ? string.Format(CultureInfo.CurrentCulture, UninstallPackageFinishing, _packageName)
                    : string.Format(CultureInfo.CurrentCulture, InstallPackageFinishing, _packageName);
                _installBanner.Progress = new ProgressState() { IsIndeterminate = true };
                break;
            default:
                return;
        }
    }
}

public enum PackageInstallCommandState
{
    Uninstall = 0,
    Update = 1,
    Install = 2,
}
