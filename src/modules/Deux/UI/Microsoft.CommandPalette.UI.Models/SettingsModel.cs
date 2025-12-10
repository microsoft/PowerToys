// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.UI.Models;

public partial class SettingsModel
{
    /*************************************************************************
    * Make sure that you make the setters public (JsonSerializer.Deserialize will fail silently otherwise)!
    * Make sure that any new types you add are added to JsonSerializationContext!
    *************************************************************************/

    public static HotkeySettings DefaultActivationShortcut { get; } = new HotkeySettings(true, false, true, false, 0x20); // win+alt+space

    public HotkeySettings? Hotkey { get; set; } = DefaultActivationShortcut;

    public bool UseLowLevelGlobalHotkey { get; set; }

    public bool ShowAppDetails { get; set; }

    public bool BackspaceGoesBack { get; set; }

    public bool SingleClickActivates { get; set; }

    public bool HighlightSearchOnActivate { get; set; } = true;

    public bool ShowSystemTrayIcon { get; set; } = true;

    public bool IgnoreShortcutWhenFullscreen { get; set; }

    public bool AllowExternalReload { get; set; }

    public MonitorBehavior SummonOn { get; set; } = MonitorBehavior.ToMouse;

    public bool DisableAnimations { get; set; } = true;

    public bool EnableAnimations { get; set; }

    public WindowPosition? LastWindowPosition { get; set; }

    public TimeSpan AutoGoHomeInterval { get; set; } = Timeout.InfiniteTimeSpan;
}
