// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Common.Models;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Common.Services;

/// <summary>
/// The slice of <see cref="MonitorManager"/> the CLI commands depend on. Exists purely so
/// commands can be unit-tested against a fake without real hardware.
/// </summary>
public interface IMonitorManager
{
    void SetMaxCompatibilityMode(bool enabled);

    Task<IReadOnlyList<Monitor>> DiscoverMonitorsAsync(CancellationToken cancellationToken = default);

    Task<MonitorOperationResult> SetBrightnessAsync(string monitorId, int brightness, CancellationToken cancellationToken = default);

    Task<MonitorOperationResult> SetContrastAsync(string monitorId, int contrast, CancellationToken cancellationToken = default);

    Task<MonitorOperationResult> SetVolumeAsync(string monitorId, int volume, CancellationToken cancellationToken = default);

    Task<MonitorOperationResult> SetColorTemperatureAsync(string monitorId, int colorTemperature, CancellationToken cancellationToken = default);

    Task<MonitorOperationResult> SetInputSourceAsync(string monitorId, int inputSource, CancellationToken cancellationToken = default);

    Task<MonitorOperationResult> SetPowerStateAsync(string monitorId, int powerState, CancellationToken cancellationToken = default);

    Task<MonitorOperationResult> SetRotationAsync(string monitorId, int orientation, CancellationToken cancellationToken = default);
}
