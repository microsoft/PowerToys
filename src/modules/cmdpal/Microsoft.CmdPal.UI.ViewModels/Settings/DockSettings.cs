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
