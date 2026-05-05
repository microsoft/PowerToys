// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.UI;

namespace Microsoft.CmdPal.UI.ViewModels.Settings;

#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
/// Settings for the Dock. These are settings for _the whole dock_. Band-specific
/// settings are in <see cref="DockBandSettings"/>. Per-monitor overrides are
/// stored in <see cref="MonitorConfigs"/>.
/// </summary>
public record DockSettings
{
    public DockSide Side { get; init; } = DockSide.Top;

    public DockSize DockSize { get; init; } = DockSize.Default;

    public bool AlwaysOnTop { get; set; } = true;

    // <Theme settings>
    public DockBackdrop Backdrop { get; init; } = DockBackdrop.Acrylic;

    public UserTheme Theme { get; init; } = UserTheme.Default;

    public ColorizationMode ColorizationMode { get; init; }

    public Color CustomThemeColor { get; init; } = new() { A = 0, R = 255, G = 255, B = 255 }; // Transparent — avoids WinUI3 COM dependency on Colors.Transparent and COM in class init

    public int CustomThemeColorIntensity { get; init; } = 100;

    public int BackgroundImageOpacity { get; init; } = 20;

    public int BackgroundImageBlurAmount { get; init; }

    public int BackgroundImageBrightness { get; init; }

    public BackgroundImageFit BackgroundImageFit { get; init; }

    public string? BackgroundImagePath { get; init; }

    // </Theme settings>
    private ImmutableList<DockBandSettings>? _startBands = ImmutableList.Create(
        new DockBandSettings
        {
            ProviderId = "com.microsoft.cmdpal.builtin.core",
            CommandId = "com.microsoft.cmdpal.home",
        },
        new DockBandSettings
        {
            ProviderId = "WinGet",
            CommandId = "com.microsoft.cmdpal.winget",
            ShowTitles = false,
        });

    public ImmutableList<DockBandSettings> StartBands
    {
        get => _startBands ?? ImmutableList<DockBandSettings>.Empty;
        init => _startBands = value;
    }

    private ImmutableList<DockBandSettings>? _centerBands = ImmutableList<DockBandSettings>.Empty;

    public ImmutableList<DockBandSettings> CenterBands
    {
        get => _centerBands ?? ImmutableList<DockBandSettings>.Empty;
        init => _centerBands = value;
    }

    private ImmutableList<DockBandSettings>? _endBands = ImmutableList.Create(
        new DockBandSettings
        {
            ProviderId = "PerformanceMonitor",
            CommandId = "com.microsoft.cmdpal.performanceWidget",
        },
        new DockBandSettings
        {
            ProviderId = "com.microsoft.cmdpal.builtin.datetime",
            CommandId = "com.microsoft.cmdpal.timedate.dockBand",
        });

    public ImmutableList<DockBandSettings> EndBands
    {
        get => _endBands ?? ImmutableList<DockBandSettings>.Empty;
        init => _endBands = value;
    }

    public bool ShowLabels { get; init; } = true;

    /// <summary>
    /// Gets the per-monitor dock configurations. Each entry overrides global
    /// settings for a specific display. Empty by default (all monitors use global).
    /// </summary>
    private ImmutableList<DockMonitorConfig>? _monitorConfigs = ImmutableList<DockMonitorConfig>.Empty;

    public ImmutableList<DockMonitorConfig> MonitorConfigs
    {
        get => _monitorConfigs ?? ImmutableList<DockMonitorConfig>.Empty;
        init => _monitorConfigs = value ?? ImmutableList<DockMonitorConfig>.Empty;
    }

    [JsonIgnore]
    public IEnumerable<(string ProviderId, string CommandId)> AllPinnedCommands
    {
        get
        {
            // Start with global bands
            var result = StartBands.Select(b => (b.ProviderId, b.CommandId))
                .Concat(CenterBands.Select(b => (b.ProviderId, b.CommandId)))
                .Concat(EndBands.Select(b => (b.ProviderId, b.CommandId)));

            // Include per-monitor bands so that commands pinned to specific
            // monitors are loaded as TopLevelViewModels and appear in the dock.
            var configs = MonitorConfigs ?? ImmutableList<DockMonitorConfig>.Empty;
            foreach (var config in configs)
            {
                if (config.IsCustomized)
                {
                    var start = config.StartBands ?? ImmutableList<DockBandSettings>.Empty;
                    var center = config.CenterBands ?? ImmutableList<DockBandSettings>.Empty;
                    var end = config.EndBands ?? ImmutableList<DockBandSettings>.Empty;
                    result = result
                        .Concat(start.Select(b => (b.ProviderId, b.CommandId)))
                        .Concat(center.Select(b => (b.ProviderId, b.CommandId)))
                        .Concat(end.Select(b => (b.ProviderId, b.CommandId)));
                }
            }

            return result;
        }
    }
}

/// <summary>
/// Per-monitor configuration for the dock. Each monitor can override the global
/// dock side, enable/disable its dock, and optionally maintain independent band lists.
/// Uses a nullable-override pattern: <c>null</c> values inherit from global <see cref="DockSettings"/>.
/// </summary>
public sealed record DockMonitorConfig
{
    /// <summary>
    /// Gets the monitor device identifier (e.g. <c>\\.\DISPLAY1</c>).
    /// </summary>
    public required string MonitorDeviceId { get; init; }

