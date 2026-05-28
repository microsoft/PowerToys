// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Cli.Errors;
using PowerDisplay.Cli.Output;
using PowerDisplay.Cli.Resolution;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;
using PowerDisplay.Common.Utils;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Cli.Commands;

public static class SetCommand
{
    public static async Task<int> RunAsync(
        MonitorManager monitorManager,
        SetCommandInputs inputs,
        ICliOutput output,
        CancellationToken cancellationToken)
    {
        var selected = CountSelectedSettings(inputs);
        if (selected == 0)
        {
            output.WriteError(new CliErrorResult
            {
                Command = "set",
                Error = new CliError
                {
                    Code = CliErrorCodes.ArgumentError,
                    ExitCode = CliExitCodes.ArgumentError,
                    Message = "no setting specified; pass one of --brightness/--contrast/--volume/--color-temperature/--input-source/--power-state/--orientation",
                },
            });
            return CliExitCodes.ArgumentError;
        }

        if (selected > 1)
        {
            output.WriteError(new CliErrorResult
            {
                Command = "set",
                Error = new CliError
                {
                    Code = CliErrorCodes.ArgumentError,
                    ExitCode = CliExitCodes.ArgumentError,
                    Message = "only one setting may be applied per 'set' call",
                    Hint = "split into multiple invocations: one --<setting> per call",
                },
            });
            return CliExitCodes.ArgumentError;
        }

        var monitors = await monitorManager.DiscoverMonitorsAsync(cancellationToken);
        var resolution = MonitorResolver.Resolve(monitors, inputs.MonitorNumber, inputs.MonitorId);

        if (resolution.Warning is not null)
        {
            output.WriteWarning(resolution.Warning);
        }

        if (resolution.Error is not null)
        {
            output.WriteError(new CliErrorResult { Command = "set", Error = resolution.Error });
            return resolution.Error.ExitCode;
        }

        var monitor = resolution.Monitor!;
        var monitorRef = ToRef(monitor);

        if (inputs.Brightness is { } brightness)
        {
            return await ApplyContinuousAsync(
                monitorManager,
                monitor,
                monitorRef,
                "brightness",
                brightness,
                monitor.SupportsBrightness,
                monitor.CurrentBrightness,
                "internal panels and external monitors via DDC/CI",
                (mm, id, v, ct) => mm.SetBrightnessAsync(id, v, ct),
                output,
                cancellationToken);
        }

        if (inputs.Contrast is { } contrast)
        {
            return await ApplyContinuousAsync(
                monitorManager,
                monitor,
                monitorRef,
                "contrast",
                contrast,
                monitor.SupportsContrast,
                monitor.CurrentContrast,
                "internal panel exposes only brightness via WmiMonitorBrightness; DDC/CI capabilities are not available",
                (mm, id, v, ct) => mm.SetContrastAsync(id, v, ct),
                output,
                cancellationToken);
        }

        if (inputs.Volume is { } volume)
        {
            return await ApplyContinuousAsync(
                monitorManager,
                monitor,
                monitorRef,
                "volume",
                volume,
                monitor.SupportsVolume,
                monitor.CurrentVolume,
                "monitor's VCP capabilities did not advertise audio speaker volume (0x62)",
                (mm, id, v, ct) => mm.SetVolumeAsync(id, v, ct),
                output,
                cancellationToken);
        }

        if (inputs.ColorTemperature is { } colorTemp)
        {
            return await ApplyDiscreteAsync(
                monitorManager,
                monitor,
                monitorRef,
                "color-temperature",
                0x14,
                colorTemp,
                monitor.SupportsColorTemperature,
                monitor.CurrentColorTemperature,
                monitor.VcpCapabilitiesInfo?.GetSupportedValues(0x14),
                "monitor's VCP capabilities did not advertise color preset (0x14)",
                (mm, id, v, ct) => mm.SetColorTemperatureAsync(id, v, ct),
                output,
                cancellationToken);
        }

        if (inputs.InputSource is { } inputSource)
        {
            return await ApplyDiscreteAsync(
                monitorManager,
                monitor,
                monitorRef,
                "input-source",
                0x60,
                inputSource,
                monitor.SupportsInputSource,
                monitor.CurrentInputSource,
                monitor.SupportedInputSources,
                "monitor's VCP capabilities did not advertise input source (0x60)",
                (mm, id, v, ct) => mm.SetInputSourceAsync(id, v, ct),
                output,
                cancellationToken);
        }

        if (inputs.PowerState is { } powerState)
        {
            return await ApplyDiscreteAsync(
                monitorManager,
                monitor,
                monitorRef,
                "power-state",
                0xD6,
                powerState,
                monitor.SupportsPowerState,
                monitor.CurrentPowerState,
                monitor.SupportedPowerStates,
                "monitor's VCP capabilities did not advertise power mode (0xD6)",
                (mm, id, v, ct) => mm.SetPowerStateAsync(id, v, ct),
                output,
                cancellationToken);
        }

        if (inputs.Orientation is { } orientation)
        {
            return await ApplyOrientationAsync(monitorManager, monitor, monitorRef, orientation, output, cancellationToken);
        }

        // Unreachable: CountSelectedSettings already vetted the inputs.
        return CliExitCodes.ArgumentError;
    }

