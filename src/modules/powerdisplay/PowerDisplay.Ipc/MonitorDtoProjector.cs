// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;
using PowerDisplay.Contracts;
using PowerDisplay.Models;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Ipc;

/// <summary>
/// Pure-function projector that turns the app's rich <see cref="Monitor"/> model into the flat
/// Contracts result DTOs consumed by the CLI renderers. All three read-side commands (list, get,
/// capabilities) are covered.
/// <para>
/// This projector is the single source of these DTOs: it defines the display strings, error
/// codes/exit codes, and hidden-monitor and selector semantics that the CLI renderers consume.
/// </para>
/// </summary>
public static class MonitorDtoProjector
{
    // ─── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the result DTO for the <c>list</c> command.
    /// Hidden monitors are excluded; each surviving monitor becomes one list entry.
    /// </summary>
    public static CliListResult BuildListResult(
        IReadOnlyList<Monitor> monitors,
        IReadOnlySet<string> hiddenIds)
    {
        var visible = ExcludeHidden(monitors, hiddenIds);
        var entries = new List<CliMonitorRef>(visible.Count);
        foreach (var m in visible)
        {
            entries.Add(ToRef(m));
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
        string? settingFilter,
        IReadOnlyList<CustomVcpValueMapping>? customMappings = null)
    {
        var visible = ExcludeHidden(monitors, hiddenIds);

        if (!number.HasValue && string.IsNullOrEmpty(id))
        {
            if (TryGetUnknownSettingError(settingFilter, out _, out var settingErr))
            {
                return (null, new CliErrorResult { Command = CliCommandNames.Get, Error = settingErr! });
            }

            var allEntries = new List<CliGetMonitorEntry>(visible.Count);
            foreach (var monitor in visible)
            {
                var monRef = ToRef(monitor);
                allEntries.Add(BuildGetEntry(monitor, monRef, settingFilter, customMappings, out _)!);
            }

            return (new CliGetResult { Monitors = allEntries }, null);
        }

        var (selected, resolveError) = ResolveMonitor(visible, number, id);
        if (resolveError is not null)
        {
            return (null, new CliErrorResult { Command = CliCommandNames.Get, Error = resolveError });
        }

        var mRef = ToRef(selected!);
        var entry = BuildGetEntry(selected!, mRef, settingFilter, customMappings, out var settingError);
        if (settingError is not null)
        {
            return (null, new CliErrorResult { Command = CliCommandNames.Get, Monitor = mRef, Error = settingError });
        }

        return (new CliGetResult { Monitors = [entry!] }, null);
    }

    /// <summary>
    /// Builds the result DTO for the <c>capabilities</c> command.
    /// A selector is required; if missing or not found an error DTO is returned.
    /// </summary>
    public static (CliCapabilitiesResult? Result, CliErrorResult? Error) BuildCapabilitiesResult(
        IReadOnlyList<Monitor> monitors,
        IReadOnlySet<string> hiddenIds,
        int? number,
        string? id,
        string? settingFilter = null,
        IReadOnlyList<CustomVcpValueMapping>? customMappings = null)
    {
        var visible = ExcludeHidden(monitors, hiddenIds);

        var (selected, resolveError) = ResolveMonitor(visible, number, id);
        if (resolveError is not null)
        {
            return (null, new CliErrorResult { Command = CliCommandNames.Capabilities, Error = resolveError });
        }

        // Optional --setting filter: restrict the result to a single discrete setting's VCP code.
        byte? filterCode = null;
        if (settingFilter is not null)
        {
            filterCode = VcpCodeForDiscreteSetting(settingFilter);
            if (filterCode is null)
            {
                return (null, new CliErrorResult
                {
                    Command = CliCommandNames.Capabilities,
                    Error = new CliError
                    {
                        Code = CliErrorCodes.ArgumentError,
                        MessageId = CliMessageIds.NotDiscreteSetting,
                        Value = settingFilter,
                    },
                });
            }
        }

        var caps = selected!.VcpCapabilitiesInfo;
        var vcpCodes = new List<CliVcpCodeInfo>();

        if (caps is not null)
        {
            foreach (var code in caps.GetSortedVcpCodes())
            {
                if (filterCode is not null && code.Code != filterCode.Value)
                {
                    continue;
                }

                List<string>? discreteValues = null;
                if (code.HasDiscreteValues)
                {
                    discreteValues = new List<string>(code.SupportedValues.Count);
                    foreach (var v in code.SupportedValues)
                    {
                        discreteValues.Add(FormatDiscrete(code.Code, v, customMappings, selected.Id));
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
            // value twice in the capabilities envelope.
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
    /// Drops monitors the user hid in PowerDisplay settings.
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
    /// <list type="bullet">
    ///   <item>No selector → <c>SelectorMissing</c> error.</item>
    ///   <item>Both selectors → id wins (the CLI surfaces the "-n ignored" note locally).</item>
    ///   <item>Not found → <c>MonitorNotFound</c> error.</item>
    /// </list>
    /// </summary>
    internal static (Monitor? Monitor, CliError? Error) ResolveMonitor(
        IReadOnlyList<Monitor> monitors,
        int? monitorNumber,
        string? monitorId)
    {
        var hasNumber = monitorNumber.HasValue;
        var hasId = !string.IsNullOrEmpty(monitorId);

        if (!hasNumber && !hasId)
        {
            return (null, new CliError
            {
                Code = CliErrorCodes.SelectorMissing,
                MessageId = CliMessageIds.SelectorMissing,
            });
        }

        if (hasId)
        {
            for (int i = 0; i < monitors.Count; i++)
            {
                if (string.Equals(monitors[i].Id, monitorId, StringComparison.OrdinalIgnoreCase))
                {
                    return (monitors[i], null);
                }
            }

            return (null, new CliError
            {
                Code = CliErrorCodes.MonitorNotFound,
                MessageId = CliMessageIds.MonitorNotFoundId,
                Value = monitorId,
            });
        }

        var number = monitorNumber!.GetValueOrDefault();
        for (int i = 0; i < monitors.Count; i++)
        {
            if (monitors[i].MonitorNumber == number)
            {
                return (monitors[i], null);
            }
        }

        return (null, new CliError
        {
            Code = CliErrorCodes.MonitorNotFound,
            MessageId = CliMessageIds.MonitorNotFoundNumber,
            Value = number.ToString(CultureInfo.InvariantCulture),
        });
    }

    // ─── Private helpers ───────────────────────────────────────────────────────

    /// <summary>Builds the compact monitor reference embedded in every response. Shared with <see cref="SetCommandExecutor"/>.</summary>
    internal static CliMonitorRef ToRef(Monitor m) => new()
    {
        Number = m.MonitorNumber,
        Id = m.Id,
        Name = m.Name,
        Method = m.CommunicationMethod,
    };

    /// <summary>
    /// Builds the per-monitor <c>get</c> entry. Returns null and sets <paramref name="error"/> when the
    /// setting filter names an unknown setting.
    /// </summary>
    private static CliGetMonitorEntry? BuildGetEntry(
        Monitor monitor,
        CliMonitorRef monitorRef,
        string? settingFilter,
        IReadOnlyList<CustomVcpValueMapping>? customMappings,
        out CliError? error)
    {
        if (TryGetUnknownSettingError(settingFilter, out var normalizedFilter, out error))
        {
            return null;
        }

        IEnumerable<string> settingNames = normalizedFilter is null
            ? CliSettingNames.All
            : new[] { normalizedFilter };

        var results = new List<CliSettingValue>();
        foreach (var name in settingNames)
        {
            results.Add(BuildSettingValue(monitor, name, customMappings)!);
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
    /// </summary>
    private static bool TryGetUnknownSettingError(string? settingFilter, out string? normalized, out CliError? error)
    {
        error = null;
        normalized = settingFilter?.ToLowerInvariant();
        if (settingFilter is null || Array.IndexOf(CliSettingNames.All, normalized) >= 0)
        {
            return false;
        }

        error = new CliError
        {
            Code = CliErrorCodes.ArgumentError,
            MessageId = CliMessageIds.UnknownSetting,
            Value = settingFilter,
        };
        return true;
    }

    /// <summary>
    /// Projects one setting value.
    /// The value is reported only when the monitor both supports it and discovery
    /// actually read it (<see cref="Monitor.ReadValues"/>) — a default/stale field is
    /// never passed off as a live reading.
    /// </summary>
    private static CliSettingValue? BuildSettingValue(Monitor monitor, string settingName, IReadOnlyList<CustomVcpValueMapping>? customMappings)
    {
        // Orientation is GDI-based (not a VCP setting), so it is not in the catalog. The raw value is
        // the orientation index; Reading formats it via OrientationDegrees only when it was actually
        // read.
        if (settingName == CliSettingNames.Orientation)
        {
            return Reading(
                CliSettingNames.Orientation,
                !string.IsNullOrEmpty(monitor.GdiDeviceName),
                monitor.ReadValues.HasFlag(MonitorReadFlags.Orientation),
                monitor.Orientation,
                OrientationDegrees);
        }

        var setting = CliSettingCatalog.TryGet(settingName);
        if (setting is null)
        {
            return null;
        }

        return Reading(
            setting.Name,
            setting.Supports(monitor),
            monitor.ReadValues.HasFlag(setting.ReadFlag),
            setting.Current(monitor),
            v => setting.FormatDisplay(v, customMappings, monitor.Id));
    }

    /// <summary>
    /// Projects one setting, gating the value on supported &amp;&amp; read.
    /// </summary>
    private static CliSettingValue Reading(string name, bool supported, bool read, int raw, Func<int, string> format)
    {
        var known = supported && read;
        return new CliSettingValue
        {
            Setting = name,
            Supported = supported,
            Display = known ? format(raw) : null,
        };
    }

    /// <summary>
    /// Formats a discrete VCP value as "Name (0xNN)" or "0xNN" when the name is unknown.
    /// </summary>
    internal static string FormatDiscrete(byte vcpCode, int value, IReadOnlyList<CustomVcpValueMapping>? customMappings = null, string monitorId = "")
    {
        var name = VcpNames.GetValueName(vcpCode, value, customMappings, monitorId);
        return name is null
            ? $"0x{value:X2}"
            : $"{name} (0x{value:X2})";
    }

    /// <summary>
    /// Maps a CLI discrete setting name to its VCP code, or null when the name is not one of the
    /// three discrete VCP settings (color-temperature 0x14, input-source 0x60, power-state 0xD6).
    /// </summary>
    internal static byte? VcpCodeForDiscreteSetting(string setting)
    {
        var descriptor = CliSettingCatalog.TryGet(setting.ToLowerInvariant());
        return descriptor is { Kind: CliSettingKind.Discrete } ? descriptor.VcpCode : null;
    }

    /// <summary>
    /// Returns the human-readable orientation string for a GDI orientation index (0–3).
    /// </summary>
    internal static string OrientationDegrees(int index) => index switch
    {
        0 => "0°",
        1 => "90°",
        2 => "180°",
        3 => "270°",
        _ => $"index {index}",
    };
}
