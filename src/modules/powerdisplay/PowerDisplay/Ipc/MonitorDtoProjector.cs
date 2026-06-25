// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;
using PowerDisplay.Contracts;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Ipc;

/// <summary>
/// Pure-function projector that turns the app's rich <see cref="Monitor"/> model into the flat
/// Contracts result DTOs consumed by the CLI renderers. All three read-side commands (list, get,
/// capabilities) are covered.
/// <para>
/// The output is byte-for-byte equivalent to what today's CLI commands produce: same display
/// strings, same error codes/exit codes, same hidden-monitor and selector semantics.
/// </para>
/// </summary>
public static class MonitorDtoProjector
{
    // ─── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the result DTO for the <c>list</c> command.
    /// Hidden monitors are excluded; the returned entry per monitor mirrors
    /// <c>ListCommand.BuildEntry</c> exactly.
    /// </summary>
    public static CliListResult BuildListResult(
        IReadOnlyList<Monitor> monitors,
        IReadOnlySet<string> hiddenIds)
    {
        var visible = ExcludeHidden(monitors, hiddenIds);
        var entries = new List<CliListMonitor>(visible.Count);
        foreach (var m in visible)
        {
            entries.Add(BuildListEntry(m));
        }

        return new CliListResult { Monitors = entries };
    }

    /// <summary>
    /// Builds the result DTO for the <c>get</c> command.
    /// <list type="bullet">
    ///   <item>When no selector (<paramref name="number"/> and <paramref name="id"/> both null/empty)
    ///         all visible monitors are returned.</item>
    ///   <item>Otherwise the selector is resolved; if resolution fails an error DTO is returned.</item>
    ///   <item>An unknown <paramref name="settingFilter"/> yields an <c>ARGUMENT_ERROR</c> error DTO.</item>
    /// </list>
    /// </summary>
    public static (CliGetResult? Result, CliErrorResult? Error) BuildGetResult(
        IReadOnlyList<Monitor> monitors,
        IReadOnlySet<string> hiddenIds,
        int? number,
        string? id,
        string? settingFilter)
    {
        // Convenience overload: delegate to the warning-aware projector and drop the warning
        // (the single source of get-resolution logic lives in BuildGetResultWithWarning).
        var (result, error, _) = BuildGetResultWithWarning(monitors, hiddenIds, number, id, settingFilter);
        return (result, error);
    }

    /// <summary>
    /// Builds the result DTO for the <c>get</c> command, also returning any selector warning.
    /// </summary>
    public static (CliGetResult? Result, CliErrorResult? Error, string? Warning) BuildGetResultWithWarning(
        IReadOnlyList<Monitor> monitors,
        IReadOnlySet<string> hiddenIds,
        int? number,
        string? id,
        string? settingFilter)
    {
        var visible = ExcludeHidden(monitors, hiddenIds);

        if (!number.HasValue && string.IsNullOrEmpty(id))
        {
            if (TryGetUnknownSettingError(settingFilter, out var settingErr))
            {
                return (null, new CliErrorResult { Command = CliCommandNames.Get, Error = settingErr! }, null);
            }

            var allEntries = new List<CliGetMonitorEntry>(visible.Count);
            foreach (var monitor in visible)
            {
                var monRef = ToRef(monitor);
                allEntries.Add(BuildGetEntry(monitor, monRef, settingFilter, out _)!);
            }

            return (new CliGetResult { Monitors = allEntries }, null, null);
        }

        var (selected, warning, resolveError) = ResolveMonitor(visible, number, id);
        if (resolveError is not null)
        {
            return (null, new CliErrorResult { Command = CliCommandNames.Get, Error = resolveError }, warning);
        }

        var mRef = ToRef(selected!);
        var entry = BuildGetEntry(selected!, mRef, settingFilter, out var settingError);
        if (settingError is not null)
        {
            return (null, new CliErrorResult { Command = CliCommandNames.Get, Monitor = mRef, Error = settingError }, warning);
        }

        return (new CliGetResult { Monitors = [entry!] }, null, warning);
    }

