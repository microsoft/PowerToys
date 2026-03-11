// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Microsoft.UI;
using Windows.UI;

namespace Microsoft.CmdPal.UI.ViewModels.Settings;

#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
/// Settings for the Dock. These are settings for _the whole dock_. Band-specific
/// settings are in <see cref="DockBandSettings"/>.
/// </summary>
public record DockSettings
{
    /// <summary>
    /// Gets the default dock side. Used as fallback for monitors that
    /// don't have a per-monitor config entry.
    /// </summary>
    public DockSide Side { get; init; } = DockSide.Top;

    public DockSize DockSize { get; init; } = DockSize.Small;

    public DockSize DockIconsSize { get; init; } = DockSize.Small;

    // <Theme settings>
    public DockBackdrop Backdrop { get; init; } = DockBackdrop.Acrylic;

    public UserTheme Theme { get; init; } = UserTheme.Default;

    public ColorizationMode ColorizationMode { get; init; }

    public Color CustomThemeColor { get; init; } = Colors.Transparent;

    public int CustomThemeColorIntensity { get; init; } = 100;

    public int BackgroundImageOpacity { get; init; } = 20;

    public int BackgroundImageBlurAmount { get; init; }

    public int BackgroundImageBrightness { get; init; }

    public BackgroundImageFit BackgroundImageFit { get; init; }

    public string? BackgroundImagePath { get; init; }

    // </Theme settings>
    // public List<string> PinnedCommands { get; set; } = [];
    public List<DockBandSettings> StartBands { get; init; } = [];

    public List<DockBandSettings> CenterBands { get; init; } = [];

    public List<DockBandSettings> EndBands { get; init; } = [];

    public bool ShowLabels { get; init; } = true;

    /// <summary>
    /// Gets or sets per-monitor dock configurations. Each entry enables the dock
    /// on a specific monitor and optionally overrides the dock side.
    /// When empty, the dock displays only on the primary monitor using <see cref="Side"/>.
    /// </summary>
    public List<DockMonitorConfig> MonitorConfigs { get; set; } = [];

    [JsonIgnore]
    public IEnumerable<(string ProviderId, string CommandId)> AllPinnedCommands =>
        StartBands.Select(b => (b.ProviderId, b.CommandId))
        .Concat(CenterBands.Select(b => (b.ProviderId, b.CommandId)))
        .Concat(EndBands.Select(b => (b.ProviderId, b.CommandId)));

    public DockSettings()
    {
        // Initialize with default values
        StartBands.Add(new DockBandSettings
        {
            ProviderId = "com.microsoft.cmdpal.builtin.core",
            CommandId = "com.microsoft.cmdpal.home",
        });
        StartBands.Add(new DockBandSettings
        {
            ProviderId = "WinGet",
            CommandId = "com.microsoft.cmdpal.winget",
            ShowTitles = false,
            ShowSubtitles = false,
        });

        EndBands.Add(new DockBandSettings
        {
            ProviderId = "PerformanceMonitor",
            CommandId = "com.microsoft.cmdpal.performanceWidget",
        });
        EndBands.Add(new DockBandSettings
        {
            ProviderId = "com.microsoft.cmdpal.builtin.datetime",
            CommandId = "com.microsoft.cmdpal.timedate.dockBand",
        });
    }
}

/// <summary>
/// Settings for a specific dock band. These are per-band settings stored
/// within the overall <see cref="DockSettings"/>.
/// </summary>
public record DockBandSettings
{
    public required string ProviderId { get; init; }

    public required string CommandId { get; init; }

    /// <summary>
    /// Gets whether titles are shown for items in this band.
    /// If null, falls back to dock-wide ShowLabels setting.
    /// </summary>
    public bool? ShowTitles { get; init; } = true;

    /// <summary>
    /// Gets whether subtitles are shown for items in this band.
    /// If null, falls back to dock-wide ShowLabels setting.
    /// </summary>
    public bool? ShowSubtitles { get; init; } = true;

    /// <summary>
    /// Resolves the effective value of <see cref="ShowTitles"/> for this band.
    /// If this band doesn't have a specific value set, we'll fall back to the
    /// dock-wide setting (passed as <paramref name="defaultValue"/>).
    /// </summary>
    public bool ResolveShowTitles(bool defaultValue) => ShowTitles ?? defaultValue;

