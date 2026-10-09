// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;

namespace PowerDisplay.Ipc.UnitTests;

/// <summary>
/// Shared fake <see cref="IMonitorManager"/> whose every write succeeds. Used by the executor and
/// request-handler tests that need a manager but exercise no hardware-failure path.
/// </summary>
internal sealed class NoOpManager : IMonitorManager
{
    public Task<MonitorOperationResult> SetBrightnessAsync(string id, int v, CancellationToken ct = default) => Task.FromResult(MonitorOperationResult.Success());

    public Task<MonitorOperationResult> SetContrastAsync(string id, int v, CancellationToken ct = default) => Task.FromResult(MonitorOperationResult.Success());

    public Task<MonitorOperationResult> SetVolumeAsync(string id, int v, CancellationToken ct = default) => Task.FromResult(MonitorOperationResult.Success());

    public Task<MonitorOperationResult> SetColorTemperatureAsync(string id, int v, CancellationToken ct = default) => Task.FromResult(MonitorOperationResult.Success());

    public Task<MonitorOperationResult> SetInputSourceAsync(string id, int v, CancellationToken ct = default) => Task.FromResult(MonitorOperationResult.Success());

    public Task<MonitorOperationResult> SetPowerStateAsync(string id, int v, CancellationToken ct = default) => Task.FromResult(MonitorOperationResult.Success());

    public Task<MonitorOperationResult> SetRotationAsync(string id, int v, CancellationToken ct = default) => Task.FromResult(MonitorOperationResult.Success());
}