    public static int CountSelectedSettings(SetCommandInputs inputs)
    {
        int count = 0;
        if (inputs.Brightness.HasValue)
        {
            count++;
        }

        if (inputs.Contrast.HasValue)
        {
            count++;
        }

        if (inputs.Volume.HasValue)
        {
            count++;
        }

        if (inputs.ColorTemperature is not null)
        {
            count++;
        }

        if (inputs.InputSource is not null)
        {
            count++;
        }

        if (inputs.PowerState is not null)
        {
            count++;
        }

        if (inputs.Orientation is not null)
        {
            count++;
        }

        return count;
    }

    internal static string FormatDiscrete(byte vcpCode, int value)
    {
        var name = VcpNames.GetValueName(vcpCode, value);
        return name is null
            ? $"0x{value:X2}"
            : $"{name} (0x{value:X2})";
    }

    internal static string OrientationDegrees(int index) => index switch
    {
        0 => "0°",
        1 => "90°",
        2 => "180°",
        3 => "270°",
        _ => $"index {index}",
    };

    private static async Task<int> ApplyContinuousAsync(
        MonitorManager monitorManager,
        Monitor monitor,
        CliMonitorRef monitorRef,
        string settingName,
        int requested,
        bool supportsCheck,
        int beforeValue,
        string unsupportedReason,
        Func<MonitorManager, string, int, CancellationToken, Task<MonitorOperationResult>> apply,
        ICliOutput output,
        CancellationToken cancellationToken)
    {
        if (!supportsCheck)
        {
            output.WriteError(new CliErrorResult
            {
                Command = "set",
                Monitor = monitorRef,
                Error = new CliError
                {
                    Code = CliErrorCodes.UnsupportedFeature,
                    ExitCode = CliExitCodes.UnsupportedFeature,
                    Setting = settingName,
                    Message = $"Monitor {monitorRef.Number} ({monitorRef.Name}) does not support {settingName} adjustment",
                    Hint = $"reason: {unsupportedReason}",
                },
            });
            return CliExitCodes.UnsupportedFeature;
        }

        var rangeError = ContinuousValueValidator.Validate(settingName, requested);
        if (rangeError is not null)
        {
            output.WriteError(new CliErrorResult { Command = "set", Monitor = monitorRef, Error = rangeError });
            return rangeError.ExitCode;
        }

        var op = await apply(monitorManager, monitor.Id, requested, cancellationToken);
        if (!op.IsSuccess)
        {
            output.WriteError(new CliErrorResult
            {
                Command = "set",
                Monitor = monitorRef,
                Error = new CliError
                {
                    Code = CliErrorCodes.HardwareFailure,
                    ExitCode = CliExitCodes.HardwareFailure,
                    Setting = settingName,
                    Requested = requested.ToString(CultureInfo.InvariantCulture),
                    Message = op.ErrorMessage ?? "Hardware write failed",
                },
            });
            return CliExitCodes.HardwareFailure;
        }

        output.WriteSetResult(new CliSetResult
        {
            Monitor = monitorRef,
            Setting = settingName,
            BeforeRaw = beforeValue,
            AfterRaw = requested,
            BeforeDisplay = beforeValue + "%",
            AfterDisplay = requested + "%",
        });
        return CliExitCodes.Ok;
    }