    /// <summary>
    /// Gets a value indicating whether the dock is enabled on this monitor. Defaults to <c>true</c>.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets the dock side override for this monitor. When <c>null</c>, inherits the global
    /// <see cref="DockSettings.Side"/> value.
    /// </summary>
    public DockSide? Side { get; init; }

    /// <summary>
    /// Gets a value indicating whether this monitor is the primary display.
    /// Used as a stable key for reconciliation when device IDs change across reboots.
    /// </summary>
    public bool IsPrimary { get; init; }

    /// <summary>
    /// Gets a value indicating whether this monitor has its own independent band lists.
    /// When <c>false</c>, the monitor inherits bands from the global <see cref="DockSettings"/>.
    /// </summary>
    public bool IsCustomized { get; init; }

    /// <summary>
    /// Gets the per-monitor start bands. Only used when <see cref="IsCustomized"/> is <c>true</c>.
    /// </summary>
    public ImmutableList<DockBandSettings>? StartBands { get; init; }

    /// <summary>
    /// Gets the per-monitor center bands. Only used when <see cref="IsCustomized"/> is <c>true</c>.
    /// </summary>
    public ImmutableList<DockBandSettings>? CenterBands { get; init; }

    /// <summary>
    /// Gets the per-monitor end bands. Only used when <see cref="IsCustomized"/> is <c>true</c>.
    /// </summary>
    public ImmutableList<DockBandSettings>? EndBands { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when this monitor was last seen connected. Used for
    /// staleness pruning: configs not seen for 6+ months are automatically removed
    /// during reconciliation.
    /// </summary>
    public DateTime? LastSeen { get; init; }

    /// <summary>
    /// Resolves the effective dock side for this monitor.
    /// </summary>
    public DockSide ResolveSide(DockSide defaultSide) => Side ?? defaultSide;

    /// <summary>
    /// Resolves the effective start bands for this monitor.
    /// Returns per-monitor bands when customized; otherwise falls back to the global bands.
    /// </summary>
    public ImmutableList<DockBandSettings> ResolveStartBands(ImmutableList<DockBandSettings> globalBands) =>
        IsCustomized && StartBands is not null ? StartBands : globalBands;

    /// <summary>
    /// Resolves the effective center bands for this monitor.
    /// Returns per-monitor bands when customized; otherwise falls back to the global bands.
    /// </summary>
    public ImmutableList<DockBandSettings> ResolveCenterBands(ImmutableList<DockBandSettings> globalBands) =>
        IsCustomized && CenterBands is not null ? CenterBands : globalBands;

    /// <summary>
    /// Resolves the effective end bands for this monitor.
    /// Returns per-monitor bands when customized; otherwise falls back to the global bands.
    /// </summary>
    public ImmutableList<DockBandSettings> ResolveEndBands(ImmutableList<DockBandSettings> globalBands) =>
        IsCustomized && EndBands is not null ? EndBands : globalBands;

    /// <summary>
    /// Creates a new <see cref="DockMonitorConfig"/> that is a customized fork of the
    /// given global dock settings. Copies global bands into per-monitor band lists so
    /// they can be independently modified.
    /// </summary>
    public DockMonitorConfig ForkFromGlobal(DockSettings globalSettings) => this with
    {
        IsCustomized = true,

        // Create independent copies by rebuilding the immutable lists
        StartBands = ImmutableList.CreateRange(globalSettings.StartBands ?? ImmutableList<DockBandSettings>.Empty),
        CenterBands = ImmutableList.CreateRange(globalSettings.CenterBands ?? ImmutableList<DockBandSettings>.Empty),
        EndBands = ImmutableList.CreateRange(globalSettings.EndBands ?? ImmutableList<DockBandSettings>.Empty),
    };
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
    public bool? ShowTitles { get; init; }

    /// <summary>
    /// Gets whether subtitles are shown for items in this band.
    /// If null, falls back to dock-wide ShowLabels setting.
    /// </summary>
    public bool? ShowSubtitles { get; init; }

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

public enum DockSide
{
    Left = 0,
    Top = 1,
    Right = 2,
    Bottom = 3,
}

[JsonConverter(typeof(DockSizeJsonConverter))]
public enum DockSize
{
    Default,
    Compact,
}

/// <summary>
/// Custom converter for <see cref="DockSize"/> that preserves backward
/// compatibility with previously-persisted values. Earlier builds shipped a
/// <c>Small</c>/<c>Medium</c>/<c>Large</c> enum; those values are migrated to
/// <see cref="DockSize.Default"/> so existing settings.json files continue to
/// load instead of failing the entire deserialization.
/// </summary>
internal sealed class DockSizeJsonConverter : JsonConverter<DockSize>
{
    public override DockSize Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (Enum.TryParse<DockSize>(value, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            // Legacy values from the original Small/Medium/Large enum, or any
            // other unknown string — fall back to Default so the user's
            // settings file remains loadable after upgrading.
            return DockSize.Default;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var number))
        {
            return Enum.IsDefined(typeof(DockSize), number) ? (DockSize)number : DockSize.Default;
        }

        return DockSize.Default;
    }

    public override void Write(Utf8JsonWriter writer, DockSize value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public enum DockBackdrop
{
    Transparent,
    Acrylic,
}

#pragma warning restore SA1402 // File may only contain a single type
