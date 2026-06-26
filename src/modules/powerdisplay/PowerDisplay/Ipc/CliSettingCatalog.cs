// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using PowerDisplay.Common.Drivers;
using PowerDisplay.Common.Models;
using PowerDisplay.Contracts;

namespace PowerDisplay.Ipc;

/// <summary>
/// The single source of per-setting VCP metadata for the CLI IPC layer. See <see cref="CliVcpSetting"/>.
/// Orientation is intentionally absent (it is GDI-based, not a VCP setting).
/// </summary>
internal static class CliSettingCatalog
{
    /// <summary>The six VCP settings, in canonical (display) order.</summary>
    public static readonly IReadOnlyList<CliVcpSetting> VcpSettings = new CliVcpSetting[]
    {
        new(CliSettingNames.Brightness,       CliSettingKind.Continuous, NativeConstants.VcpCodeBrightness,         MonitorReadFlags.Brightness,       m => m.SupportsBrightness,       m => m.CurrentBrightness),
        new(CliSettingNames.Contrast,         CliSettingKind.Continuous, NativeConstants.VcpCodeContrast,           MonitorReadFlags.Contrast,         m => m.SupportsContrast,         m => m.CurrentContrast),
        new(CliSettingNames.Volume,           CliSettingKind.Continuous, NativeConstants.VcpCodeVolume,             MonitorReadFlags.Volume,           m => m.SupportsVolume,           m => m.CurrentVolume),
        new(CliSettingNames.ColorTemperature, CliSettingKind.Discrete,   NativeConstants.VcpCodeSelectColorPreset,  MonitorReadFlags.ColorTemperature, m => m.SupportsColorTemperature, m => m.CurrentColorTemperature),
        new(CliSettingNames.InputSource,      CliSettingKind.Discrete,   NativeConstants.VcpCodeInputSource,        MonitorReadFlags.InputSource,      m => m.SupportsInputSource,      m => m.CurrentInputSource),
        new(CliSettingNames.PowerState,       CliSettingKind.Discrete,   NativeConstants.VcpCodePowerMode,          MonitorReadFlags.PowerState,       m => m.SupportsPowerState,       m => m.CurrentPowerState),
    };

    private static readonly IReadOnlyDictionary<string, CliVcpSetting> ByNameMap =
        VcpSettings.ToDictionary(s => s.Name, StringComparer.Ordinal);

    /// <summary>
    /// Returns the descriptor for a canonical (lower-case) setting name, or <see langword="null"/>
    /// when the name is not one of the six VCP settings (e.g. <c>orientation</c> or an unknown name).
    /// </summary>
    public static CliVcpSetting? TryGet(string settingName)
        => ByNameMap.TryGetValue(settingName, out var setting) ? setting : null;
}
