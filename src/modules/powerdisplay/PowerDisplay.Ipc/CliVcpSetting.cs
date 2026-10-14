// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;
using PowerDisplay.Contracts;
using PowerDisplay.Models;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Ipc;

/// <summary>
/// Polymorphic base for the per-setting metadata + behavior of one VCP setting the CLI read/write
/// commands operate on. The shared <em>data</em> (name, VCP code, read flag, capability/value/apply
/// delegates) lives here so <see cref="CliSettingCatalog"/> can still declare one row per setting;
/// the <em>behavior</em> that used to fan out into <c>Kind</c> switches in <see cref="SetCommandExecutor"/>
/// and <see cref="MonitorDtoProjector"/> — value parsing/validation, display formatting, and the
/// <c>set</c> pipeline — is dispatched here via <see cref="ContinuousVcpSetting"/> and
/// <see cref="DiscreteVcpSetting"/>.
/// <para>
/// <b>Orientation is intentionally excluded:</b> it is GDI-based (not a VCP code), needs a
/// <c>GdiDeviceName</c>, and maps degrees↔index, so it stays a special case at the call sites.
/// </para>
/// </summary>
internal abstract class CliVcpSetting
{
    private readonly Func<Monitor, bool> _supports;
    private readonly Func<Monitor, int> _current;
    private readonly Func<IMonitorManager, string, int, CancellationToken, Task<MonitorOperationResult>> _apply;

    /// <param name="name">Canonical (lower-case) setting name; see <c>CliSettingNames</c>.</param>
    /// <param name="vcpCode">The VESA MCCS VCP code for this setting.</param>
    /// <param name="readFlag">The <see cref="MonitorReadFlags"/> bit set when discovery read this setting.</param>
    /// <param name="supports">Selects the monitor's hardware-capability flag for this setting.</param>
    /// <param name="current">Selects the monitor's last-read value for this setting.</param>
    /// <param name="apply">The hardware-write delegate for this setting on <see cref="IMonitorManager"/>.</param>
    /// <param name="unsupportedReason">
    /// Invariant English explanation surfaced when the monitor does not support this setting.
    /// </param>
    /// <param name="blanksDisplay">
    /// True only for settings whose values can blank the panel (power-state); gates the
    /// <c>--confirm-power-off</c> requirement.
    /// </param>
    protected CliVcpSetting(
        string name,
        byte vcpCode,
        MonitorReadFlags readFlag,
        Func<Monitor, bool> supports,
        Func<Monitor, int> current,
        Func<IMonitorManager, string, int, CancellationToken, Task<MonitorOperationResult>> apply,
        string unsupportedReason,
        bool blanksDisplay = false)
    {
        Name = name;
        VcpCode = vcpCode;
        ReadFlag = readFlag;
        _supports = supports;
        _current = current;
        _apply = apply;
        UnsupportedReason = unsupportedReason;
        BlanksDisplay = blanksDisplay;
    }

    /// <summary>Canonical (lower-case) setting name; see <c>CliSettingNames</c>.</summary>
    public string Name { get; }

    /// <summary>The VESA MCCS VCP code for this setting.</summary>
    public byte VcpCode { get; }

    /// <summary>The <see cref="MonitorReadFlags"/> bit set when discovery read this setting.</summary>
    public MonitorReadFlags ReadFlag { get; }

    /// <summary>Invariant English explanation surfaced when the monitor does not support this setting.</summary>
    public string UnsupportedReason { get; }

    /// <summary>True only for settings whose values can blank the panel (power-state).</summary>
    public bool BlanksDisplay { get; }

    /// <summary>Continuous percentage vs. discrete VCP value.</summary>
    public abstract CliSettingKind Kind { get; }

    /// <summary>Whether the monitor advertises hardware support for this setting.</summary>
    public bool Supports(Monitor monitor) => _supports(monitor);

    /// <summary>The monitor's last-read value for this setting.</summary>
    public int Current(Monitor monitor) => _current(monitor);

    /// <summary>Performs the DDC/CI or WMI hardware write for this setting.</summary>
    public Task<MonitorOperationResult> Apply(IMonitorManager manager, string monitorId, int value, CancellationToken ct)
        => _apply(manager, monitorId, value, ct);