    /// <summary>
    /// Builds the result DTO for the <c>capabilities</c> command.
    /// A selector is required; if missing or not found an error DTO is returned.
    /// </summary>
    public static (CliCapabilitiesResult? Result, CliErrorResult? Error) BuildCapabilitiesResult(
        IReadOnlyList<Monitor> monitors,
        IReadOnlySet<string> hiddenIds,
        int? number,
        string? id)
    {
        var visible = ExcludeHidden(monitors, hiddenIds);

        var (selected, _, resolveError) = ResolveMonitor(visible, number, id);
        if (resolveError is not null)
        {
            return (null, new CliErrorResult { Command = CliCommandNames.Capabilities, Error = resolveError });
        }

        var caps = selected!.VcpCapabilitiesInfo;
        var vcpCodes = new List<CliVcpCodeInfo>();

        if (caps is not null)
        {
            foreach (var code in caps.GetSortedVcpCodes())
            {
                List<string>? discreteValues = null;
                if (code.HasDiscreteValues)
                {
                    discreteValues = new List<string>(code.SupportedValues.Count);
                    foreach (var v in code.SupportedValues)
                    {
                        discreteValues.Add(FormatDiscrete(code.Code, v));
                    }
                }

                vcpCodes.Add(new CliVcpCodeInfo
                {
                    Code = code.FormattedCode,
                    Name = code.Name,
                    Continuous = code.IsContinuous,
                    DiscreteValues = discreteValues,
                });
            }
        }

        return (new CliCapabilitiesResult
        {
            // Transport lives in the dedicated top-level CommunicationMethod below, so leave
            // Method off the monitor ref (it is omitted from JSON) to avoid emitting the same
            // value twice in the capabilities envelope. Mirrors CapabilitiesCommand exactly.
            Monitor = new CliMonitorRef
            {
                Number = selected!.MonitorNumber,
                Id = selected!.Id,
                Name = selected!.Name,
            },
            CommunicationMethod = selected!.CommunicationMethod,
            RawCapabilities = selected!.CapabilitiesRaw,
            Model = caps?.Model,
            MccsVersion = caps?.MccsVersion,
            VcpCodes = vcpCodes,
        }, null);
    }

    // ─── Internal helpers (visible for testing) ────────────────────────────────

    /// <summary>
    /// Drops monitors the user hid in PowerDisplay settings. Mirrors
    /// <c>MonitorFiltering.ExcludeHidden</c>.
    /// </summary>
    internal static IReadOnlyList<Monitor> ExcludeHidden(
        IReadOnlyList<Monitor> monitors,
        IReadOnlySet<string> hiddenIds)
    {
        if (hiddenIds.Count == 0)
        {
            return monitors;
        }

        var kept = new List<Monitor>(monitors.Count);
        foreach (var m in monitors)
        {
            if (!hiddenIds.Contains(m.Id))
            {
                kept.Add(m);
            }
        }

        return kept;
    }

    /// <summary>
    /// Resolves the target monitor from the already-filtered list using CLI selector semantics.
    /// Mirrors <c>MonitorResolver.Resolve</c> + <c>MonitorFiltering.ResolveSelected</c>.
    /// <list type="bullet">
    ///   <item>No selector → <c>SelectorMissing</c> error.</item>
    ///   <item>Both selectors → id wins; warning string is set.</item>
    ///   <item>Not found → <c>MonitorNotFound</c> error.</item>
    /// </list>
    /// </summary>
    internal static (Monitor? Monitor, string? Warning, CliError? Error) ResolveMonitor(
        IReadOnlyList<Monitor> monitors,
        int? monitorNumber,
        string? monitorId)
    {
        var hasNumber = monitorNumber.HasValue;
        var hasId = !string.IsNullOrEmpty(monitorId);

        if (!hasNumber && !hasId)
        {
            return (null, null, new CliError
            {
                Code = CliErrorCodes.SelectorMissing,
                Message = "one of --monitor-number/-n or --monitor-id/-i is required",
                Hint = "run 'powerdisplay list' to see available monitors",
            });
        }

        string? warning = null;
        if (hasNumber && hasId)
        {
            warning = string.Format(
                CultureInfo.InvariantCulture,
                "warning: --monitor-number {0} ignored because --monitor-id was also provided",
                monitorNumber!.GetValueOrDefault());
        }

        if (hasId)
        {
            for (int i = 0; i < monitors.Count; i++)
            {
                if (string.Equals(monitors[i].Id, monitorId, StringComparison.OrdinalIgnoreCase))
                {
                    return (monitors[i], warning, null);
                }
            }

            // Carry the "-n ignored" warning even on not-found: spec says the note still applies.
            return (null, warning, new CliError
            {
                Code = CliErrorCodes.MonitorNotFound,
                Message = string.Format(CultureInfo.InvariantCulture, "no monitor found with id '{0}'", monitorId),
                Hint = "run 'powerdisplay list' to see available monitors",
            });
        }

        var number = monitorNumber!.GetValueOrDefault();
        for (int i = 0; i < monitors.Count; i++)
        {
            if (monitors[i].MonitorNumber == number)
            {
                return (monitors[i], null, null);
            }
        }

        return (null, null, new CliError
        {
            Code = CliErrorCodes.MonitorNotFound,
            Message = string.Format(CultureInfo.InvariantCulture, "no monitor found with number {0}", number),
            Hint = "run 'powerdisplay list' to see available monitors",
        });
    }