    /// <summary>
    /// Resolves the effective value of <see cref="ShowSubtitles"/> for this band.
    /// If this band doesn't have a specific value set, we'll fall back to the
    /// dock-wide setting (passed as <paramref name="defaultValue"/>).
    /// </summary>
    public bool ResolveShowSubtitles(bool defaultValue) => ShowSubtitles ?? defaultValue;
}

/// <summary>
/// Per-monitor dock configuration. Allows the user to enable the dock on
/// specific monitors with an optional side override and per-monitor band pinning.
/// </summary>
public class DockMonitorConfig
{
    /// <summary>
    /// Gets or sets the device identifier of the target monitor (e.g. "\\.\DISPLAY1").
    /// This value may change across reboots; <see cref="IsPrimary"/> is used as a
    /// stable fallback when the device id no longer matches any connected monitor.
    /// </summary>
    public required string MonitorDeviceId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the dock is enabled on this monitor.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the dock side for this monitor. If null, falls back to
    /// the dock-wide <see cref="DockSettings.Side"/> value.
    /// </summary>
    public DockSide? Side { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this config was for the primary
    /// monitor. Used as a stable matching key when <see cref="MonitorDeviceId"/>
    /// changes across reboots.
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this monitor has custom band
    /// pinning. When false, the monitor inherits bands from the global
    /// <see cref="DockSettings"/>. When true, <see cref="StartBands"/>,
    /// <see cref="CenterBands"/>, and <see cref="EndBands"/> are used.
    /// </summary>
    public bool IsCustomized { get; set; }

    /// <summary>
    /// Gets or sets per-monitor start bands. Only used when <see cref="IsCustomized"/>
    /// is true. Null means inherit from global dock settings.
    /// </summary>
    public List<DockBandSettings>? StartBands { get; set; }

    /// <summary>
    /// Gets or sets per-monitor center bands. Only used when <see cref="IsCustomized"/>
    /// is true. Null means inherit from global dock settings.
    /// </summary>
    public List<DockBandSettings>? CenterBands { get; set; }

    /// <summary>
    /// Gets or sets per-monitor end bands. Only used when <see cref="IsCustomized"/>
    /// is true. Null means inherit from global dock settings.
    /// </summary>
    public List<DockBandSettings>? EndBands { get; set; }

    /// <summary>
    /// Resolves the effective dock side for this monitor, falling back to the
    /// dock-wide default when no per-monitor override is set.
    /// </summary>
    public DockSide ResolveSide(DockSide defaultSide) => Side ?? defaultSide;

    /// <summary>
    /// Resolves the effective start bands for this monitor. Returns per-monitor
    /// bands when customized, otherwise the global bands.
    /// </summary>
    public List<DockBandSettings> ResolveStartBands(List<DockBandSettings> globalBands)
        => IsCustomized && StartBands is not null ? StartBands : globalBands;

    /// <summary>
    /// Resolves the effective center bands for this monitor.
    /// </summary>
    public List<DockBandSettings> ResolveCenterBands(List<DockBandSettings> globalBands)
        => IsCustomized && CenterBands is not null ? CenterBands : globalBands;

    /// <summary>
    /// Resolves the effective end bands for this monitor.
    /// </summary>
    public List<DockBandSettings> ResolveEndBands(List<DockBandSettings> globalBands)
        => IsCustomized && EndBands is not null ? EndBands : globalBands;

    /// <summary>
    /// Forks this monitor's band configuration from the global settings.
    /// Copies the global bands into per-monitor lists and sets
    /// <see cref="IsCustomized"/> to true.
    /// </summary>
    public void ForkFromGlobal(DockSettings globalSettings)
    {
        StartBands = new List<DockBandSettings>();
        foreach (var b in globalSettings.StartBands)
        {
            StartBands.Add(b with { });
        }

        CenterBands = new List<DockBandSettings>();
        foreach (var b in globalSettings.CenterBands)
        {
            CenterBands.Add(b with { });
        }

        EndBands = new List<DockBandSettings>();
        foreach (var b in globalSettings.EndBands)
        {
            EndBands.Add(b with { });
        }

        IsCustomized = true;
    }
}

public enum DockSide
{
    Left = 0,
    Top = 1,
    Right = 2,
    Bottom = 3,
}

public enum DockSize
{
    Small,
    Medium,
    Large,
}

public enum DockBackdrop
{
    Transparent,
    Acrylic,
}

#pragma warning restore SA1402 // File may only contain a single type
