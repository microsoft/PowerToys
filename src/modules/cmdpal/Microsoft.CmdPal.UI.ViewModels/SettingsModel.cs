// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.UI;
using Windows.UI;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed record SettingsModel
{
    ///////////////////////////////////////////////////////////////////////////
    // SETTINGS HERE
    public static HotkeySettings DefaultActivationShortcut { get; } = new HotkeySettings(true, false, true, false, 0x20); // win+alt+space

    public HotkeySettings? Hotkey { get; init; } = DefaultActivationShortcut;

    public bool UseLowLevelGlobalHotkey { get; init; }

    public bool ShowAppDetails { get; init; }

    public bool BackspaceGoesBack { get; init; }

    public bool SingleClickActivates { get; init; }

    public bool HighlightSearchOnActivate { get; init; } = true;

    public bool KeepPreviousQuery { get; init; }

    public bool ShowSystemTrayIcon { get; init; } = true;

    public bool IgnoreShortcutWhenFullscreen { get; init; }

    public bool AllowExternalReload { get; init; }

    public ImmutableDictionary<string, ProviderSettings> ProviderSettings { get; init; } = ImmutableDictionary<string, ProviderSettings>.Empty;

    public string[] FallbackRanks { get; init; } = [];

    public ImmutableDictionary<string, CommandAlias> Aliases { get; init; } = ImmutableDictionary<string, CommandAlias>.Empty;

    public ImmutableList<TopLevelHotkey> CommandHotkeys { get; init; } = ImmutableList<TopLevelHotkey>.Empty;

    public MonitorBehavior SummonOn { get; init; } = MonitorBehavior.ToMouse;

    public bool DisableAnimations { get; init; } = true;

    public WindowPosition? LastWindowPosition { get; init; }

    public TimeSpan AutoGoHomeInterval { get; init; } = Timeout.InfiniteTimeSpan;

    public EscapeKeyBehavior EscapeKeyBehaviorSetting { get; init; } = EscapeKeyBehavior.ClearSearchFirstThenGoBack;

    public bool EnableDock { get; init; }

    public DockSettings DockSettings { get; init; } = new();

    // Theme settings
    public UserTheme Theme { get; init; } = UserTheme.Default;

    public ColorizationMode ColorizationMode { get; init; }

    public Color CustomThemeColor { get; init; } = Colors.Transparent;

    public int CustomThemeColorIntensity { get; init; } = 100;

    public int BackgroundImageTintIntensity { get; init; }

    public int BackgroundImageOpacity { get; init; } = 20;

    public int BackgroundImageBlurAmount { get; init; }

    public int BackgroundImageBrightness { get; init; }

    public BackgroundImageFit BackgroundImageFit { get; init; }

    public string? BackgroundImagePath { get; init; }

    public BackdropStyle BackdropStyle { get; init; }

    public int BackdropOpacity { get; init; } = 100;

    // </Theme settings>

    // END SETTINGS
    ///////////////////////////////////////////////////////////////////////////

    public string[] GetGlobalFallbacks()
    {
        var globalFallbacks = new HashSet<string>();

        foreach (var provider in ProviderSettings.Values)
        {
            foreach (var fallback in provider.FallbackCommands)
            {
                var fallbackSetting = fallback.Value;
                if (fallbackSetting.IsEnabled && fallbackSetting.IncludeInGlobalResults)
                {
                    globalFallbacks.Add(fallback.Key);
                }
            }
        }

        return globalFallbacks.ToArray();
    }
}

public enum MonitorBehavior
{
    ToMouse = 0,
    ToPrimary = 1,
    ToFocusedWindow = 2,
    InPlace = 3,
    ToLast = 4,
}

public enum EscapeKeyBehavior
{
    ClearSearchFirstThenGoBack = 0,
    AlwaysGoBack = 1,
    AlwaysDismiss = 2,
    AlwaysHide = 3,
}
