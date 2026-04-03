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
    private static readonly ImmutableList<DockBandSettings> DefaultStartBands = ImmutableList.Create(
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

    private static readonly ImmutableList<DockBandSettings> DefaultEndBands = ImmutableList.Create(
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

    private static readonly Color DefaultCustomThemeColor = new() { A = 0, R = 255, G = 255, B = 255 }; // Transparent — avoids WinUI3 COM dependency on Colors.Transparent and COM in class init

    private readonly Color? _customThemeColor;

    public DockSide Side { get; init; } = DockSide.Top;

    public DockSize DockSize { get; init; } = DockSize.Small;

    public DockSize DockIconsSize { get; init; } = DockSize.Small;

    public bool AlwaysOnTop { get; set; } = true;

    // <Theme settings>
    public DockBackdrop Backdrop { get; init; } = DockBackdrop.Acrylic;

    public UserTheme Theme { get; init; } = UserTheme.Default;

    public ColorizationMode ColorizationMode { get; init; }

    [JsonPropertyName("CustomThemeColor")]
    [JsonInclude]
    internal Color? CustomThemeColorFallback
    {
        get => _customThemeColor;
        init => _customThemeColor = value;
    }

    [JsonIgnore]
    public Color CustomThemeColor
    {
        get => _customThemeColor ?? DefaultCustomThemeColor;
        init => _customThemeColor = value;
    }

    public int CustomThemeColorIntensity { get; init; } = 100;

    public int BackgroundImageOpacity { get; init; } = 20;

    public int BackgroundImageBlurAmount { get; init; }

    public int BackgroundImageBrightness { get; init; }

    public BackgroundImageFit BackgroundImageFit { get; init; }

    public string? BackgroundImagePath { get; init; }

    // </Theme settings>
    public ImmutableList<DockBandSettings> StartBands { get; init; } = DefaultStartBands;

    public ImmutableList<DockBandSettings> CenterBands { get; init; } = ImmutableList<DockBandSettings>.Empty;

    public ImmutableList<DockBandSettings> EndBands { get; init; } = DefaultEndBands;

    public bool ShowLabels { get; init; } = true;

    [JsonIgnore]
    public IEnumerable<(string ProviderId, string CommandId)> AllPinnedCommands =>
        StartBands.Select(b => (b.ProviderId, b.CommandId))
        .Concat(CenterBands.Select(b => (b.ProviderId, b.CommandId)))
        .Concat(EndBands.Select(b => (b.ProviderId, b.CommandId)));

    public DockSettings()
    {
    }

    [JsonConstructor]
    public DockSettings(
        ImmutableList<DockBandSettings> startBands,
        ImmutableList<DockBandSettings> centerBands,
        ImmutableList<DockBandSettings> endBands,
        DockSide side = DockSide.Top,
        bool alwaysOnTop = true,
        DockBackdrop backdrop = DockBackdrop.Acrylic,
        int customThemeColorIntensity = 100,
        int backgroundImageOpacity = 20,
        bool showLabels = true)
    {
        StartBands = startBands ?? DefaultStartBands;
        CenterBands = centerBands ?? ImmutableList<DockBandSettings>.Empty;
        EndBands = endBands ?? DefaultEndBands;
        Side = side;
        AlwaysOnTop = alwaysOnTop;
        Backdrop = backdrop;
        CustomThemeColorIntensity = customThemeColorIntensity;
        BackgroundImageOpacity = backgroundImageOpacity;
        ShowLabels = showLabels;
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
    public bool? ShowTitles { get; init; }

    /// <summary>
    /// Gets whether subtitles are shown for items in this band.
    /// If null, falls back to dock-wide ShowLabels setting.
    /// </summary>
    public bool? ShowSubtitles { get; init; }

    /// <summary>
    /// Gets a value for backward compatibility. Maps to ShowTitles.
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
