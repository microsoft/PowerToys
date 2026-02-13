// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels;

public class ProviderSettings
{
    // List of built-in fallbacks that should not have global results enabled by default
    private readonly string[] _excludedBuiltInFallbacks = [
        "com.microsoft.cmdpal.builtin.indexer.fallback",
        "com.microsoft.cmdpal.builtin.calculator.fallback",
        ];

    public bool IsEnabled { get; set; } = true;

    public Dictionary<string, FallbackSettings> FallbackCommands { get; set; } = new();

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
                if (!FallbackCommands.ContainsKey(fallback.Id))
                {
                    var enableGlobalResults = IsBuiltin && !_excludedBuiltInFallbacks.Contains(fallback.Id);
                    FallbackCommands[fallback.Id] = new FallbackSettings(enableGlobalResults);
                }
            }
        }

        if (string.IsNullOrEmpty(ProviderId))
        {
            throw new InvalidDataException("Did you add a built-in command and forget to set the Id? Make sure you do that!");
        }
    }
}
