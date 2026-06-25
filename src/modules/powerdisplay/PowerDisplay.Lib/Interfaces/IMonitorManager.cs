// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Common.Models;

namespace PowerDisplay.Common.Services;

/// <summary>
/// The hardware-write slice of <see cref="MonitorManager"/> the CLI set/apply-profile commands
/// depend on. Exists purely so those commands can be unit-tested against a fake without real
/// hardware. Discovery and compatibility-mode toggling stay on the concrete <see cref="MonitorManager"/>.
/// </summary>
public interface IMonitorManager
{
    Task<MonitorOperationResult> SetBrightnessAsync(string monitorId, int brightness, CancellationToken cancellationToken = default);

    Task<MonitorOperationResult> SetContrastAsync(string monitorId, int contrast, CancellationToken cancellationToken = default);

    Task<MonitorOperationResult> SetVolumeAsync(string monitorId, int volume, CancellationToken cancellationToken = default);

    Task<MonitorOperationResult> SetColorTemperatureAsync(string monitorId, int colorTemperature, CancellationToken cancellationToken = default);

    Task<MonitorOperationResult> SetInputSourceAsync(string monitorId, int inputSource, CancellationToken cancellationToken = default);

    Task<MonitorOperationResult> SetPowerStateAsync(string monitorId, int powerState, CancellationToken cancellationToken = default);

    Task<MonitorOperationResult> SetRotationAsync(string monitorId, int orientation, CancellationToken cancellationToken = default);
}
