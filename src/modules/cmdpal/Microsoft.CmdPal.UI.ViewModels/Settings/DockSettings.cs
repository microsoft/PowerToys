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
public class DockSettings
{
    public DockSide Side { get; set; } = DockSide.Top;

    public DockSize DockSize { get; set; } = DockSize.Small;

    public DockSize DockIconsSize { get; set; } = DockSize.Small;

    // <Theme settings>
    public DockBackdrop Backdrop { get; set; } = DockBackdrop.Acrylic;

    public UserTheme Theme { get; set; } = UserTheme.Default;

    public ColorizationMode ColorizationMode { get; set; }

    public Color CustomThemeColor { get; set; } = Colors.Transparent;

    public int CustomThemeColorIntensity { get; set; } = 100;

    public int BackgroundImageOpacity { get; set; } = 20;

    public int BackgroundImageBlurAmount { get; set; }

    public int BackgroundImageBrightness { get; set; }

    public BackgroundImageFit BackgroundImageFit { get; set; }

    public string? BackgroundImagePath { get; set; }

    // </Theme settings>
    // public List<string> PinnedCommands { get; set; } = [];
    public List<DockBandSettings> StartBands { get; set; } = [];

    public List<DockBandSettings> CenterBands { get; set; } = [];

    public List<DockBandSettings> EndBands { get; set; } = [];

    public bool ShowLabels { get; set; } = true;

    [JsonIgnore]
    public IEnumerable<(string ProviderId, string CommandId)> AllPinnedCommands =>
        StartBands.Select(b => (b.ProviderId, b.CommandId))
        .Concat(CenterBands.Select(b => (b.ProviderId, b.CommandId)))
        .Concat(EndBands.Select(b => (b.ProviderId, b.CommandId)));

    public DockSettings()
    {
        // Initialize with default values
        // PinnedCommands = [
        //     "com.microsoft.cmdpal.winget"
        // ];
        StartBands.Add(new DockBandSettings
        {
            ProviderId = "com.microsoft.cmdpal.builtin.core",
            CommandId = "com.microsoft.cmdpal.home",
        });
        StartBands.Add(new DockBandSettings
        {
            ProviderId = "WinGet",
            CommandId = "com.microsoft.cmdpal.winget",
            ShowLabels = false,
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
public class DockBandSettings
{
    public required string ProviderId { get; set; }

    public required string CommandId { get; set; }

    /// <summary>
    /// Gets or sets whether titles are shown for items in this band.
    /// If null, falls back to dock-wide ShowLabels setting.
    /// </summary>
    public bool? ShowTitles { get; set; }

    /// <summary>
    /// Gets or sets whether subtitles are shown for items in this band.
    /// If null, falls back to dock-wide ShowLabels setting.
    /// </summary>
    public bool? ShowSubtitles { get; set; }

    /// <summary>
    /// Gets or sets a value for backward compatibility. Maps to ShowTitles.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool? ShowLabels
    {
        get => ShowTitles;
        set => ShowTitles = value;
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

    public DockBandSettings Clone()
    {
        return new()
        {
            ProviderId = this.ProviderId,
            CommandId = this.CommandId,
            ShowTitles = this.ShowTitles,
            ShowSubtitles = this.ShowSubtitles,
        };
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
