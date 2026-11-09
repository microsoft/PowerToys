// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;
using PowerDisplay.Contracts;
using PowerDisplay.Models;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Ipc;

/// <summary>
/// App-side executor for the <c>set</c> IPC command. Validates a <see cref="SetRequest"/>
/// against the target monitor's hardware capabilities, performs the DDC/CI or GDI write via
/// <see cref="IMonitorManager"/>, and returns a Contracts result tuple.
/// <para>
/// The "exactly one setting" syntactic check is intentionally omitted — that stays in the CLI
/// argument parser. By the time <see cref="ExecuteAsync"/> is called, <see cref="SetRequest.Setting"/>
/// already names the single target setting.
/// </para>
/// <para>
/// Defines the validation order and exit-code mapping for the <c>set</c> command. Errors carry a
/// <see cref="CliError.Code"/> + <see cref="CliError.MessageId"/> + structured fields only; the CLI
/// owns and localizes the human-readable text (see <c>CliErrorLocalizer</c>).
/// </para>
/// </summary>
public static class SetCommandExecutor
{
    // ─── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates <paramref name="req"/> and executes the hardware write.
    /// </summary>
    /// <param name="manager">The app's live monitor manager.</param>
    /// <param name="snapshot">Pre-discovered monitor list (already filtered by the caller if needed).</param>
    /// <param name="hidden">Set of monitor IDs hidden by user preference.</param>
    /// <param name="req">The set request from the CLI IPC channel.</param>
    /// <param name="customMappings">User-defined names for discrete VCP values.</param>
    /// <param name="ct">
    /// The server's app-lifetime cancellation token. Client Ctrl+C/deadlines are local to the CLI,
    /// only close the pipe, and are not propagated to handlers.
    /// </param>
    /// <returns>
    /// Exactly one of <c>Result</c> or <c>Error</c> is non-null.
    /// </returns>
    public static async Task<(CliSetResult? Result, CliErrorResult? Error)> ExecuteAsync(
        IMonitorManager manager,
        IReadOnlyList<Monitor> snapshot,
        IReadOnlySet<string> hidden,
        SetRequest req,
        CancellationToken ct,
        IReadOnlyList<CustomVcpValueMapping>? customMappings = null)
    {
        // --- 1. Exclude hidden monitors ---
        var visible = MonitorDtoProjector.ExcludeHidden(snapshot, hidden);

        // --- 2. Resolve the target monitor ---
        var (monitor, resolveError) = MonitorDtoProjector.ResolveMonitor(visible, req.MonitorNumber, req.MonitorId);
        if (resolveError is not null)
        {
            return (null, new CliErrorResult { Command = CliCommandNames.Set, Error = resolveError });
        }

        var monitorRef = MonitorDtoProjector.ToRef(monitor!);
        var setting = req.Setting?.Trim().ToLowerInvariant() ?? string.Empty;

        // --- 3. Dispatch to the per-setting handler ---

        // Orientation is GDI-based (not a VCP setting), so it is not in the catalog.
        if (setting == CliSettingNames.Orientation)
        {
            return await ApplyOrientationAsync(manager, monitor!, monitorRef, req.RawValue, ct);
        }

        var descriptor = CliSettingCatalog.TryGet(setting);
        if (descriptor is null)
        {
            return (null, new CliErrorResult
            {
                Command = CliCommandNames.Set,
                Monitor = monitorRef,
                Error = new CliError
                {
                    Code = CliErrorCodes.ArgumentError,
                    MessageId = CliMessageIds.UnknownSetting,
                    Value = req.Setting,
                },
            });
        }

        // Continuous vs. discrete parsing, validation, formatting, and the panel-blanking gate are
        // dispatched polymorphically by the descriptor's ApplySetAsync template method.
        return await descriptor.ApplySetAsync(manager, monitor!, monitorRef, req, ct, customMappings);
    }

    // ─── Orientation ──────────────────────────────────────────────────────────
    private static async Task<(CliSetResult? Result, CliErrorResult? Error)> ApplyOrientationAsync(
        IMonitorManager manager,
        Monitor monitor,
        CliMonitorRef monitorRef,
        string rawValue,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(monitor.GdiDeviceName))
        {
            return (null, new CliErrorResult
            {
                Command = CliCommandNames.Set,
                Monitor = monitorRef,
                Error = new CliError
                {
                    Code = CliErrorCodes.UnsupportedFeature,
                    MessageId = CliMessageIds.Unsupported,
                    Setting = CliSettingNames.Orientation,
                    Detail = "no GDI device name",
                },
            });
        }

        var index = TryResolveOrientation(rawValue, out var error);
        if (index is null)
        {
            return (null, new CliErrorResult { Command = CliCommandNames.Set, Monitor = monitorRef, Error = error! });
        }

        var beforeIndex = monitor.Orientation;
        var beforeKnown = monitor.ReadValues.HasFlag(MonitorReadFlags.Orientation);
        var op = await manager.SetRotationAsync(monitor.Id, index.Value, ct);

        // The server receives its own app-lifetime token; a client Ctrl+C/deadline only closes the
        // pipe and is not propagated here. If server shutdown is observed before or after the
        // non-interruptible rotation call, surface the cancellation as TIMEOUT when a response can
        // still be returned.
        ct.ThrowIfCancellationRequested();

        if (!op.IsSuccess)
        {
            return (null, CliErrorFactory.HardwareFailure(CliCommandNames.Set, monitorRef, op.ErrorMessage));
        }

        return (new CliSetResult
        {
            Monitor = monitorRef,
            Setting = CliSettingNames.Orientation,
            BeforeDisplay = beforeKnown ? MonitorDtoProjector.OrientationDegrees(beforeIndex) : null,
            AfterDisplay = MonitorDtoProjector.OrientationDegrees(index.Value),
        }, null);
    }

    // ─── Value validation / resolution ─────────────────────────────────────────

    /// <summary>
    /// Resolves an orientation degree string (0, 90, 180, 270) into a GDI index (0–3).
    /// </summary>
    private static int? TryResolveOrientation(string raw, out CliError? error)
    {
        error = null;

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var degrees))
        {
            error = MakeOrientationError(raw);
            return null;
        }

        switch (degrees)
        {
            case 0: return 0;
            case 90: return 1;
            case 180: return 2;
            case 270: return 3;
            default:
                error = MakeOrientationError(raw);
                return null;
        }
    }

    // ─── Shared CliError factory helpers ─────────────────────────────────────
    private static CliError MakeOrientationError(string raw)
    {
        return new CliError
        {
            Code = CliErrorCodes.InvalidDiscreteValue,
            MessageId = CliMessageIds.InvalidOrientation,
            Value = raw,
        };
    }
}
