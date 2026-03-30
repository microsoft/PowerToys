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

    public ImmutableList<PinnedCommandSettings> PinnedCommands { get; init; }
        = ImmutableList<PinnedCommandSettings>.Empty;

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

    public TimeSpan AutoGoHomeInterval { get; init; } = Timeout.InfiniteTimeSpan;

    public EscapeKeyBehavior EscapeKeyBehaviorSetting { get; init; } = EscapeKeyBehavior.ClearSearchFirstThenGoBack;

    public bool EnableDock { get; init; }

    public DockSettings DockSettings { get; init; } = new();

    // Theme settings
    public UserTheme Theme { get; init; } = UserTheme.Default;

    public ColorizationMode ColorizationMode { get; init; }

    public Color CustomThemeColor { get; init; } = new() { A = 0, R = 255, G = 255, B = 255 }; // Transparent — avoids WinUI3 COM dependency on Colors.Transparent

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

    public SettingsModel NormalizePinnedCommands()
    {
        var pinnedCommands = PinnedCommands;
        if (pinnedCommands.Count == 0)
        {
            var migratedPins = ImmutableList.CreateBuilder<PinnedCommandSettings>();
            foreach (var (providerId, providerSettings) in ProviderSettings.OrderBy(static kvp => kvp.Key, StringComparer.Ordinal))
            {
                foreach (var commandId in providerSettings.PinnedCommandIds)
                {
                    migratedPins.Add(new PinnedCommandSettings(providerId, commandId));
                }
            }

            pinnedCommands = migratedPins.ToImmutable();
        }

        return WithPinnedCommands(pinnedCommands);
    }

    public SettingsModel WithPinnedCommands(ImmutableList<PinnedCommandSettings> pinnedCommands)
    {
        var groupedPinnedCommands = pinnedCommands
            .GroupBy(static pin => pin.ProviderId, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static pin => pin.CommandId).ToImmutableList(),
                StringComparer.Ordinal);

        var allProviderIds = ProviderSettings.Keys.Union(groupedPinnedCommands.Keys, StringComparer.Ordinal).ToArray();
        var providerSettingsAlreadyMatch = allProviderIds.All(providerId =>
        {
            ProviderSettings.TryGetValue(providerId, out var currentProviderSettings);
            groupedPinnedCommands.TryGetValue(providerId, out var desiredPinnedIds);

            var currentPinnedIds = currentProviderSettings?.PinnedCommandIds ?? ImmutableList<string>.Empty;
            desiredPinnedIds ??= ImmutableList<string>.Empty;

            return currentPinnedIds.SequenceEqual(desiredPinnedIds);
        });

        if (PinnedCommands.SequenceEqual(pinnedCommands) && providerSettingsAlreadyMatch)
        {
            return this;
        }

        var providerSettingsBuilder = ProviderSettings.ToBuilder();
        foreach (var providerId in allProviderIds)
        {
            providerSettingsBuilder.TryGetValue(providerId, out var providerSettings);
            providerSettings ??= new ProviderSettings();

            groupedPinnedCommands.TryGetValue(providerId, out var desiredPinnedIds);
            desiredPinnedIds ??= ImmutableList<string>.Empty;

            providerSettingsBuilder[providerId] = providerSettings with { PinnedCommandIds = desiredPinnedIds };
        }

        return this with
        {
            PinnedCommands = pinnedCommands,
            ProviderSettings = providerSettingsBuilder.ToImmutable(),
        };
    }

    public bool IsCommandPinned(string providerId, string commandId)
    {
        foreach (var pinnedCommand in PinnedCommands)
        {
            if (pinnedCommand.ProviderId == providerId &&
                pinnedCommand.CommandId == commandId)
            {
                return true;
            }
        }

        return false;
    }

    public List<string> GetPinnedCommandIds(string providerId)
    {
        List<string> pinnedCommandIds = [];
        foreach (var pinnedCommand in PinnedCommands)
        {
            if (pinnedCommand.ProviderId == providerId)
            {
                pinnedCommandIds.Add(pinnedCommand.CommandId);
            }
        }

        return pinnedCommandIds;
    }

    public SettingsModel TryPinCommand(string providerId, string commandId)
    {
        if (IsCommandPinned(providerId, commandId))
        {
            return this;
        }

        return WithPinnedCommands(PinnedCommands.Add(new PinnedCommandSettings(providerId, commandId)));
    }

    public SettingsModel TryUnpinCommand(string providerId, string commandId)
    {
        for (var i = 0; i < PinnedCommands.Count; i++)
        {
            var pinnedCommand = PinnedCommands[i];
            if (pinnedCommand.ProviderId == providerId &&
                pinnedCommand.CommandId == commandId)
            {
                return WithPinnedCommands(PinnedCommands.RemoveAt(i));
            }
        }

        return this;
    }

    public SettingsModel TryMovePinnedCommand(string providerId, string commandId, bool moveUp, Func<PinnedCommandSettings, bool>? isVisible = null)
    {
        var index = FindPinnedCommandIndex(providerId, commandId);
        if (index < 0)
        {
            return this;
        }

        // Find the next visible neighbor in the move direction, skipping
        // stale entries (removed/disabled/failed extensions).
        var direction = moveUp ? -1 : 1;
        var targetIndex = index + direction;
        while (targetIndex >= 0 && targetIndex < PinnedCommands.Count &&
               isVisible != null && !isVisible(PinnedCommands[targetIndex]))
        {
            targetIndex += direction;
        }

        if (targetIndex < 0 || targetIndex >= PinnedCommands.Count)
        {
            return this;
        }

        // Remove and re-insert rather than swap so that stale entries
        // between index and targetIndex keep their relative positions.
        var pinnedCommand = PinnedCommands[index];
        var pinnedCommands = PinnedCommands.RemoveAt(index);
        pinnedCommands = pinnedCommands.Insert(targetIndex, pinnedCommand);

        return WithPinnedCommands(pinnedCommands);
    }

    public SettingsModel TryMovePinnedCommandToTop(string providerId, string commandId)
    {
        var index = FindPinnedCommandIndex(providerId, commandId);
        if (index <= 0)
        {
            return this;
        }

        var pinnedCommand = PinnedCommands[index];
        var pinnedCommands = PinnedCommands.RemoveAt(index);
        pinnedCommands = pinnedCommands.Insert(0, pinnedCommand);

        return WithPinnedCommands(pinnedCommands);
    }

    private int FindPinnedCommandIndex(string providerId, string commandId)
    {
        for (var i = 0; i < PinnedCommands.Count; i++)
        {
            var pinnedCommand = PinnedCommands[i];
            if (pinnedCommand.ProviderId == providerId &&
                pinnedCommand.CommandId == commandId)
            {
                return i;
            }
        }

        return -1;
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
[JsonSerializable(typeof(ImmutableList<PinnedCommandSettings>), TypeInfoPropertyName = "ImmutablePinnedCommandSettingsList")]
[JsonSerializable(typeof(ImmutableList<DockBandSettings>), TypeInfoPropertyName = "ImmutableDockBandSettingsList")]
[JsonSerializable(typeof(ImmutableDictionary<string, ProviderSettings>), TypeInfoPropertyName = "ImmutableProviderSettingsDictionary")]
[JsonSerializable(typeof(ImmutableDictionary<string, CommandAlias>), TypeInfoPropertyName = "ImmutableAliasDictionary")]
[JsonSerializable(typeof(ImmutableList<TopLevelHotkey>), TypeInfoPropertyName = "ImmutableTopLevelHotkeyList")]
[JsonSerializable(typeof(Dictionary<string, object>), TypeInfoPropertyName = "Dictionary")]
[JsonSerializable(typeof(PinnedCommandSettings))]
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
