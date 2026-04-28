// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels;

public record ProviderSettings
{
    // List of built-in fallbacks that should not have global results enabled by default
    private static readonly string[] _excludedBuiltInFallbacks = [
        "com.microsoft.cmdpal.builtin.indexer.fallback",
        "com.microsoft.cmdpal.builtin.calculator.fallback",
        "com.microsoft.cmdpal.builtin.remotedesktop.fallback",
        ];

    public bool IsEnabled { get; init; } = true;

    public ImmutableDictionary<string, FallbackSettings> FallbackCommands { get; init; }
        = ImmutableDictionary<string, FallbackSettings>.Empty;

    public ImmutableList<string> PinnedCommandIds { get; init; }
        = ImmutableList<string>.Empty;

    [JsonIgnore]
    public string ProviderId { get; init; } = string.Empty;

    [JsonIgnore]
    public bool IsBuiltin { get; init; }

    [JsonIgnore]
    public string ProviderDisplayName { get; init; } = string.Empty;

    public ProviderSettings()
    {
    }

    [JsonConstructor]
    public ProviderSettings(
        ImmutableDictionary<string, FallbackSettings> fallbackCommands,
        ImmutableList<string> pinnedCommandIds,
        bool isEnabled = true)
    {
        IsEnabled = isEnabled;
        FallbackCommands = fallbackCommands ?? ImmutableDictionary<string, FallbackSettings>.Empty;
        PinnedCommandIds = pinnedCommandIds ?? ImmutableList<string>.Empty;
    }

    /// <summary>
    /// Returns a new ProviderSettings connected to the given wrapper.
    /// Returns <see langword="this"/> when the connection produces no changes.
    /// Pure function — does not mutate this instance.
    /// </summary>
    public ProviderSettings WithConnection(CommandProviderWrapper wrapper)
    {
        if (string.IsNullOrWhiteSpace(wrapper.ProviderId))
        {
            throw new ArgumentException("ProviderId must not be null, empty, or whitespace.", nameof(wrapper));
        }

        var changed = false;
        var builder = FallbackCommands.ToBuilder();
        if (wrapper.FallbackItems.Length > 0)
        {
            foreach (var fallback in wrapper.FallbackItems)
            {
                if (!string.IsNullOrEmpty(fallback.Id) && !builder.ContainsKey(fallback.Id))
                {
                    var enableGlobalResults = (wrapper.Extension is null)
                        && !_excludedBuiltInFallbacks.Contains(fallback.Id);
                    builder[fallback.Id] = new FallbackSettings(enableGlobalResults);
                    changed = true;
                }
            }
        }

        var isBuiltin = wrapper.Extension is null;

        // If nothing changed, return the same instance to avoid unnecessary allocations and saves
        if (!changed
            && ProviderId == wrapper.ProviderId
            && IsBuiltin == isBuiltin
            && ProviderDisplayName == wrapper.DisplayName)
        {
            return this;
        }

        return this with
        {
            ProviderId = wrapper.ProviderId,
            IsBuiltin = isBuiltin,
            ProviderDisplayName = wrapper.DisplayName,
            FallbackCommands = changed ? builder.ToImmutable() : FallbackCommands,
        };
    }
}
