// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;
using PowerDisplay.Common.Utils;
using PowerDisplay.Contracts;
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
    /// <param name="ct">Cancellation token (Ctrl+C / timeout).</param>
    /// <returns>
    /// Exactly one of <c>Result</c> or <c>Error</c> is non-null.
    /// </returns>
    public static async Task<(CliSetResult? Result, CliErrorResult? Error)> ExecuteAsync(
        IMonitorManager manager,
        IReadOnlyList<Monitor> snapshot,
        IReadOnlySet<string> hidden,
        SetRequest req,
        CancellationToken ct)
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

        var supports = descriptor.Supports(monitor!);
        var current = descriptor.Current(monitor!);
        var beforeKnown = monitor!.ReadValues.HasFlag(descriptor.ReadFlag);

        if (descriptor.Kind == CliSettingKind.Continuous)
        {
            return await ApplyContinuousAsync(
                manager,
                monitor.Id,
                monitorRef,
                descriptor.Name,
                req.RawValue,
                supports,
                current,
                beforeKnown,
                descriptor.UnsupportedReason,
                descriptor.Apply,
                ct);
        }

        return await ApplyDiscreteAsync(
            manager,
            monitor.Id,
            monitorRef,
            descriptor.Name,
            descriptor.VcpCode,
            req.RawValue,
            supports,
            current,
            beforeKnown,
            descriptor.SupportedValues(monitor),
            descriptor.UnsupportedReason,
            descriptor.Apply,
            confirmIfDisplayBlanking: descriptor.BlanksDisplay && !req.ConfirmPowerOff,
            ct);
    }

    // ─── Continuous settings (brightness / contrast / volume) ─────────────────
    private static async Task<(CliSetResult? Result, CliErrorResult? Error)> ApplyContinuousAsync(
        IMonitorManager manager,
        string monitorId,
        CliMonitorRef monitorRef,
        string settingName,
        string rawValue,
        bool supportsCheck,
        int beforeValue,
        bool beforeKnown,
        string unsupportedReason,
        Func<IMonitorManager, string, int, CancellationToken, Task<MonitorOperationResult>> apply,
        CancellationToken ct)
    {
        if (!supportsCheck)
        {
            return (null, CliErrorFactory.Unsupported(CliCommandNames.Set, monitorRef, settingName, unsupportedReason));
        }

        // Parse the raw value string to int (the CLI receives it as an already-parsed int; the
        // app receives it as a string from the JSON request, so we parse here).
        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var requested))
        {
            return (null, new CliErrorResult
            {
                Command = CliCommandNames.Set,
                Monitor = monitorRef,
                Error = new CliError
                {
                    Code = CliErrorCodes.ArgumentError,
                    MessageId = CliMessageIds.InvalidInteger,
                    Value = rawValue,
                    Setting = settingName,
                },
            });
        }

        var rangeError = ValidateContinuous(settingName, requested);
        if (rangeError is not null)
        {
            return (null, new CliErrorResult { Command = CliCommandNames.Set, Monitor = monitorRef, Error = rangeError });
        }

        var op = await apply(manager, monitorId, requested, ct);

        // A blocking write that overran the CLI timeout (or Ctrl+C) cancels the token but cannot be
        // interrupted mid-call; surface it as TIMEOUT rather than reporting a false success.
        ct.ThrowIfCancellationRequested();

        if (!op.IsSuccess)
        {
            return (null, CliErrorFactory.HardwareFailure(CliCommandNames.Set, monitorRef, op.ErrorMessage));
        }

        return (new CliSetResult
        {
            Monitor = monitorRef,
            Setting = settingName,
            BeforeDisplay = beforeKnown ? beforeValue + "%" : null,
            AfterDisplay = requested + "%",
        }, null);
    }

    // ─── Discrete settings (color-temperature / input-source / power-state) ───
    private static async Task<(CliSetResult? Result, CliErrorResult? Error)> ApplyDiscreteAsync(
        IMonitorManager manager,
        string monitorId,
        CliMonitorRef monitorRef,
        string settingName,
        byte vcpCode,
        string rawValue,
        bool supportsCheck,
        int beforeValue,
        bool beforeKnown,
        IReadOnlyList<int>? supportedValues,
        string unsupportedReason,
        Func<IMonitorManager, string, int, CancellationToken, Task<MonitorOperationResult>> apply,
        bool confirmIfDisplayBlanking,
        CancellationToken ct)
    {
        if (!supportsCheck)
        {
            return (null, CliErrorFactory.Unsupported(CliCommandNames.Set, monitorRef, settingName, unsupportedReason));
        }

        // Resolve (and verify against the monitor's advertised set) BEFORE the power-off
        // confirmation gate.
        var resolved = TryResolveDiscrete(vcpCode, settingName, rawValue, supportedValues, out var valueError);
        if (resolved is null)
        {
            return (null, new CliErrorResult { Command = CliCommandNames.Set, Monitor = monitorRef, Error = valueError! });
        }

        // Gate any state that blanks the panel on the already-resolved value.
        if (confirmIfDisplayBlanking && IsDisplayBlanking(resolved.Value))
        {
            return (null, new CliErrorResult
            {
                Command = CliCommandNames.Set,
                Monitor = monitorRef,
                Error = new CliError
                {
                    Code = CliErrorCodes.ArgumentError,
                    MessageId = CliMessageIds.PowerBlankingConfirm,
                    Setting = settingName,
                },
            });
        }

        var op = await apply(manager, monitorId, resolved.Value, ct);

        // A blocking write that overran the CLI timeout (or Ctrl+C) cancels the token but cannot be
        // interrupted mid-call; surface it as TIMEOUT rather than reporting a false success.
        ct.ThrowIfCancellationRequested();

        if (!op.IsSuccess)
        {
            return (null, CliErrorFactory.HardwareFailure(CliCommandNames.Set, monitorRef, op.ErrorMessage));
        }

        return (new CliSetResult
        {
            Monitor = monitorRef,
            Setting = settingName,
            BeforeDisplay = beforeKnown ? MonitorDtoProjector.FormatDiscrete(vcpCode, beforeValue) : null,
            AfterDisplay = MonitorDtoProjector.FormatDiscrete(vcpCode, resolved.Value),
        }, null);
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

        // A blocking rotation that overran the CLI timeout (or Ctrl+C) cancels the token but cannot be
        // interrupted mid-call; surface it as TIMEOUT rather than reporting a false success.
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
    /// Validates that a continuous value (brightness/contrast/volume) is in [0, 100].
    /// </summary>
    private static CliError? ValidateContinuous(string settingName, int value)
    {
        const int Min = 0;
        const int Max = 100;

        if (value < Min || value > Max)
        {
            return new CliError
            {
                Code = CliErrorCodes.OutOfRange,
                MessageId = CliMessageIds.OutOfRange,
                ExpectedRange = $"[{Min}, {Max}]",
                Value = value.ToString(CultureInfo.InvariantCulture),
                Setting = settingName,
            };
        }

        return null;
    }

    /// <summary>
    /// Resolves a discrete VCP value from a hex literal (0x??), then verifies it against the
    /// monitor's supported set. Friendly names are intentionally NOT accepted: the generic VCP name
    /// table can disagree with a specific monitor's value mapping, so the CLI requires an
    /// unambiguous hex value (use 'capabilities --setting &lt;name&gt;' to discover them).
    /// </summary>
    private static int? TryResolveDiscrete(
        byte vcpCode,
        string settingName,
        string raw,
        IReadOnlyList<int>? supportedValues,
        out CliError? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = MakeDiscreteParseError(settingName, raw, supportedValues, vcpCode);
            return null;
        }

        int? parsedValue = TryParseHex(raw);

        if (parsedValue is null)
        {
            error = MakeDiscreteParseError(settingName, raw, supportedValues, vcpCode);
            return null;
        }

        // If the monitor reports a supported-value set, the resolved value must be in it.
        if (!CliSettingValidation.IsDiscreteValueSupported(parsedValue.Value, supportedValues))
        {
            error = MakeDiscreteUnsupportedError(settingName, raw, supportedValues!, vcpCode);
            return null;
        }

        return parsedValue;
    }

    /// <summary>
    /// Parses a hex literal of the form "0x??".
    /// </summary>
    private static int? TryParseHex(string raw)
    {
        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(raw[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)
            && hex is >= 0x00 and <= 0xFF)
        {
            return hex;
        }

        return null;
    }

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

    // ─── VCP display power-blanking gate ──────────────────────────────────────

    /// <summary>
    /// VCP 0xD6 states that leave a headless caller staring at a dark panel.
    /// </summary>
    private static bool IsDisplayBlanking(int powerState) => powerState is 0x02 or 0x03 or 0x04 or 0x05;

    // ─── Shared CliError factory helpers ─────────────────────────────────────
    private static CliError MakeDiscreteParseError(
        string settingName,
        string raw,
        IReadOnlyList<int>? supportedValues,
        byte vcpCode)
    {
        return new CliError
        {
            Code = CliErrorCodes.InvalidDiscreteValue,
            MessageId = CliMessageIds.InvalidDiscrete,
            Supported = BuildSupportedList(vcpCode, supportedValues),
            Value = raw,
            Setting = settingName,
        };
    }

    private static CliError MakeDiscreteUnsupportedError(
        string settingName,
        string raw,
        IReadOnlyList<int> supportedValues,
        byte vcpCode)
    {
        return new CliError
        {
            Code = CliErrorCodes.InvalidDiscreteValue,
            MessageId = CliMessageIds.DiscreteNotInSet,
            Supported = BuildSupportedList(vcpCode, supportedValues),
            Value = raw,
            Setting = settingName,
        };
    }

    private static CliError MakeOrientationError(string raw)
    {
        return new CliError
        {
            Code = CliErrorCodes.InvalidDiscreteValue,
            MessageId = CliMessageIds.InvalidOrientation,
            Value = raw,
        };
    }

    private static IReadOnlyList<CliSupportedValue>? BuildSupportedList(byte vcpCode, IReadOnlyList<int>? values)
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }

        var list = new List<CliSupportedValue>(values.Count);
        for (int i = 0; i < values.Count; i++)
        {
            var v = values[i];
            list.Add(new CliSupportedValue
            {
                Name = VcpNames.GetValueName(vcpCode, v) ?? $"0x{v:X2}",
                Vcp = $"0x{v:X2}",
            });
        }

        return list;
    }
}
