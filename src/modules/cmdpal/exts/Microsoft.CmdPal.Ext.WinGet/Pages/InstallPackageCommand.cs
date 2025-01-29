// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.Management.Deployment;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.WinGet.Pages;

public partial class InstallPackageCommand : InvokableCommand
{
    private readonly CatalogPackage _package;

    private readonly StatusMessage _installBanner = new();
    private IAsyncOperationWithProgress<InstallResult, InstallProgress>? _installAction;
    private IAsyncOperationWithProgress<UninstallResult, UninstallProgress>? _unInstallAction;
    private Task? _installTask;

    public bool IsInstalled { get; private set; }

    public static IconInfo CompletedIcon { get; } = new("\uE930"); // Completed

    public static IconInfo DownloadIcon { get; } = new("\uE896"); // Download

    public event EventHandler<InstallPackageCommand>? InstallStateChanged;

    public InstallPackageCommand(CatalogPackage package, bool isInstalled)
    {
        _package = package;
        IsInstalled = isInstalled;
        UpdateAppearance();
    }

    internal void FakeChangeStatus()
    {
        IsInstalled = !IsInstalled;
        UpdateAppearance();
    }

    private void UpdateAppearance()
    {
        Icon = IsInstalled ? CompletedIcon : DownloadIcon;
        Name = IsInstalled ? "Uninstall" : "Install";
    }

    public override ICommandResult Invoke()
    {
        // TODO: LOCK in here, so this can only be invoked once until the
        // install / uninstall is done. Just use like, an atomic
        if (_installTask != null)
        {
            return CommandResult.KeepOpen();
        }

        if (IsInstalled)
        {
            // Uninstall
            _installBanner.State = MessageState.Info;
            _installBanner.Message = $"Uninstalling {_package.Name}...";
            WinGetExtensionHost.Instance.ShowStatus(_installBanner);

            var installOptions = WinGetStatics.WinGetFactory.CreateUninstallOptions();
            installOptions.PackageUninstallScope = PackageUninstallScope.Any;
            _unInstallAction = WinGetStatics.Manager.UninstallPackageAsync(_package, installOptions);

            var handler = new AsyncOperationProgressHandler<UninstallResult, UninstallProgress>(OnUninstallProgress);
            _unInstallAction.Progress = handler;

            _installTask = Task.Run(() => TryDoInstallOperation(_unInstallAction));
        }
        else
        {
            // Install
            _installBanner.State = MessageState.Info;
            _installBanner.Message = $"Installing {_package.Name}...";
            WinGetExtensionHost.Instance.ShowStatus(_installBanner);

            var installOptions = WinGetStatics.WinGetFactory.CreateInstallOptions();
            installOptions.PackageInstallScope = PackageInstallScope.Any;
            _installAction = WinGetStatics.Manager.InstallPackageAsync(_package, installOptions);

            var handler = new AsyncOperationProgressHandler<InstallResult, InstallProgress>(OnInstallProgress);
            _installAction.Progress = handler;

            _installTask = Task.Run(() => TryDoInstallOperation(_installAction));
        }

        return CommandResult.KeepOpen();
    }

    private async void TryDoInstallOperation<T_Operation, T_Progress>(
        IAsyncOperationWithProgress<T_Operation, T_Progress> action)
    {
        try
        {
            await action.AsTask();
            _installBanner.Message = $"Finished {(IsInstalled ? "uninstall" : "install")} for {_package.Name}";
            _installBanner.Progress = null;
            _installBanner.State = MessageState.Success;
            _installTask = null;

            _ = Task.Run(() =>
            {
                Thread.Sleep(2500);
                if (_installTask == null)
                {
                    WinGetExtensionHost.Instance.HideStatus(_installBanner);
                }
            });
            InstallStateChanged?.Invoke(this, this);
        }
        catch (Exception ex)
        {
            _installBanner.State = MessageState.Error;
            _installBanner.Progress = null;
            _installBanner.Message = ex.Message;
            _installTask = null;
        }
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

    private void OnInstallProgress(
        IAsyncOperationWithProgress<InstallResult, InstallProgress> operation,
        InstallProgress progress)
    {
        var downloadText = "Downloading. ";
        switch (progress.State)
        {
            case PackageInstallProgressState.Queued:
                _installBanner.Message = $"Queued {_package.Name} for download...";
                break;
            case PackageInstallProgressState.Downloading:
                if (progress.BytesRequired > 0)
                {
                    downloadText += $"{FormatBytes(progress.BytesDownloaded)} of {FormatBytes(progress.BytesRequired)}";
                    _installBanner.Progress ??= new ProgressState() { IsIndeterminate = false };
                    var downloaded = (float)progress.BytesDownloaded / (float)progress.BytesRequired;
                    var percent = downloaded * 100.0f;
                    ((ProgressState)_installBanner.Progress).ProgressPercent = (uint)percent;
                    _installBanner.Message = downloadText;
                }

                break;
            case PackageInstallProgressState.Installing:
                _installBanner.Message = $"Installing {_package.Name}...";
                _installBanner.Progress = new ProgressState() { IsIndeterminate = true };
                break;
            case PackageInstallProgressState.PostInstall:
                _installBanner.Message = $"Finishing install for {_package.Name}...";
                break;
            case PackageInstallProgressState.Finished:
                _installBanner.Message = "Finished install.";

                // progressBar.IsIndeterminate(false);
                _installBanner.Progress = null;
                _installBanner.State = MessageState.Success;
                break;
            default:
                _installBanner.Message = string.Empty;
                break;
        }
    }

    private void OnUninstallProgress(
        IAsyncOperationWithProgress<UninstallResult, UninstallProgress> operation,
        UninstallProgress progress)
    {
        switch (progress.State)
        {
            case PackageUninstallProgressState.Queued:
                _installBanner.Message = $"Queued {_package.Name} for uninstall...";
                break;

            case PackageUninstallProgressState.Uninstalling:
                _installBanner.Message = $"Uninstalling {_package.Name}...";
                _installBanner.Progress = new ProgressState() { IsIndeterminate = true };
                break;
            case PackageUninstallProgressState.PostUninstall:
                _installBanner.Message = $"Finishing uninstall for {_package.Name}...";
                break;
            case PackageUninstallProgressState.Finished:
                _installBanner.Message = "Finished uninstall.";
                _installBanner.Progress = null;
                _installBanner.State = MessageState.Success;
                break;
            default:
                _installBanner.Message = string.Empty;
                break;
        }
    }
}