    // ─── Private helpers ───────────────────────────────────────────────────────

    /// <summary>Mirrors <c>SetCommand.ToRef</c>.</summary>
    private static CliMonitorRef ToRef(Monitor m) => new()
    {
        Number = m.MonitorNumber,
        Id = m.Id,
        Name = m.Name,
        Method = m.CommunicationMethod,
    };

    /// <summary>Mirrors <c>ListCommand.BuildEntry</c>.</summary>
    private static CliListMonitor BuildListEntry(Monitor m) => new()
    {
        Number = m.MonitorNumber,
        Id = m.Id,
        Name = m.Name,
        Method = m.CommunicationMethod,
        SupportsBrightness = m.SupportsBrightness,
        SupportsContrast = m.SupportsContrast,
        SupportsVolume = m.SupportsVolume,
        SupportsColorTemperature = m.SupportsColorTemperature,
        SupportsInputSource = m.SupportsInputSource,
        SupportsPowerState = m.SupportsPowerState,
        SupportsOrientation = !string.IsNullOrEmpty(m.GdiDeviceName),
    };

    /// <summary>
    /// Mirrors <c>GetCommand.BuildEntry</c>. Returns null and sets <paramref name="error"/> when the
    /// setting filter names an unknown setting.
    /// </summary>
    private static CliGetMonitorEntry? BuildGetEntry(
        Monitor monitor,
        CliMonitorRef monitorRef,
        string? settingFilter,
        out CliError? error)
    {
        if (TryGetUnknownSettingError(settingFilter, out error))
        {
            return null;
        }

        IEnumerable<string> settingNames = settingFilter is null
            ? CliSettingNames.All
            : new[] { settingFilter.ToLowerInvariant() };

        var results = new List<CliSettingValue>();
        foreach (var name in settingNames)
        {
            results.Add(BuildSettingValue(monitor, name)!);
        }

        return new CliGetMonitorEntry
        {
            Monitor = monitorRef,
            Settings = results,
        };
    }

    /// <summary>
    /// Validates the optional <c>--setting</c> filter against <see cref="CliSettingNames.All"/>.
    /// Returns <c>true</c> with a populated error when the filter names an unknown setting.
    /// The error echoes the user's original input verbatim, not the lower-cased lookup key.
    /// Mirrors <c>GetCommand.TryGetUnknownSettingError</c>.
    /// </summary>
    private static bool TryGetUnknownSettingError(string? settingFilter, out CliError? error)
    {
        error = null;
        if (settingFilter is null || Array.IndexOf(CliSettingNames.All, settingFilter.ToLowerInvariant()) >= 0)
        {
            return false;
        }

        error = new CliError
        {
            Code = CliErrorCodes.ArgumentError,
            Setting = settingFilter,
            Message = string.Format(CultureInfo.InvariantCulture, "unknown setting '{0}'", settingFilter),
            Hint = string.Format(
                CultureInfo.InvariantCulture,
                "valid settings: {0}",
                string.Join(", ", CliSettingNames.All)),
        };
        return true;
    }

