// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public class ProviderSettings
{
    // List of built-in fallbacks that should not have global results enabled by default
    private static readonly string[] ExcludedBuiltInFallbacks = [
        "com.microsoft.cmdpal.builtin.indexer.fallback",
        "com.microsoft.cmdpal.builtin.calculator.fallback",
        ];

    private static readonly Dictionary<string, FallbackSettingsDefaults> DefaultFallbackSettings = new(StringComparer.Ordinal)
    {
        ["com.microsoft.cmdpal.builtin.indexer.fallback"] = new(QueryDelayMilliseconds: 120, MinQueryLength: 2),
        ["com.microsoft.cmdpal.builtin.remotedesktop.fallback"] = new(QueryDelayMilliseconds: 75, MinQueryLength: 2),
        ["com.microsoft.cmdpal.builtin.shell.fallback"] = new(QueryDelayMilliseconds: 100, MinQueryLength: 2),
    };

    public bool IsEnabled { get; set; } = true;

    public Dictionary<string, FallbackSettings> FallbackCommands { get; set; } = new();

    public List<string> PinnedCommandIds { get; set; } = [];

    [JsonIgnore]
    public string ProviderDisplayName { get; set; } = string.Empty;

    [JsonIgnore]
    public string ProviderId { get; private set; } = string.Empty;

    [JsonIgnore]
    public bool IsBuiltin { get; private set; }

    public ProviderSettings(CommandProviderWrapper wrapper)
    {
        Connect(wrapper);
    }

    [JsonConstructor]
    public ProviderSettings(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }

    public void Connect(CommandProviderWrapper wrapper)
    {
        ProviderId = wrapper.ProviderId;
        IsBuiltin = wrapper.Extension is null;

        ProviderDisplayName = wrapper.DisplayName;

        if (wrapper.FallbackItems.Length > 0)
        {
            foreach (var fallback in wrapper.FallbackItems)
            {
                if (!FallbackCommands.TryGetValue(fallback.Id, out var fallbackSettings))
                {
                    var enableGlobalResults = IsBuiltin && !ExcludedBuiltInFallbacks.Contains(fallback.Id);
                    fallbackSettings = new FallbackSettings(enableGlobalResults);
                    FallbackCommands[fallback.Id] = fallbackSettings;
                }
            }
        }

        if (string.IsNullOrEmpty(ProviderId))
        {
            throw new InvalidDataException("Did you add a built-in command and forget to set the Id? Make sure you do that!");
        }
    }

    internal static uint? GetSuggestedFallbackQueryDelayMilliseconds(string fallbackId, IFallbackCommandItemDefaults? defaults = null)
    {
        if (defaults is not null && defaults.SuggestedQueryDelayMilliseconds.HasValue)
        {
            return defaults.SuggestedQueryDelayMilliseconds.Value;
        }

        return DefaultFallbackSettings.TryGetValue(fallbackId, out var fallbackDefaults) ? fallbackDefaults.QueryDelayMilliseconds : null;
    }

    internal static uint? GetSuggestedFallbackMinQueryLength(string fallbackId, IFallbackCommandItemDefaults? defaults = null)
    {
        if (defaults is not null && defaults.SuggestedMinQueryLength.HasValue)
        {
            return defaults.SuggestedMinQueryLength.Value;
        }

        return DefaultFallbackSettings.TryGetValue(fallbackId, out var fallbackDefaults) ? fallbackDefaults.MinQueryLength : null;
    }

    internal static FallbackExecutionPolicy GetEffectiveFallbackExecutionPolicy(string fallbackId, FallbackSettings? fallbackSettings, IFallbackCommandItemDefaults? defaults = null)
    {
        var delayMilliseconds = fallbackSettings?.QueryDelayMilliseconds ?? GetSuggestedFallbackQueryDelayMilliseconds(fallbackId, defaults);
        var minQueryLength = fallbackSettings?.MinQueryLength ?? GetSuggestedFallbackMinQueryLength(fallbackId, defaults);
        var delay = delayMilliseconds is uint value ? TimeSpan.FromMilliseconds(value) : TimeSpan.Zero;
        return new FallbackExecutionPolicy(delay, minQueryLength ?? 0);
    }

    private readonly record struct FallbackSettingsDefaults(uint? QueryDelayMilliseconds, uint? MinQueryLength);
}
