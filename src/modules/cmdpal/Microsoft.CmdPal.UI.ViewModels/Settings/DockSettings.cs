// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

    // Theme settings
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

    // /Theme settings
    public List<string> PinnedCommands { get; set; } = [];

    public List<DockBandSettings> StartBands { get; set; } = [];

    public List<DockBandSettings> CenterBands { get; set; } = [];

    public List<DockBandSettings> EndBands { get; set; } = [];

    public bool ShowLabels { get; set; } = true;

    public DockSettings()
    {
        // Initialize with default values
        PinnedCommands = [
            "com.microsoft.cmdpal.winget"
        ];

        StartBands.Add(new DockBandSettings { Id = "com.microsoft.cmdpal.home" });
        StartBands.Add(new DockBandSettings { Id = "com.microsoft.cmdpal.winget", ShowLabels = false });

        EndBands.Add(new DockBandSettings { Id = "com.microsoft.cmdpal.performanceWidget" });
        EndBands.Add(new DockBandSettings { Id = "com.microsoft.cmdpal.timedate.dockband" });
    }
}

/// <summary>
/// Settings for a specific dock band. These are per-band settings stored
/// within the overall <see cref="DockSettings"/>.
/// </summary>
public class DockBandSettings
{
    public string Id { get; set; } = string.Empty;

    public bool? ShowLabels { get; set; }

    /// <summary>
    /// Resolves the effective value of <see cref="ShowLabels"/> for this band.
    /// If this band doesn't have a specific value set, we'll fall back to the
    /// dock-wide setting (passed as <paramref name="defaultValue"/>).
    /// </summary>
    public bool ResolveShowLabels(bool defaultValue) => ShowLabels ?? defaultValue;
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
