// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CmdPal.Common.WinGet.Models;
using Microsoft.CmdPal.Common.WinGet.Services;

namespace Microsoft.CmdPal.UI.ViewModels.WinGet;

public sealed partial class WinGetOperationViewModel : ObservableObject
{
    private static readonly CompositeFormat DownloadingPercentFormat = CompositeFormat.Parse(Properties.Resources.winget_operation_status_downloading_percent);
    private static readonly CompositeFormat DownloadProgressFormat = CompositeFormat.Parse(Properties.Resources.winget_operation_detail_progress);
    private static readonly CompositeFormat UpdatedFormat = CompositeFormat.Parse(Properties.Resources.winget_operation_detail_updated);
    private static readonly CompositeFormat GigabytesFormat = CompositeFormat.Parse(Properties.Resources.winget_operation_size_gigabytes);
    private static readonly CompositeFormat MegabytesFormat = CompositeFormat.Parse(Properties.Resources.winget_operation_size_megabytes);
    private static readonly CompositeFormat KilobytesFormat = CompositeFormat.Parse(Properties.Resources.winget_operation_size_kilobytes);
    private static readonly CompositeFormat BytesFormat = CompositeFormat.Parse(Properties.Resources.winget_operation_size_bytes);
    private readonly IWinGetOperationTrackerService _trackerService;
    private WinGetPackageOperation _operation;

    public WinGetOperationViewModel(WinGetPackageOperation operation, IWinGetOperationTrackerService trackerService)
    {
        _operation = operation;
        _trackerService = trackerService;
    }

    public Guid OperationId => _operation.OperationId;

    public string PackageId => _operation.PackageId;

    public string PackageName => !string.IsNullOrWhiteSpace(_operation.PackageName) ? _operation.PackageName : _operation.PackageId;

    public bool IsCompleted => _operation.IsCompleted;

    public bool IsActive => !IsCompleted;

    public bool CanCancel => _operation.CanCancel;

    public bool ShowProgressBar => IsActive;

    public bool IsIndeterminate => _operation.IsIndeterminate || !_operation.ProgressPercent.HasValue;

    public double ProgressValue => _operation.ProgressPercent ?? 0;

    public string StatusText => _operation.State switch
    {
        WinGetPackageOperationState.Queued => _operation.Kind == WinGetPackageOperationKind.Uninstall
            ? Properties.Resources.winget_operation_status_queued_uninstall
            : Properties.Resources.winget_operation_status_queued_install,
        WinGetPackageOperationState.Downloading => _operation.ProgressPercent is uint percent
            ? string.Format(CultureInfo.CurrentCulture, DownloadingPercentFormat, percent)
            : Properties.Resources.winget_operation_status_downloading,
        WinGetPackageOperationState.Installing => Properties.Resources.winget_operation_status_installing,
        WinGetPackageOperationState.Uninstalling => Properties.Resources.winget_operation_status_uninstalling,
        WinGetPackageOperationState.PostProcessing => Properties.Resources.winget_operation_status_post_processing,
        WinGetPackageOperationState.Succeeded => _operation.Kind == WinGetPackageOperationKind.Uninstall
            ? Properties.Resources.winget_operation_status_succeeded_uninstall
            : Properties.Resources.winget_operation_status_succeeded_install,
        WinGetPackageOperationState.Canceled => Properties.Resources.winget_operation_status_canceled,
        WinGetPackageOperationState.Failed => Properties.Resources.winget_operation_status_failed,
        _ => _operation.State.ToString(),
    };

    public string DetailText
    {
        get
        {
            if (_operation.State == WinGetPackageOperationState.Downloading
                && _operation.BytesDownloaded is ulong bytesDownloaded
                && _operation.BytesRequired is ulong bytesRequired
                && bytesRequired > 0)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    DownloadProgressFormat,
                    FormatBytes(bytesDownloaded),
                    FormatBytes(bytesRequired));
            }

            if (_operation.State == WinGetPackageOperationState.Failed && !string.IsNullOrWhiteSpace(_operation.ErrorMessage))
            {
                return _operation.ErrorMessage;
            }

            if (_operation.IsCompleted && _operation.CompletedAt is DateTimeOffset completedAt)
            {
                return string.Format(CultureInfo.CurrentCulture, UpdatedFormat, completedAt.ToLocalTime());
            }

            return string.Empty;
        }
    }

    public bool HasDetailText => !string.IsNullOrWhiteSpace(DetailText);

    public void ApplyOperation(WinGetPackageOperation operation)
    {
        _operation = operation;
        NotifyStateChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _trackerService.TryCancelOperation(OperationId);
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(OperationId));
        OnPropertyChanged(nameof(PackageId));
        OnPropertyChanged(nameof(PackageName));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(ShowProgressBar));
        OnPropertyChanged(nameof(IsIndeterminate));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(DetailText));
        OnPropertyChanged(nameof(HasDetailText));
    }

    private static string FormatBytes(ulong bytes)
    {
        const double KB = 1024;
        const double MB = KB * 1024;
        const double GB = MB * 1024;

        return bytes switch
        {
            >= (ulong)GB => string.Format(CultureInfo.CurrentCulture, GigabytesFormat, bytes / GB),
            >= (ulong)MB => string.Format(CultureInfo.CurrentCulture, MegabytesFormat, bytes / MB),
            >= (ulong)KB => string.Format(CultureInfo.CurrentCulture, KilobytesFormat, bytes / KB),
            _ => string.Format(CultureInfo.CurrentCulture, BytesFormat, bytes),
        };
    }
}
