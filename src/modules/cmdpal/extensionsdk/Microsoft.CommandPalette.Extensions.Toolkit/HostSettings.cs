// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Concrete implementation of IHostSettings for the Command Palette host settings.
/// This class is used by the host to create settings objects that can be passed to extensions.
/// </summary>
public partial class HostSettings : IHostSettings
{
    /// <summary>
    /// Gets or sets the global hotkey for summoning the Command Palette.
    /// </summary>
    public string Hotkey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether to show app details in the UI.
    /// </summary>
    public bool ShowAppDetails { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether pressing the hotkey goes to the home page.
    /// </summary>
    public bool HotkeyGoesHome { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether backspace navigates back.
    /// </summary>
    public bool BackspaceGoesBack { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether single click activates items.
    /// </summary>
    public bool SingleClickActivates { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to highlight the search box on activation.
    /// </summary>
    public bool HighlightSearchOnActivate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show the system tray icon.
    /// </summary>
    public bool ShowSystemTrayIcon { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to ignore the shortcut when in fullscreen mode.
    /// </summary>
    public bool IgnoreShortcutWhenFullscreen { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether animations are disabled.
    /// </summary>
    public bool DisableAnimations { get; set; }

    /// <summary>
    /// Gets or sets the target monitor behavior when summoning the Command Palette.
    /// </summary>
    public SummonTarget SummonOn { get; set; }
}
