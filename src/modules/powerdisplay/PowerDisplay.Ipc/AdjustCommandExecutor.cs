// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Common.Services;
using PowerDisplay.Contracts;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Ipc;

/// <summary>
/// App-side executor for the relative <c>up</c>/<c>down</c> IPC commands. Resolves the target
/// monitor, looks up the continuous-setting descriptor, computes the clamped new value from the
/// monitor's current value and the step (explicit or the settings default), performs the DDC/CI or
/// WMI write, and returns the shared <see cref="CliSetResult"/> with before/after values.
/// <para>
/// Only continuous settings (brightness, contrast, volume) are adjustable: an unknown setting name
/// is an <c>ARGUMENT_ERROR</c>; a known-but-discrete setting (e.g. color-temperature) is
/// <c>UNSUPPORTED_FEATURE</c>. The CLI never sends those — this is app-side defense in depth.
/// </para>
/// </summary>
public static class AdjustCommandExecutor
{
    public static async Task<(CliSetResult? Result, CliErrorResult? Error)> ExecuteAsync(
        IMonitorManager manager,
        IReadOnlyList<Monitor> snapshot,
        IReadOnlySet<string> hidden,
        AdjustRequest req,
        bool isUp,
        int defaultStep,
        CancellationToken ct)
    {
        var commandName = isUp ? CliCommandNames.Up : CliCommandNames.Down;

        var visible = MonitorDtoProjector.ExcludeHidden(snapshot, hidden);

        var (monitor, resolveError) = MonitorDtoProjector.ResolveMonitor(visible, req.MonitorNumber, req.MonitorId);
        if (resolveError is not null)
        {
            return (null, new CliErrorResult { Command = commandName, Error = resolveError });
        }

        var monitorRef = MonitorDtoProjector.ToRef(monitor!);
        var setting = req.Setting?.Trim().ToLowerInvariant() ?? string.Empty;

        var descriptor = CliSettingCatalog.TryGet(setting);
        if (descriptor is null)
        {
            return (null, new CliErrorResult
            {
                Command = commandName,
                Monitor = monitorRef,
                Error = new CliError
                {
                    Code = CliErrorCodes.ArgumentError,
                    MessageId = CliMessageIds.UnknownSettingAdjust,
                    Value = req.Setting,
                },
            });
        }

        if (descriptor.Kind != CliSettingKind.Continuous)
        {
            return (null, new CliErrorResult
            {
                Command = commandName,
                Monitor = monitorRef,
                Error = new CliError
                {
                    Code = CliErrorCodes.UnsupportedFeature,
                    MessageId = CliMessageIds.NotAdjustable,
                    Setting = setting,
                },
            });
        }

        if (!descriptor.Supports(monitor!))
        {
            return (null, CliErrorFactory.Unsupported(commandName, monitorRef, setting, descriptor.UnsupportedReason));
        }

        var step = req.Step ?? defaultStep;
        if (step < 0)
        {
            return (null, new CliErrorResult
            {
                Command = commandName,
                Monitor = monitorRef,
                Error = new CliError
                {
                    Code = CliErrorCodes.ArgumentError,
                    MessageId = CliMessageIds.OutOfRange,
                    Setting = "step",
                    Value = step.ToString(CultureInfo.InvariantCulture),
                    ExpectedRange = "[0, 2147483647]",
                },
            });
        }

        var beforeKnown = monitor!.ReadValues.HasFlag(descriptor.ReadFlag);

        // Relative adjust is meaningless without a trustworthy starting value. If discovery never
        // read this setting (the capability is advertised but the live VCP read failed),
        // descriptor.Current returns a fabricated default (0 for brightness, 50 for contrast/volume).
        // Adjusting from that would silently turn "up 10" into an absolute write to ~10 on a panel
        // that may have been at any level. Surface it as a hardware failure rather than guessing.
        if (!beforeKnown)
        {
            return (null, new CliErrorResult
            {
                Command = commandName,
                Monitor = monitorRef,
                Error = new CliError
                {
                    Code = CliErrorCodes.HardwareFailure,
                    MessageId = CliMessageIds.AdjustValueUnknown,
                    Setting = setting,
                },
            });
        }

        var current = descriptor.Current(monitor!);
        var delta = isUp ? step : -step;

        // Compute in long so a pathologically large --step cannot overflow int: `current + delta`
        // could wrap negative and Math.Clamp of a wrapped value would invert the direction (an
        // `up` ending at 0). Widen, clamp to [0, 100], then narrow back.
        var newValue = (int)Math.Clamp((long)current + delta, 0, 100);

        var op = await descriptor.Apply(manager, monitor.Id, newValue, ct);

        // The server receives its own app-lifetime token; a client Ctrl+C/deadline only closes the
        // pipe and is not propagated here. If this long write is interrupted by server shutdown
        // before or after the non-interruptible hardware call, surface the cancellation as TIMEOUT
        // when a response can still be returned.
        ct.ThrowIfCancellationRequested();

        if (!op.IsSuccess)
        {
            return (null, CliErrorFactory.HardwareFailure(commandName, monitorRef, op.ErrorMessage));
        }

        return (new CliSetResult
        {
            Command = commandName,
            Monitor = monitorRef,
            Setting = descriptor.Name,

            // beforeKnown is guaranteed true here (the !beforeKnown case returned above).
            BeforeDisplay = current + "%",
            AfterDisplay = newValue + "%",
        }, null);
    }
}
