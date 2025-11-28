// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerToys.ModuleContracts;

namespace Awake.ModuleServices;

public interface IAwakeService : IModuleService
{
    Task<OperationResult> SetIndefiniteAsync(CancellationToken cancellationToken = default);

    Task<OperationResult> SetTimedAsync(int minutes, CancellationToken cancellationToken = default);

    Task<OperationResult> SetOffAsync(CancellationToken cancellationToken = default);
}
