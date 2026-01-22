// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Common.Models;

public partial class SettingsModel : ObservableObject
{
    /*************************************************************************
    * Make sure that you make the setters public (JsonSerializer.Deserialize will fail silently otherwise)!
    * Make sure that any new types you add are added to JsonSerializationContext!
    *************************************************************************/

    ///////////////////////////////////////////////////////////////////////////
    // SETTINGS HERE
    public static Hotkey DefaultActivationShortcut { get; } = new Hotkey(true, false, true, false, 0x20); // win+alt+space

    public Hotkey? Hotkey { get; set; } = DefaultActivationShortcut;

    public bool UseLowLevelGlobalHotkey { get; set; }

    public bool ShowAppDetails { get; set; }

    public bool BackspaceGoesBack { get; set; }

    public bool SingleClickActivates { get; set; }

    public bool HighlightSearchOnActivate { get; set; } = true;

    public bool ShowSystemTrayIcon { get; set; } = true;

    public bool IgnoreShortcutWhenFullscreen { get; set; }

    public bool AllowExternalReload { get; set; }

    public Dictionary<string, ProviderSettings> ProviderSettings { get; set; } = [];

    public string[] FallbackRanks { get; set; } = [];

    public Dictionary<string, CommandAlias> Aliases { get; set; } = [];

    public List<TopLevelHotkey> CommandHotkeys { get; set; } = [];

    public MonitorBehavior SummonOn { get; set; } = MonitorBehavior.ToMouse;

    public bool DisableAnimations { get; set; } = true;

    public WindowPosition? LastWindowPosition { get; set; }

    public TimeSpan AutoGoHomeInterval { get; set; } = Timeout.InfiniteTimeSpan;

    public EscapeKeyBehavior EscapeKeyBehaviorSetting { get; set; } = EscapeKeyBehavior.ClearSearchFirstThenGoBack;

    public UserTheme Theme { get; set; } = UserTheme.Default;

    public ColorizationMode ColorizationMode { get; set; }

    public Color CustomThemeColor { get; set; } = Colors.Transparent;

    public int CustomThemeColorIntensity { get; set; } = 100;

    public int BackgroundImageOpacity { get; set; } = 20;

    public int BackgroundImageBlurAmount { get; set; }

    public int BackgroundImageBrightness { get; set; }

    public BackgroundImageFit BackgroundImageFit { get; set; }

    public string? BackgroundImagePath { get; set; }

    // END SETTINGS
    ///////////////////////////////////////////////////////////////////////////

    public ProviderSettings GetProviderSettings(CommandProviderWrapper provider)
    {
        ProviderSettings? settings;
        if (!ProviderSettings.TryGetValue(provider.ProviderId, out settings))
        {
            settings = new ProviderSettings(provider);
            settings.Connect(provider);
            ProviderSettings[provider.ProviderId] = settings;
        }
        else
        {
            settings.Connect(provider);
        }

        return settings;
    }

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
