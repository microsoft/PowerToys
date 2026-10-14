// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;

namespace PowerDisplay.Ipc.UnitTests;

/// <summary>
/// Shared fake <see cref="IMonitorManager"/> whose every write fails with a configurable message.
/// Used by the executor tests that exercise the HARDWARE_FAILURE path.
/// </summary>
internal sealed class FailingManager : IMonitorManager
{
    private readonly string _errorMessage;

    public FailingManager(string errorMessage = "simulated hardware failure")
    {
        _errorMessage = errorMessage;
    }

    public Task<MonitorOperationResult> SetBrightnessAsync(string id, int v, CancellationToken ct = default) => Task.FromResult(MonitorOperationResult.Failure(_errorMessage));

    public Task<MonitorOperationResult> SetContrastAsync(string id, int v, CancellationToken ct = default) => Task.FromResult(MonitorOperationResult.Failure(_errorMessage));

    public Task<MonitorOperationResult> SetVolumeAsync(string id, int v, CancellationToken ct = default) => Task.FromResult(MonitorOperationResult.Failure(_errorMessage));

    public Task<MonitorOperationResult> SetColorTemperatureAsync(string id, int v, CancellationToken ct = default) => Task.FromResult(MonitorOperationResult.Failure(_errorMessage));

    public Task<MonitorOperationResult> SetInputSourceAsync(string id, int v, CancellationToken ct = default) => Task.FromResult(MonitorOperationResult.Failure(_errorMessage));

    public Task<MonitorOperationResult> SetPowerStateAsync(string id, int v, CancellationToken ct = default) => Task.FromResult(MonitorOperationResult.Failure(_errorMessage));

    public Task<MonitorOperationResult> SetRotationAsync(string id, int v, CancellationToken ct = default) => Task.FromResult(MonitorOperationResult.Failure(_errorMessage));
}
