// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Windows.UI;

namespace Microsoft.CmdPal.UI.ViewModels.Settings;

#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
/// Settings for the Dock. These are settings for _the whole dock_. Band-specific
/// settings are in <see cref="DockBandSettings"/>.
/// </summary>
public record DockSettings
{
    public DockSide Side { get; init; } = DockSide.Top;

    public DockSize DockSize { get; init; } = DockSize.Default;

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
    public ImmutableList<DockBandSettings> StartBands { get; init; } = ImmutableList.Create(
        new DockBandSettings
        {
            ProviderId = "com.microsoft.cmdpal.builtin.core",
            CommandId = "com.microsoft.cmdpal.home",
        },
        new DockBandSettings
        {
            ProviderId = "WinGet",
            CommandId = "com.microsoft.cmdpal.winget",
            ShowLabels = false,
        });

    public ImmutableList<DockBandSettings> CenterBands { get; init; } = ImmutableList<DockBandSettings>.Empty;

    public ImmutableList<DockBandSettings> EndBands { get; init; } = ImmutableList.Create(
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

    public bool ShowLabels { get; init; } = true;

    [JsonIgnore]
    public IEnumerable<(string ProviderId, string CommandId)> AllPinnedCommands =>
        StartBands.Select(b => (b.ProviderId, b.CommandId))
        .Concat(CenterBands.Select(b => (b.ProviderId, b.CommandId)))
        .Concat(EndBands.Select(b => (b.ProviderId, b.CommandId)));
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
    /// Gets or sets whether titles are shown for items in this band.
    /// If null, falls back to dock-wide ShowLabels setting.
    /// </summary>
    public bool? ShowTitles { get; init; }

    /// <summary>
    /// Gets or sets whether subtitles are shown for items in this band.
    /// If null, falls back to dock-wide ShowLabels setting.
    /// </summary>
    public bool? ShowSubtitles { get; init; }

    /// <summary>
    /// Gets or sets a value for backward compatibility. Maps to ShowTitles.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool? ShowLabels
    {
        get => ShowTitles;
        init => ShowTitles = value;
    }

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

public enum DockSize
{
    Default,
    Compact,
}

public enum DockBackdrop
{
    Transparent,
    Acrylic,
}

#pragma warning restore SA1402 // File may only contain a single type
