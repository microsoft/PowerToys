// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Windows.UI;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class SettingsModel : ObservableObject
{
    ///////////////////////////////////////////////////////////////////////////
    // SETTINGS HERE
    public static HotkeySettings DefaultActivationShortcut { get; } = new HotkeySettings(true, false, true, false, 0x20); // win+alt+space

    public HotkeySettings? Hotkey { get; set; } = DefaultActivationShortcut;

    public bool UseLowLevelGlobalHotkey { get; set; }

    public bool ShowAppDetails { get; set; }

    public bool BackspaceGoesBack { get; set; }

    public bool SingleClickActivates { get; set; }

    public bool HighlightSearchOnActivate { get; set; } = true;

    public bool KeepPreviousQuery { get; set; }

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

    public bool EnableDock { get; set; }

    public DockSettings DockSettings { get; set; } = new();

    // Theme settings
    public UserTheme Theme { get; set; } = UserTheme.Default;

    public ColorizationMode ColorizationMode { get; set; }

    public Color CustomThemeColor { get; set; } = new() { A = 0, R = 255, G = 255, B = 255 }; // Transparent — avoids WinUI3 COM dependency on Colors.Transparent

    public int CustomThemeColorIntensity { get; set; } = 100;

    public int BackgroundImageTintIntensity { get; set; }

    public int BackgroundImageOpacity { get; set; } = 20;

    public int BackgroundImageBlurAmount { get; set; }

    public int BackgroundImageBrightness { get; set; }

    public BackgroundImageFit BackgroundImageFit { get; set; }

    public string? BackgroundImagePath { get; set; }

    public string? BackgroundImageSlideshowFolderPath { get; set; }

    public int BackgroundImageChangeIntervalMinutes { get; set; }

    public bool BackgroundImageShuffle { get; set; } = true;

    public BackdropStyle BackdropStyle { get; set; }

    public int BackdropOpacity { get; set; } = 100;

    // </Theme settings>

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

    // [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "<Pending>")]
    // private static readonly JsonSerializerOptions _serializerOptions = new()
    // {
    //    WriteIndented = true,
    //    Converters = { new JsonStringEnumConverter() },
    // };
    // private static readonly JsonSerializerOptions _deserializerOptions = new()
    // {
    //    PropertyNameCaseInsensitive = true,
    //    IncludeFields = true,
    //    Converters = { new JsonStringEnumConverter() },
    //    AllowTrailingCommas = true,
    // };
}

[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(Color))]
[JsonSerializable(typeof(HistoryItem))]
[JsonSerializable(typeof(SettingsModel))]
[JsonSerializable(typeof(WindowPosition))]
[JsonSerializable(typeof(AppStateModel))]
[JsonSerializable(typeof(RecentCommandsManager))]
[JsonSerializable(typeof(List<string>), TypeInfoPropertyName = "StringList")]
[JsonSerializable(typeof(List<HistoryItem>), TypeInfoPropertyName = "HistoryList")]
[JsonSerializable(typeof(Dictionary<string, object>), TypeInfoPropertyName = "Dictionary")]
[JsonSourceGenerationOptions(UseStringEnumConverter = true, WriteIndented = true, IncludeFields = true, PropertyNameCaseInsensitive = true, AllowTrailingCommas = true)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Just used here")]
internal sealed partial class JsonSerializationContext : JsonSerializerContext
{
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
