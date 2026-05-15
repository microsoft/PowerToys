// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.Common.WinGet.Models;
using Microsoft.CmdPal.Common.WinGet.Services;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.WinGet;

public sealed partial class WinGetOperationsViewModel : ObservableObject, IDisposable
{
    private static readonly CompositeFormat ActiveOperationsFormat = CompositeFormat.Parse(Properties.Resources.winget_operations_in_progress_plural);
    private readonly IWinGetOperationTrackerService _trackerService;
    private readonly TaskScheduler _uiScheduler;
    private readonly Dictionary<Guid, WinGetOperationViewModel> _operationViewModels = [];
    private bool _disposed;

    public WinGetOperationsViewModel(IWinGetOperationTrackerService trackerService, TaskScheduler? uiScheduler = null)
    {
        _trackerService = trackerService;
        _uiScheduler = uiScheduler ?? TaskScheduler.Current;

        _trackerService.OperationStarted += OnTrackedOperationChanged;
        _trackerService.OperationUpdated += OnTrackedOperationChanged;
        _trackerService.OperationCompleted += OnTrackedOperationChanged;

        RefreshOperations(_trackerService.Operations);
    }

    public ObservableCollection<WinGetOperationViewModel> Operations { get; } = [];

    public bool HasVisibleOperations => Operations.Count > 0;

    public bool HasActiveOperations => Operations.Any(static operation => operation.IsActive);

    public string SummaryText
    {
        get
        {
            var activeCount = Operations.Count(static operation => operation.IsActive);
            if (activeCount == 0)
            {
                return Properties.Resources.winget_operations_recent_activity;
            }

            return activeCount == 1
                ? Properties.Resources.winget_operations_in_progress_single
                : string.Format(CultureInfo.CurrentCulture, ActiveOperationsFormat, activeCount);
        }
    }

    public string FlyoutHeaderText => HasActiveOperations
        ? Properties.Resources.winget_operations_flyout_active_header
        : Properties.Resources.winget_operations_recent_activity;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _trackerService.OperationStarted -= OnTrackedOperationChanged;
        _trackerService.OperationUpdated -= OnTrackedOperationChanged;
        _trackerService.OperationCompleted -= OnTrackedOperationChanged;
    }

    private void OnTrackedOperationChanged(object? sender, WinGetPackageOperationEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        _ = Task.Factory.StartNew(
            () => RefreshOperations(_trackerService.Operations),
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            _uiScheduler);
    }

    private void RefreshOperations(IReadOnlyList<WinGetPackageOperation> operations)
    {
        HashSet<Guid> activeIds = [];
        List<WinGetOperationViewModel> ordered = new(operations.Count);

        for (var i = 0; i < operations.Count; i++)
        {
            var operation = operations[i];
            activeIds.Add(operation.OperationId);

            if (!_operationViewModels.TryGetValue(operation.OperationId, out var operationViewModel))
            {
                operationViewModel = new WinGetOperationViewModel(operation, _trackerService);
                _operationViewModels[operation.OperationId] = operationViewModel;
            }
            else
            {
                operationViewModel.ApplyOperation(operation);
            }

            ordered.Add(operationViewModel);
        }

        var staleIds = _operationViewModels.Keys
            .Where(id => !activeIds.Contains(id))
            .ToArray();
        for (var i = 0; i < staleIds.Length; i++)
        {
            _operationViewModels.Remove(staleIds[i]);
        }

        ListHelpers.InPlaceUpdateList(Operations, ordered);
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasVisibleOperations));
        OnPropertyChanged(nameof(HasActiveOperations));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(FlyoutHeaderText));
    }
}