    /// <summary>
    /// The monitor's advertised discrete value set (used to validate a <c>set</c> value).
    /// <see langword="null"/> for continuous settings, which have no discrete set.
    /// </summary>
    public virtual IReadOnlyList<int>? SupportedValues(Monitor monitor) => null;

    /// <summary>
    /// Parses and validates the raw <c>set</c> value against this setting's rules and the target
    /// <paramref name="monitor"/>. Returns the resolved value with a <see langword="null"/> error, or
    /// a <see langword="null"/> value with the <see cref="CliError"/> to surface (the caller wraps it
    /// in a <see cref="CliErrorResult"/> with the monitor ref).
    /// </summary>
    public abstract (int? Value, CliError? Error) ParseSetValue(string rawValue, Monitor monitor);

    /// <summary>Formats a value as the human-readable before/after display string for this setting.</summary>
    public abstract string FormatDisplay(int value, IReadOnlyList<CustomVcpValueMapping>? customMappings = null, string monitorId = "");

    /// <summary>
    /// Shared <c>set</c> pipeline (template method): capability check → value parse/validate →
    /// panel-blanking gate → hardware write → cancellation/failure handling → before/after result.
    /// The per-kind steps (<see cref="ParseSetValue"/>, <see cref="FormatDisplay"/>, and the
    /// <see cref="BlanksDisplay"/> data) are the only points that vary between continuous and discrete
    /// settings, so the ordering and error contract live here once.
    /// </summary>
    public async Task<(CliSetResult? Result, CliErrorResult? Error)> ApplySetAsync(
        IMonitorManager manager,
        Monitor monitor,
        CliMonitorRef monitorRef,
        SetRequest req,
        CancellationToken ct,
        IReadOnlyList<CustomVcpValueMapping>? customMappings = null)
    {
        if (!Supports(monitor))
        {
            return (null, CliErrorFactory.Unsupported(CliCommandNames.Set, monitorRef, Name, UnsupportedReason));
        }

        var (value, parseError) = ParseSetValue(req.RawValue, monitor);
        if (parseError is not null)
        {
            return (null, new CliErrorResult { Command = CliCommandNames.Set, Monitor = monitorRef, Error = parseError });
        }

        // Gate any state that blanks the panel on the already-resolved value. Only power-state sets
        // BlanksDisplay, so continuous settings skip this unconditionally.
        if (BlanksDisplay && !req.ConfirmPowerOff && IsDisplayBlanking(value!.Value))
        {
            return (null, new CliErrorResult
            {
                Command = CliCommandNames.Set,
                Monitor = monitorRef,
                Error = new CliError
                {
                    Code = CliErrorCodes.ArgumentError,
                    MessageId = CliMessageIds.PowerBlankingConfirm,
                    Setting = Name,
                },
            });
        }

        var beforeKnown = monitor.ReadValues.HasFlag(ReadFlag);
        var beforeValue = Current(monitor);

        var op = await Apply(manager, monitor.Id, value!.Value, ct);

        // The server receives its own app-lifetime token; a client Ctrl+C/deadline only closes the
        // pipe and is not propagated here. If server shutdown is observed before or after the
        // non-interruptible hardware call, surface the cancellation as TIMEOUT when a response can
        // still be returned.
        ct.ThrowIfCancellationRequested();

        if (!op.IsSuccess)
        {
            return (null, CliErrorFactory.HardwareFailure(CliCommandNames.Set, monitorRef, op.ErrorMessage));
        }

        return (new CliSetResult
        {
            Monitor = monitorRef,
            Setting = Name,
            BeforeDisplay = beforeKnown ? FormatDisplay(beforeValue, customMappings, monitor.Id) : null,
            AfterDisplay = FormatDisplay(value.Value, customMappings, monitor.Id),
        }, null);
    }

    /// <summary>
    /// VCP 0xD6 states that leave a headless caller staring at a dark panel.
    /// </summary>
    protected static bool IsDisplayBlanking(int powerState) => powerState is 0x02 or 0x03 or 0x04 or 0x05;
}
