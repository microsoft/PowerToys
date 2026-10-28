// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.ProtectedStorage;

public interface IProtectedStorageSetupClient
{
    Task<MaintenanceResult> InspectAsync(CancellationToken cancellationToken = default);

    Task<MaintenanceResult> SyncAsync(CancellationToken cancellationToken = default);

    Task<MaintenanceResult> EnsureReadyAsync(CancellationToken cancellationToken = default);

    Task<MaintenanceResult> RepairAsync(CancellationToken cancellationToken = default);

    Task<MaintenanceResult> RepairBootstrapWithAuthorizationAsync(CancellationToken cancellationToken = default);

    Task<MaintenanceResult> RetryFailedOperationAsync(Guid operationId, CancellationToken cancellationToken = default);
}
