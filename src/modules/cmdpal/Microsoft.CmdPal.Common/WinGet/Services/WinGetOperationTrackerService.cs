// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.WinGet.Models;

namespace Microsoft.CmdPal.Common.WinGet.Services;

public sealed class WinGetOperationTrackerService : IWinGetOperationTrackerService
{
    private const int MaxTrackedOperations = 100;

    private static readonly StringComparer OrdinalIgnoreCase = StringComparer.OrdinalIgnoreCase;

    private readonly Lock _operationsLock = new();
    private readonly List<WinGetPackageOperation> _operations = [];
    private readonly Dictionary<Guid, Action> _cancelCallbacks = [];

    public event EventHandler<WinGetPackageOperationEventArgs>? OperationStarted;

    public event EventHandler<WinGetPackageOperationEventArgs>? OperationUpdated;

    public event EventHandler<WinGetPackageOperationEventArgs>? OperationCompleted;

    public IReadOnlyList<WinGetPackageOperation> Operations
    {
        get
        {
            lock (_operationsLock)
            {
                return _operations.ToArray();
            }
        }
    }

    public WinGetPackageOperation? GetLatestOperation(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return null;
        }

        lock (_operationsLock)
        {
            for (var i = 0; i < _operations.Count; i++)
            {
                if (OrdinalIgnoreCase.Equals(_operations[i].PackageId, packageId))
                {
                    return _operations[i];
                }
            }
        }

        return null;
    }

    internal WinGetPackageOperation StartOperation(string packageId, string packageName, WinGetPackageOperationKind kind)
    {
        var now = DateTimeOffset.UtcNow;
        var operation = new WinGetPackageOperation(
            OperationId: Guid.NewGuid(),
            PackageId: packageId,
            PackageName: packageName,
            Kind: kind,
            State: WinGetPackageOperationState.Queued,
            CanCancel: false,
            IsIndeterminate: true,
            ProgressPercent: null,
            BytesDownloaded: null,
            BytesRequired: null,
            ErrorMessage: null,
            StartedAt: now,
            UpdatedAt: now,
            CompletedAt: null);

        lock (_operationsLock)
        {
            _operations.Insert(0, operation);
            TrimCompletedOperationsNoLock();
        }

        OperationStarted?.Invoke(this, new WinGetPackageOperationEventArgs(operation));
        return operation;
    }

    public bool TryCancelOperation(Guid operationId)
    {
        Action? cancelCallback = null;
        WinGetPackageOperation? updated = null;

        lock (_operationsLock)
        {
            var index = FindOperationIndexNoLock(operationId);
            if (index < 0 || _operations[index].IsCompleted || !_cancelCallbacks.Remove(operationId, out cancelCallback) || cancelCallback is null)
            {
                return false;
            }

            updated = _operations[index] with
            {
                CanCancel = false,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            _operations[index] = updated;
        }

        OperationUpdated?.Invoke(this, new WinGetPackageOperationEventArgs(updated));

        try
        {
            cancelCallback();
            return true;
        }
        catch (Exception ex)
        {
            CoreLogger.LogWarning($"Failed to cancel WinGet operation '{operationId}': {ex.Message}");
            return false;
        }
    }

    internal WinGetPackageOperation? RegisterCancellationHandler(Guid operationId, Action cancelCallback)
    {
        ArgumentNullException.ThrowIfNull(cancelCallback);

        WinGetPackageOperation? updated = null;

        lock (_operationsLock)
        {
            var index = FindOperationIndexNoLock(operationId);
            if (index < 0 || _operations[index].IsCompleted)
            {
                return null;
            }

            _cancelCallbacks[operationId] = cancelCallback;
            updated = _operations[index] with
            {
                CanCancel = true,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            _operations[index] = updated;
        }

        OperationUpdated?.Invoke(this, new WinGetPackageOperationEventArgs(updated));
        return updated;
    }

    internal WinGetPackageOperation? UpdateOperation(
        Guid operationId,
        WinGetPackageOperationState state,
        bool isIndeterminate,
        uint? progressPercent = null,
        ulong? bytesDownloaded = null,
        ulong? bytesRequired = null)
    {
        WinGetPackageOperation? updated = null;

        lock (_operationsLock)
        {
            var index = FindOperationIndexNoLock(operationId);
            if (index < 0)
            {
                return null;
            }

            updated = _operations[index] with
            {
                State = state,
                CanCancel = _operations[index].CanCancel,
                IsIndeterminate = isIndeterminate,
                ProgressPercent = progressPercent,
                BytesDownloaded = bytesDownloaded,
                BytesRequired = bytesRequired,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            _operations[index] = updated;
        }

        OperationUpdated?.Invoke(this, new WinGetPackageOperationEventArgs(updated));
        return updated;
    }

    internal WinGetPackageOperation? CompleteOperation(Guid operationId, WinGetPackageOperationState state, string? errorMessage = null)
    {
        WinGetPackageOperation? completed = null;

        lock (_operationsLock)
        {
            var index = FindOperationIndexNoLock(operationId);
            if (index < 0)
            {
                return null;
            }

            _cancelCallbacks.Remove(operationId);

            var now = DateTimeOffset.UtcNow;
            completed = _operations[index] with
            {
                State = state,
                CanCancel = false,
                IsIndeterminate = false,
                ProgressPercent = state == WinGetPackageOperationState.Succeeded ? 100u : _operations[index].ProgressPercent,
                ErrorMessage = errorMessage,
                UpdatedAt = now,
                CompletedAt = now,
            };

            _operations[index] = completed;
            TrimCompletedOperationsNoLock();
        }

        OperationCompleted?.Invoke(this, new WinGetPackageOperationEventArgs(completed));
        return completed;
    }

    private int FindOperationIndexNoLock(Guid operationId)
    {
        for (var i = 0; i < _operations.Count; i++)
        {
            if (_operations[i].OperationId == operationId)
            {
                return i;
            }
        }

        return -1;
    }

    private void TrimCompletedOperationsNoLock()
    {
        if (_operations.Count <= MaxTrackedOperations)
        {
            return;
        }

        for (var i = _operations.Count - 1; i >= 0 && _operations.Count > MaxTrackedOperations; i--)
        {
            if (_operations[i].IsCompleted)
            {
                _operations.RemoveAt(i);
            }
        }
    }
}