    private static async Task<int> ApplyDiscreteAsync(
        MonitorManager monitorManager,
        Monitor monitor,
        CliMonitorRef monitorRef,
        string settingName,
        byte vcpCode,
        string raw,
        bool supportsCheck,
        int beforeValue,
        IReadOnlyList<int>? supportedValues,
        string unsupportedReason,
        Func<MonitorManager, string, int, CancellationToken, Task<MonitorOperationResult>> apply,
        ICliOutput output,
        CancellationToken cancellationToken)
    {
        if (!supportsCheck)
        {
            output.WriteError(new CliErrorResult
            {
                Command = "set",
                Monitor = monitorRef,
                Error = new CliError
                {
                    Code = CliErrorCodes.UnsupportedFeature,
                    ExitCode = CliExitCodes.UnsupportedFeature,
                    Setting = settingName,
                    Message = $"Monitor {monitorRef.Number} ({monitorRef.Name}) does not support {settingName} adjustment",
                    Hint = $"reason: {unsupportedReason}",
                },
            });
            return CliExitCodes.UnsupportedFeature;
        }

        var resolved = DiscreteValueResolver.TryResolve(vcpCode, settingName, raw, supportedValues, out var valueError);
        if (resolved is null)
        {
            output.WriteError(new CliErrorResult { Command = "set", Monitor = monitorRef, Error = valueError! });
            return valueError!.ExitCode;
        }

        var op = await apply(monitorManager, monitor.Id, resolved.Value, cancellationToken);
        if (!op.IsSuccess)
        {
            output.WriteError(new CliErrorResult
            {
                Command = "set",
                Monitor = monitorRef,
                Error = new CliError
                {
                    Code = CliErrorCodes.HardwareFailure,
                    ExitCode = CliExitCodes.HardwareFailure,
                    Setting = settingName,
                    Requested = raw,
                    Message = op.ErrorMessage ?? "Hardware write failed",
                },
            });
            return CliExitCodes.HardwareFailure;
        }

        output.WriteSetResult(new CliSetResult
        {
            Monitor = monitorRef,
            Setting = settingName,
            BeforeRaw = beforeValue,
            AfterRaw = resolved.Value,
            BeforeDisplay = FormatDiscrete(vcpCode, beforeValue),
            AfterDisplay = FormatDiscrete(vcpCode, resolved.Value),
        });
        return CliExitCodes.Ok;
    }

    private static async Task<int> ApplyOrientationAsync(
        MonitorManager monitorManager,
        Monitor monitor,
        CliMonitorRef monitorRef,
        string raw,
        ICliOutput output,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(monitor.GdiDeviceName))
        {
            output.WriteError(new CliErrorResult
            {
                Command = "set",
                Monitor = monitorRef,
                Error = new CliError
                {
                    Code = CliErrorCodes.UnsupportedFeature,
                    ExitCode = CliExitCodes.UnsupportedFeature,
                    Setting = OrientationResolver.SettingName,
                    Message = $"Monitor {monitorRef.Number} ({monitorRef.Name}) does not have a GDI device name and cannot be rotated via Windows display settings",
                },
            });
            return CliExitCodes.UnsupportedFeature;
        }

        var index = OrientationResolver.TryResolve(raw, out var error);
        if (index is null)
        {
            output.WriteError(new CliErrorResult { Command = "set", Monitor = monitorRef, Error = error! });
            return error!.ExitCode;
        }

        var beforeIndex = monitor.Orientation;
        var op = await monitorManager.SetRotationAsync(monitor.Id, index.Value, cancellationToken);
        if (!op.IsSuccess)
        {
            output.WriteError(new CliErrorResult
            {
                Command = "set",
                Monitor = monitorRef,
                Error = new CliError
                {
                    Code = CliErrorCodes.HardwareFailure,
                    ExitCode = CliExitCodes.HardwareFailure,
                    Setting = OrientationResolver.SettingName,
                    Requested = raw,
                    Message = op.ErrorMessage ?? "ChangeDisplaySettingsEx failed",
                },
            });
            return CliExitCodes.HardwareFailure;
        }

        output.WriteSetResult(new CliSetResult
        {
            Monitor = monitorRef,
            Setting = OrientationResolver.SettingName,
            BeforeRaw = beforeIndex,
            AfterRaw = index.Value,
            BeforeDisplay = OrientationDegrees(beforeIndex),
            AfterDisplay = OrientationDegrees(index.Value),
        });
        return CliExitCodes.Ok;
    }

    private static CliMonitorRef ToRef(Monitor m) => new()
    {
        Number = m.MonitorNumber,
        Id = m.Id,
        Name = m.Name,
        Method = m.CommunicationMethod,
    };
}