    /// <summary>
    /// Projects one setting value. Mirrors <c>GetCommand.BuildSettingValue</c>.
    /// The value is reported only when the monitor both supports it and discovery
    /// actually read it (<see cref="Monitor.ReadValues"/>) — a default/stale field is
    /// never passed off as a live reading.
    /// </summary>
    private static CliSettingValue? BuildSettingValue(Monitor monitor, string settingName) => settingName switch
    {
        "brightness" => Reading("brightness", monitor.SupportsBrightness, monitor.ReadValues.HasFlag(MonitorReadFlags.Brightness), monitor.CurrentBrightness, v => v + "%"),
        "contrast" => Reading("contrast", monitor.SupportsContrast, monitor.ReadValues.HasFlag(MonitorReadFlags.Contrast), monitor.CurrentContrast, v => v + "%"),
        "volume" => Reading("volume", monitor.SupportsVolume, monitor.ReadValues.HasFlag(MonitorReadFlags.Volume), monitor.CurrentVolume, v => v + "%"),
        "color-temperature" => Reading("color-temperature", monitor.SupportsColorTemperature, monitor.ReadValues.HasFlag(MonitorReadFlags.ColorTemperature), monitor.CurrentColorTemperature, v => FormatDiscrete(0x14, v)),
        "input-source" => Reading("input-source", monitor.SupportsInputSource, monitor.ReadValues.HasFlag(MonitorReadFlags.InputSource), monitor.CurrentInputSource, v => FormatDiscrete(0x60, v)),
        "power-state" => Reading("power-state", monitor.SupportsPowerState, monitor.ReadValues.HasFlag(MonitorReadFlags.PowerState), monitor.CurrentPowerState, v => FormatDiscrete(0xD6, v)),

        // raw is the orientation in degrees; the display string is derived from the index.
        // The formatter ignores its int argument and calls OrientationDegrees(index) directly,
        // matching SetCommand.BuildSettingValue's "formatter ignores its argument" comment.
        "orientation" => Reading("orientation", !string.IsNullOrEmpty(monitor.GdiDeviceName), monitor.ReadValues.HasFlag(MonitorReadFlags.Orientation), OrientationDegreesValue(monitor.Orientation), _ => OrientationDegrees(monitor.Orientation)),
        _ => null,
    };

    /// <summary>
    /// Projects one setting, gating the value on supported &amp;&amp; read.
    /// Mirrors <c>GetCommand.Reading</c>.
    /// </summary>
    private static CliSettingValue Reading(string name, bool supported, bool read, int raw, Func<int, string> format)
    {
        var known = supported && read;
        return new CliSettingValue
        {
            Setting = name,
            Supported = supported,
            Raw = known ? raw : null,
            Display = known ? format(raw) : null,
        };
    }

    /// <summary>
    /// Formats a discrete VCP value as "Name (0xNN)" or "0xNN" when the name is unknown.
    /// Mirrors <c>SetCommand.FormatDiscrete</c>.
    /// </summary>
    internal static string FormatDiscrete(byte vcpCode, int value)
    {
        var name = VcpNames.GetValueName(vcpCode, value);
        return name is null
            ? $"0x{value:X2}"
            : $"{name} (0x{value:X2})";
    }

    /// <summary>
    /// Returns the human-readable orientation string for a GDI orientation index (0–3).
    /// Mirrors <c>SetCommand.OrientationDegrees</c>.
    /// </summary>
    internal static string OrientationDegrees(int index) => index switch
    {
        0 => "0°",
        1 => "90°",
        2 => "180°",
        3 => "270°",
        _ => $"index {index}",
    };

    /// <summary>
    /// Returns the degree value for a GDI orientation index (0–3).
    /// Mirrors <c>SetCommand.OrientationDegreesValue</c>.
    /// </summary>
    internal static int OrientationDegreesValue(int index) => index switch
    {
        0 => 0,
        1 => 90,
        2 => 180,
        3 => 270,
        _ => index,
    };
}
