// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
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

    public static IconInfo DeleteIcon { get; } = new("\uE74D"); // Delete

    public event EventHandler<InstallPackageCommand>? InstallStateChanged;

    private static readonly CompositeFormat UninstallingPackage = System.Text.CompositeFormat.Parse(Properties.Resources.winget_uninstalling_package);
    private static readonly CompositeFormat InstallingPackage = System.Text.CompositeFormat.Parse(Properties.Resources.winget_installing_package);
    private static readonly CompositeFormat InstallPackageFinished = System.Text.CompositeFormat.Parse(Properties.Resources.winget_install_package_finished);
    private static readonly CompositeFormat UninstallPackageFinished = System.Text.CompositeFormat.Parse(Properties.Resources.winget_uninstall_package_finished);
    private static readonly CompositeFormat QueuedPackageDownload = System.Text.CompositeFormat.Parse(Properties.Resources.winget_queued_package_download);
    private static readonly CompositeFormat InstallPackageFinishing = System.Text.CompositeFormat.Parse(Properties.Resources.winget_install_package_finishing);
    private static readonly CompositeFormat QueuedPackageUninstall = System.Text.CompositeFormat.Parse(Properties.Resources.winget_queued_package_uninstall);
    private static readonly CompositeFormat UninstallPackageFinishing = System.Text.CompositeFormat.Parse(Properties.Resources.winget_uninstall_package_finishing);
    private static readonly CompositeFormat DownloadProgress = System.Text.CompositeFormat.Parse(Properties.Resources.winget_download_progress);

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
        Name = IsInstalled ? Properties.Resources.winget_uninstall_name : Properties.Resources.winget_install_name;
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
            _installBanner.Message = string.Format(CultureInfo.CurrentCulture, UninstallingPackage, _package.Name);
            WinGetExtensionHost.Instance.ShowStatus(_installBanner, StatusContext.Extension);

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
            _installBanner.Message = string.Format(CultureInfo.CurrentCulture, InstallingPackage, _package.Name);
            WinGetExtensionHost.Instance.ShowStatus(_installBanner, StatusContext.Extension);

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
            _installBanner.Message = IsInstalled ?
                string.Format(CultureInfo.CurrentCulture, UninstallPackageFinished, _package.Name) :
                string.Format(CultureInfo.CurrentCulture, InstallPackageFinished, _package.Name);

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
        switch (progress.State)
        {
            case PackageInstallProgressState.Queued:
                _installBanner.Message = string.Format(CultureInfo.CurrentCulture, QueuedPackageDownload, _package.Name);
                break;
            case PackageInstallProgressState.Downloading:
                if (progress.BytesRequired > 0)
                {
                    var downloadText = string.Format(CultureInfo.CurrentCulture, DownloadProgress, FormatBytes(progress.BytesDownloaded), FormatBytes(progress.BytesRequired));
                    _installBanner.Progress ??= new ProgressState() { IsIndeterminate = false };
                    var downloaded = progress.BytesDownloaded / (float)progress.BytesRequired;
                    var percent = downloaded * 100.0f;
                    ((ProgressState)_installBanner.Progress).ProgressPercent = (uint)percent;
                    _installBanner.Message = downloadText;
                }

                break;
            case PackageInstallProgressState.Installing:
                _installBanner.Message = string.Format(CultureInfo.CurrentCulture, InstallingPackage, _package.Name);
                _installBanner.Progress = new ProgressState() { IsIndeterminate = true };
                break;
            case PackageInstallProgressState.PostInstall:
                _installBanner.Message = string.Format(CultureInfo.CurrentCulture, InstallPackageFinishing, _package.Name);
                break;
            case PackageInstallProgressState.Finished:
                _installBanner.Message = Properties.Resources.winget_install_finished;

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
                _installBanner.Message = string.Format(CultureInfo.CurrentCulture, QueuedPackageUninstall, _package.Name);
                break;

            case PackageUninstallProgressState.Uninstalling:
                _installBanner.Message = string.Format(CultureInfo.CurrentCulture, UninstallingPackage, _package.Name);
                _installBanner.Progress = new ProgressState() { IsIndeterminate = true };
                break;
            case PackageUninstallProgressState.PostUninstall:
                _installBanner.Message = string.Format(CultureInfo.CurrentCulture, UninstallPackageFinishing, _package.Name);
                break;
            case PackageUninstallProgressState.Finished:
                _installBanner.Message = Properties.Resources.winget_uninstall_finished;
                _installBanner.Progress = null;
                _installBanner.State = MessageState.Success;
                break;
            default:
                _installBanner.Message = string.Empty;
                break;
        }
    }
}
