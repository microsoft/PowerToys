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
    public ProviderSettings(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }

    /// <summary>
    /// Returns a new ProviderSettings connected to the given wrapper.
    /// Pure function — does not mutate this instance.
    /// </summary>
    public ProviderSettings WithConnection(CommandProviderWrapper wrapper)
    {
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
                }
            }
        }

        return this with
        {
            ProviderId = wrapper.ProviderId,
            IsBuiltin = wrapper.Extension is null,
            ProviderDisplayName = wrapper.DisplayName,
            FallbackCommands = builder.ToImmutable(),
        };
    }
}
