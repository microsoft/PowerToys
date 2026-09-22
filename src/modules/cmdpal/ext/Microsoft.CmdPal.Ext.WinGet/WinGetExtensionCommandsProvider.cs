// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.WinGet.Models;
using Microsoft.CmdPal.Common.WinGet.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WinGet;

public partial class WinGetExtensionCommandsProvider : CommandProvider
{
    private static readonly CompositeFormat InstallingPackage = CompositeFormat.Parse(Properties.Resources.winget_installing_package);
    private static readonly CompositeFormat UninstallingPackage = CompositeFormat.Parse(Properties.Resources.winget_uninstalling_package);
    private static readonly CompositeFormat InstallPackageFinished = CompositeFormat.Parse(Properties.Resources.winget_install_package_finished);
    private static readonly CompositeFormat UninstallPackageFinished = CompositeFormat.Parse(Properties.Resources.winget_uninstall_package_finished);
    private static readonly CompositeFormat OperationFailed = CompositeFormat.Parse(Properties.Resources.winget_operation_failed);
    private static readonly CompositeFormat OperationCanceled = CompositeFormat.Parse(Properties.Resources.winget_operation_canceled);

    private readonly ICommandItem[] _commands;
    private readonly WinGetExtensionPage _page;
    private readonly IWinGetOperationTrackerService _operationTracker;
    private readonly TaskScheduler _uiScheduler;
    private bool _disposed;

    public event EventHandler<string>? NotificationRequested;

    public WinGetExtensionCommandsProvider(
        IWinGetPackageManagerService winGetPackageManagerService,
        IWinGetOperationTrackerService winGetOperationTrackerService,
        TaskScheduler uiScheduler)
    {
        DisplayName = Properties.Resources.winget_display_name;
        Id = "WinGet";
        Icon = Icons.WinGetIcon;

        _operationTracker = winGetOperationTrackerService;
        _uiScheduler = uiScheduler;
        _page = new WinGetExtensionPage(winGetPackageManagerService, winGetOperationTrackerService, uiScheduler);
        _commands = [
            new ListItem(_page),
        ];

        _operationTracker.OperationStarted += OnOperationStarted;
        _operationTracker.OperationCompleted += OnOperationCompleted;
        UpdatePageTitle();
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override void InitializeWithHost(IExtensionHost host) => WinGetExtensionHost.Instance.Initialize(host);

    public void SetAllLookup(Func<string, ICommandItem?> lookupByPackageName, Func<string, ICommandItem?> lookupByProductCode)
    {
        WinGetStatics.AppSearchByPackageFamilyNameCallback = lookupByPackageName;
        WinGetStatics.AppSearchByProductCodeCallback = lookupByProductCode;
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _operationTracker.OperationStarted -= OnOperationStarted;
        _operationTracker.OperationCompleted -= OnOperationCompleted;
        _page.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnOperationStarted(object? sender, WinGetPackageOperationEventArgs e) => QueueOperationFeedback(e.Operation, isStarting: true);

    private void OnOperationCompleted(object? sender, WinGetPackageOperationEventArgs e) => QueueOperationFeedback(e.Operation, isStarting: false);

    private void QueueOperationFeedback(WinGetPackageOperation operation, bool isStarting)
    {
        _ = Task.Factory.StartNew(
            () =>
            {
                if (_disposed)
                {
                    return;
                }

                UpdatePageTitle();
                var format = isStarting
                    ? GetStartingMessageFormat(operation)
                    : operation.State switch
                    {
                        WinGetPackageOperationState.Succeeded => operation.Kind == WinGetPackageOperationKind.Uninstall
                            ? UninstallPackageFinished
                            : InstallPackageFinished,
                        WinGetPackageOperationState.Failed => OperationFailed,
                        WinGetPackageOperationState.Canceled => OperationCanceled,
                        _ => throw new InvalidOperationException($"Unexpected completed WinGet operation state: {operation.State}"),
                    };

                NotificationRequested?.Invoke(this, FormatMessage(format, operation));
            },
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            _uiScheduler);
    }

    private void UpdatePageTitle()
    {
        var activeOperation = _operationTracker.Operations.FirstOrDefault(operation => !operation.IsCompleted);
        _page.Title = activeOperation is null
            ? string.Empty
            : FormatMessage(GetStartingMessageFormat(activeOperation), activeOperation);
    }

    private static CompositeFormat GetStartingMessageFormat(WinGetPackageOperation operation) =>
        operation.Kind == WinGetPackageOperationKind.Uninstall ? UninstallingPackage : InstallingPackage;

    private static string FormatMessage(CompositeFormat format, WinGetPackageOperation operation) =>
        string.Format(
            CultureInfo.CurrentCulture,
            format,
            string.IsNullOrWhiteSpace(operation.PackageName) ? operation.PackageId : operation.PackageName);
}
