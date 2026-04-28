// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Windows.UI;

namespace Microsoft.CmdPal.UI.ViewModels;

public record SettingsModel
{
    ///////////////////////////////////////////////////////////////////////////
    // SETTINGS HERE
    public static HotkeySettings DefaultActivationShortcut { get; } = new HotkeySettings(true, false, true, false, 0x20); // win+alt+space

    private static readonly Color DefaultCustomThemeColor = new() { A = 0, R = 255, G = 255, B = 255 }; // Transparent — avoids WinUI3 COM dependency on Colors.Transparent

    public HotkeySettings? Hotkey { get; init; } = DefaultActivationShortcut;

    public bool UseLowLevelGlobalHotkey { get; init; }

    public bool ShowAppDetails { get; init; }

    public bool BackspaceGoesBack { get; init; }

    public bool SingleClickActivates { get; init; }

    public bool HighlightSearchOnActivate { get; init; } = true;

    public bool KeepPreviousQuery { get; init; }

    public bool ShowSystemTrayIcon { get; init; } = true;

    public bool IgnoreShortcutWhenFullscreen { get; init; } = true;

    public bool IgnoreShortcutWhenBusy { get; init; }

    public bool AllowBreakthroughShortcut { get; init; }

    public bool AllowExternalReload { get; init; }

    public ImmutableDictionary<string, ProviderSettings> ProviderSettings { get; init; }
        = ImmutableDictionary<string, ProviderSettings>.Empty;

    public string[] FallbackRanks { get; init; } = [];

    public ImmutableDictionary<string, CommandAlias> Aliases { get; init; }
        = ImmutableDictionary<string, CommandAlias>.Empty;

    public ImmutableList<TopLevelHotkey> CommandHotkeys { get; init; }
        = ImmutableList<TopLevelHotkey>.Empty;

    public MonitorBehavior SummonOn { get; init; } = MonitorBehavior.ToMouse;

    public bool DisableAnimations { get; init; } = true;

    public WindowPosition? LastWindowPosition { get; init; }

    [JsonPropertyName("AutoGoHomeInterval")]
    [JsonInclude]
    internal string? AutoGoHomeIntervalString
    {
        get => _autoGoHomeInterval?.ToString();
        init => _autoGoHomeInterval = TimeSpan.TryParse(value, out var ts) ? ts : null;
    }

    private TimeSpan? _autoGoHomeInterval;

    [JsonIgnore]
    public TimeSpan AutoGoHomeInterval
    {
        get => _autoGoHomeInterval ?? Timeout.InfiniteTimeSpan;
        init => _autoGoHomeInterval = value;
    }

    public EscapeKeyBehavior EscapeKeyBehaviorSetting { get; init; } = EscapeKeyBehavior.ClearSearchFirstThenGoBack;

    public bool EnableDock { get; init; }

    public DockSettings DockSettings { get; init; } = new();

    // Theme settings
    public UserTheme Theme { get; init; } = UserTheme.Default;

    public ColorizationMode ColorizationMode { get; init; }

    private Color? _customThemeColor;

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

    public SettingsModel()
    {
    }

    [JsonConstructor]
    public SettingsModel(
        ImmutableDictionary<string, ProviderSettings> providerSettings,
        string[] fallbackRanks,
        ImmutableDictionary<string, CommandAlias> aliases,
        ImmutableList<TopLevelHotkey> commandHotkeys,
        DockSettings dockSettings,
        bool highlightSearchOnActivate = true,
        bool showSystemTrayIcon = true,
        bool ignoreShortcutWhenFullscreen = true,
        bool disableAnimations = true,
        int customThemeColorIntensity = 100,
        int backgroundImageOpacity = 20,
        int backdropOpacity = 100)
    {
        ProviderSettings = providerSettings ?? ImmutableDictionary<string, ProviderSettings>.Empty;
        FallbackRanks = fallbackRanks ?? [];
        Aliases = aliases ?? ImmutableDictionary<string, CommandAlias>.Empty;
        CommandHotkeys = commandHotkeys ?? ImmutableList<TopLevelHotkey>.Empty;
        DockSettings = dockSettings ?? new();
        HighlightSearchOnActivate = highlightSearchOnActivate;
        ShowSystemTrayIcon = showSystemTrayIcon;
        IgnoreShortcutWhenFullscreen = ignoreShortcutWhenFullscreen;
        DisableAnimations = disableAnimations;
        CustomThemeColorIntensity = customThemeColorIntensity;
        BackgroundImageOpacity = backgroundImageOpacity;
        BackdropOpacity = backdropOpacity;
    }

    public (SettingsModel Model, ProviderSettings Settings) GetProviderSettings(CommandProviderWrapper provider)
    {
        if (!ProviderSettings.TryGetValue(provider.ProviderId, out var settings))
        {
            settings = new ProviderSettings();
        }

        var connected = settings.WithConnection(provider);

        // If WithConnection returned the same instance, nothing changed — skip SetItem
        if (ReferenceEquals(connected, settings))
        {
            return (this, connected);
        }

        var newModel = this with
        {
            ProviderSettings = ProviderSettings.SetItem(provider.ProviderId, connected),
        };
        return (newModel, connected);
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
[JsonSerializable(typeof(ImmutableList<HistoryItem>), TypeInfoPropertyName = "ImmutableHistoryList")]
[JsonSerializable(typeof(ImmutableDictionary<string, FallbackSettings>), TypeInfoPropertyName = "ImmutableFallbackDictionary")]
[JsonSerializable(typeof(ImmutableList<string>), TypeInfoPropertyName = "ImmutableStringList")]
[JsonSerializable(typeof(ImmutableList<DockBandSettings>), TypeInfoPropertyName = "ImmutableDockBandSettingsList")]
[JsonSerializable(typeof(ImmutableDictionary<string, ProviderSettings>), TypeInfoPropertyName = "ImmutableProviderSettingsDictionary")]
[JsonSerializable(typeof(ImmutableDictionary<string, CommandAlias>), TypeInfoPropertyName = "ImmutableAliasDictionary")]
[JsonSerializable(typeof(ImmutableList<TopLevelHotkey>), TypeInfoPropertyName = "ImmutableTopLevelHotkeyList")]
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
