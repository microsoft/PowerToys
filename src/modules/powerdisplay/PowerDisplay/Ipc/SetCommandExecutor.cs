// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
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
/// Faithfully reproduces the validation order and exit-code mapping of
/// <c>SetCommand.RunAsync</c> (CLI). Error messages are inlined from the CLI resource strings
/// so the Contracts layer stays self-contained; the CLI maps them to localized strings at render
/// time.
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
        // --- 1. Exclude hidden monitors (mirrors MonitorFiltering.ExcludeHidden) ---
        var visible = MonitorDtoProjector.ExcludeHidden(snapshot, hidden);

        // --- 2. Resolve the target monitor ---
        var (monitor, resolveError) = MonitorDtoProjector.ResolveMonitor(visible, req.MonitorNumber, req.MonitorId);
        if (resolveError is not null)
        {
            return (null, new CliErrorResult { Command = CliCommandNames.Set, Error = resolveError });
        }

        var monitorRef = MonitorDtoProjector.ToRef(monitor!);
        var setting = req.Setting?.Trim().ToLowerInvariant() ?? string.Empty;

        // --- 3. Dispatch to the per-setting handler (mirrors SetCommand.RunAsync if-chain) ---
        switch (setting)
        {
            case "brightness":
                return await ApplyContinuousAsync(
                    manager,
                    monitor!.Id,
                    monitorRef,
                    "brightness",
                    req.RawValue,
                    monitor.SupportsBrightness,
                    monitor.CurrentBrightness,
                    monitor.ReadValues.HasFlag(MonitorReadFlags.Brightness),
                    "monitor exposed neither a WMI brightness interface nor DDC/CI brightness (0x10)",
                    (mm, id, v, c) => mm.SetBrightnessAsync(id, v, c),
                    ct);

            case "contrast":
                return await ApplyContinuousAsync(
                    manager,
                    monitor!.Id,
                    monitorRef,
                    "contrast",
                    req.RawValue,
                    monitor.SupportsContrast,
                    monitor.CurrentContrast,
                    monitor.ReadValues.HasFlag(MonitorReadFlags.Contrast),
                    "monitor's VCP capabilities did not advertise contrast (0x12)",
                    (mm, id, v, c) => mm.SetContrastAsync(id, v, c),
                    ct);

            case "volume":
                return await ApplyContinuousAsync(
                    manager,
                    monitor!.Id,
                    monitorRef,
                    "volume",
                    req.RawValue,
                    monitor.SupportsVolume,
                    monitor.CurrentVolume,
                    monitor.ReadValues.HasFlag(MonitorReadFlags.Volume),
                    "monitor's VCP capabilities did not advertise audio speaker volume (0x62)",
                    (mm, id, v, c) => mm.SetVolumeAsync(id, v, c),
                    ct);

            case "color-temperature":
                return await ApplyDiscreteAsync(
                    manager,
                    monitor!.Id,
                    monitorRef,
                    "color-temperature",
                    0x14,
                    req.RawValue,
                    monitor.SupportsColorTemperature,
                    monitor.CurrentColorTemperature,
                    monitor.ReadValues.HasFlag(MonitorReadFlags.ColorTemperature),
                    monitor.VcpCapabilitiesInfo?.GetSupportedValues(0x14),
                    "monitor's VCP capabilities did not advertise color preset (0x14)",
                    (mm, id, v, c) => mm.SetColorTemperatureAsync(id, v, c),
                    confirmIfDisplayBlanking: false,
                    ct);

            case "input-source":
                return await ApplyDiscreteAsync(
                    manager,
                    monitor!.Id,
                    monitorRef,
                    "input-source",
                    0x60,
                    req.RawValue,
                    monitor.SupportsInputSource,
                    monitor.CurrentInputSource,
                    monitor.ReadValues.HasFlag(MonitorReadFlags.InputSource),
                    monitor.SupportedInputSources,
                    "monitor's VCP capabilities did not advertise input source (0x60)",
                    (mm, id, v, c) => mm.SetInputSourceAsync(id, v, c),
                    confirmIfDisplayBlanking: false,
                    ct);

            case "power-state":
                return await ApplyDiscreteAsync(
                    manager,
                    monitor!.Id,
                    monitorRef,
                    "power-state",
                    0xD6,
                    req.RawValue,
                    monitor.SupportsPowerState,
                    monitor.CurrentPowerState,
                    monitor.ReadValues.HasFlag(MonitorReadFlags.PowerState),
                    monitor.SupportedPowerStates,
                    "monitor's VCP capabilities did not advertise power mode (0xD6)",
                    (mm, id, v, c) => mm.SetPowerStateAsync(id, v, c),
                    confirmIfDisplayBlanking: !req.ConfirmPowerOff,
                    ct);

            case "orientation":
                return await ApplyOrientationAsync(manager, monitor!, monitorRef, req.RawValue, ct);

            default:
                return (null, new CliErrorResult
                {
                    Command = CliCommandNames.Set,
                    Error = new CliError
                    {
                        // TODO(M4): app should set Code-only; CLI maps Code->localized message
                        Code = CliErrorCodes.ArgumentError,
                        Message = string.Format(CultureInfo.InvariantCulture, "unknown setting '{0}'", req.Setting),
                        Hint = string.Format(
                            CultureInfo.InvariantCulture,
                            "valid settings: {0}",
                            string.Join(", ", CliSettingNames.All)),
                    },
                });
        }
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
            return (null, MakeUnsupportedError(monitorRef, settingName, unsupportedReason));
        }

        // Parse the raw value string to int (the CLI receives it as an already-parsed int; the
        // app receives it as a string from the JSON request, so we parse here).
        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var requested))
        {
            // TODO(M4): app should set Code-only; CLI maps Code->localized message
            return (null, new CliErrorResult
            {
                Command = CliCommandNames.Set,
                Monitor = monitorRef,
                Error = new CliError
                {
                    Code = CliErrorCodes.ArgumentError,
                    Message = string.Format(CultureInfo.InvariantCulture, "'{0}' is not a valid integer for {1}", rawValue, settingName),
                },
            });
        }

        var rangeError = ValidateContinuous(settingName, requested);
        if (rangeError is not null)
        {
            return (null, new CliErrorResult { Command = CliCommandNames.Set, Monitor = monitorRef, Error = rangeError });
        }

        var op = await apply(manager, monitorId, requested, ct);

        // A blocking write that overran --timeout (or Ctrl+C) cancels the token but cannot be
        // interrupted mid-call; surface it as TIMEOUT rather than reporting a false success.
        ct.ThrowIfCancellationRequested();

        if (!op.IsSuccess)
        {
            return (null, MakeHardwareFailureError(monitorRef, op.ErrorMessage, "hardware write failed"));
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
            return (null, MakeUnsupportedError(monitorRef, settingName, unsupportedReason));
        }

        // Resolve (and verify against the monitor's advertised set) BEFORE the power-off
        // confirmation gate — mirrors SetCommand.ApplyDiscreteAsync exactly.
        var resolved = TryResolveDiscrete(vcpCode, settingName, rawValue, supportedValues, out var valueError);
        if (resolved is null)
        {
            return (null, new CliErrorResult { Command = CliCommandNames.Set, Monitor = monitorRef, Error = valueError! });
        }

        // Gate any state that blanks the panel on the already-resolved value.
        if (confirmIfDisplayBlanking && IsDisplayBlanking(resolved.Value))
        {
            // TODO(M4): app should set Code-only; CLI maps Code->localized message
            return (null, new CliErrorResult
            {
                Command = CliCommandNames.Set,
                Monitor = monitorRef,
                Error = new CliError
                {
                    Code = CliErrorCodes.ArgumentError,
                    Message = string.Format(
                        CultureInfo.InvariantCulture,
                        "monitor {0} ({1}): add --confirm-power-off to apply a display-blanking power state",
                        monitorRef.Number,
                        monitorRef.Name),
                    Hint = "use --confirm-power-off to allow setting power states that blank the display",
                },
            });
        }

        var op = await apply(manager, monitorId, resolved.Value, ct);

        // A blocking write that overran --timeout (or Ctrl+C) cancels the token but cannot be
        // interrupted mid-call; surface it as TIMEOUT rather than reporting a false success.
        ct.ThrowIfCancellationRequested();

        if (!op.IsSuccess)
        {
            return (null, MakeHardwareFailureError(monitorRef, op.ErrorMessage, "hardware write failed"));
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
            // TODO(M4): app should set Code-only; CLI maps Code->localized message
            return (null, new CliErrorResult
            {
                Command = CliCommandNames.Set,
                Monitor = monitorRef,
                Error = new CliError
                {
                    Code = CliErrorCodes.UnsupportedFeature,
                    Message = string.Format(
                        CultureInfo.InvariantCulture,
                        "monitor {0} ({1}): rotation is not supported (no GDI device name)",
                        monitorRef.Number,
                        monitorRef.Name),
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

        // A blocking rotation that overran --timeout (or Ctrl+C) cancels the token but cannot be
        // interrupted mid-call; surface it as TIMEOUT rather than reporting a false success.
        ct.ThrowIfCancellationRequested();

        if (!op.IsSuccess)
        {
            return (null, MakeHardwareFailureError(monitorRef, op.ErrorMessage, "ChangeDisplaySettingsEx failed"));
        }

        return (new CliSetResult
        {
            Monitor = monitorRef,
            Setting = "orientation",
            BeforeDisplay = beforeKnown ? MonitorDtoProjector.OrientationDegrees(beforeIndex) : null,
            AfterDisplay = MonitorDtoProjector.OrientationDegrees(index.Value),
        }, null);
    }

    // ─── Inlined validator logic (mirrors CLI Resolution classes) ──────────────

    /// <summary>
    /// Validates that a continuous value (brightness/contrast/volume) is in [0, 100].
    /// Mirrors <c>ContinuousValueValidator.Validate</c>.
    /// </summary>
    private static CliError? ValidateContinuous(string settingName, int value)
    {
        const int Min = 0;
        const int Max = 100;

        if (value < Min || value > Max)
        {
            // TODO(M4): app should set Code-only; CLI maps Code->localized message
            return new CliError
            {
                Code = CliErrorCodes.OutOfRange,
                ExpectedRange = $"[{Min}, {Max}]",
                Message = string.Format(
                    CultureInfo.InvariantCulture,
                    "'{0}' is out of range for {1}; accepted range is [{2}, {3}]",
                    value,
                    settingName,
                    Min,
                    Max),
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
        if (supportedValues is { Count: > 0 } && !ContainsValue(supportedValues, parsedValue.Value))
        {
            error = MakeDiscreteUnsupportedError(settingName, raw, supportedValues, vcpCode);
            return null;
        }

        return parsedValue;
    }

    /// <summary>
    /// Parses a hex literal of the form "0x??". Mirrors <c>DiscreteValueResolver.TryParseHex</c>.
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
    /// Mirrors <c>OrientationResolver.TryResolve</c>.
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
    /// Mirrors <c>SetCommand.IsDisplayBlanking</c>.
    /// </summary>
    private static bool IsDisplayBlanking(int powerState) => powerState is 0x02 or 0x03 or 0x04 or 0x05;

    /// <summary>
    /// Linear scan for <paramref name="value"/> in <paramref name="list"/>. Avoids a LINQ
    /// dependency that could conflict with existing using directives.
    /// </summary>
    private static bool ContainsValue(IReadOnlyList<int> list, int value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
            {
                return true;
            }
        }

        return false;
    }

    // ─── Shared CliError factory helpers ─────────────────────────────────────
    private static CliErrorResult MakeUnsupportedError(CliMonitorRef monitorRef, string settingName, string unsupportedReason)
    {
        // TODO(M4): app should set Code-only; CLI maps Code->localized message
        return new CliErrorResult
        {
            Command = CliCommandNames.Set,
            Monitor = monitorRef,
            Error = new CliError
            {
                Code = CliErrorCodes.UnsupportedFeature,
                Message = string.Format(
                    CultureInfo.InvariantCulture,
                    "monitor {0} ({1}): {2} is not supported",
                    monitorRef.Number,
                    monitorRef.Name,
                    settingName),
                Hint = string.Format(CultureInfo.InvariantCulture, "reason: {0}", unsupportedReason),
            },
        };
    }

    private static CliErrorResult MakeHardwareFailureError(
        CliMonitorRef monitorRef,
        string? errorMessage,
        string fallback)
    {
        // TODO(M4): app should set Code-only; CLI maps Code->localized message
        return new CliErrorResult
        {
            Command = CliCommandNames.Set,
            Monitor = monitorRef,
            Error = new CliError
            {
                Code = CliErrorCodes.HardwareFailure,
                Message = errorMessage ?? fallback,
            },
        };
    }

    private static CliError MakeDiscreteParseError(
        string settingName,
        string raw,
        IReadOnlyList<int>? supportedValues,
        byte vcpCode)
    {
        // TODO(M4): app should set Code-only; CLI maps Code->localized message
        return new CliError
        {
            Code = CliErrorCodes.InvalidDiscreteValue,
            Supported = BuildSupportedList(vcpCode, supportedValues),
            Message = string.Format(CultureInfo.InvariantCulture, "'{0}' is not a valid value for {1}", raw, settingName),
            Hint = "use a hex VCP value (0x??); run 'powerdisplay capabilities -n N --setting <name>' to list supported values",
        };
    }

    private static CliError MakeDiscreteUnsupportedError(
        string settingName,
        string raw,
        IReadOnlyList<int> supportedValues,
        byte vcpCode)
    {
        // TODO(M4): app should set Code-only; CLI maps Code->localized message
        return new CliError
        {
            Code = CliErrorCodes.InvalidDiscreteValue,
            Supported = BuildSupportedList(vcpCode, supportedValues),
            Message = string.Format(CultureInfo.InvariantCulture, "'{0}' is not in the monitor's supported set for {1}", raw, settingName),
            Hint = "use a hex VCP value (0x??); run 'powerdisplay capabilities -n N --setting <name>' to list supported values",
        };
    }

    private static CliError MakeOrientationError(string raw)
    {
        // TODO(M4): app should set Code-only; CLI maps Code->localized message
        return new CliError
        {
            Code = CliErrorCodes.InvalidDiscreteValue,
            Message = string.Format(
                CultureInfo.InvariantCulture,
                "'{0}' is not a valid orientation; accepted values are: 0, 90, 180, 270",
                raw),
            Hint = "specify orientation in degrees: 0, 90, 180, or 270",
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
