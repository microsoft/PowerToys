// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Ipc;

/// <summary>
/// Static, per-setting metadata for one of the six VCP settings the CLI read/write commands operate
/// on. One descriptor replaces the parallel hand-maintained switch arms previously spread across
/// <see cref="MonitorDtoProjector"/>, <see cref="SetCommandExecutor"/>, and the apply-profile path,
/// so adding or changing a setting touches a single row in <see cref="CliSettingCatalog"/>.
/// <para>
/// <b>Orientation is intentionally excluded:</b> it is GDI-based (not a VCP code), needs a
/// <c>GdiDeviceName</c>, and maps degrees↔index, so it stays a special case at the call sites.
/// </para>
/// </summary>
/// <param name="Name">Canonical (lower-case) setting name; see <c>CliSettingNames</c>.</param>
/// <param name="Kind">Continuous percentage vs. discrete VCP value.</param>
/// <param name="VcpCode">The VESA MCCS VCP code for this setting.</param>
/// <param name="ReadFlag">The <see cref="MonitorReadFlags"/> bit set when discovery read this setting.</param>
/// <param name="Supports">Selects the monitor's hardware-capability flag for this setting.</param>
/// <param name="Current">Selects the monitor's last-read value for this setting.</param>
/// <param name="SupportedValues">
/// Selects the monitor's advertised discrete value set (used to validate a <c>set</c> value).
/// Returns <see langword="null"/> for continuous settings, which have no discrete set.
/// </param>
/// <param name="Apply">The hardware-write delegate for this setting on <see cref="IMonitorManager"/>.</param>
/// <param name="UnsupportedReason">
/// Invariant English explanation surfaced when the monitor does not support this setting.
/// </param>
/// <param name="BlanksDisplay">
/// True only for settings whose values can blank the panel (power-state); gates the
/// <c>--confirm-power-off</c> requirement.
/// </param>
internal sealed record CliVcpSetting(
    string Name,
    CliSettingKind Kind,
    byte VcpCode,
    MonitorReadFlags ReadFlag,
    Func<Monitor, bool> Supports,
    Func<Monitor, int> Current,
    Func<Monitor, IReadOnlyList<int>?> SupportedValues,
    Func<IMonitorManager, string, int, CancellationToken, Task<MonitorOperationResult>> Apply,
    string UnsupportedReason,
    bool BlanksDisplay = false);
