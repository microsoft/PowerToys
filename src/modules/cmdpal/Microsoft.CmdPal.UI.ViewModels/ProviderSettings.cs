// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels;

public class ProviderSettings
{
    public bool IsEnabled { get; set; } = true;

    public Dictionary<string, bool> FallbackCommands { get; set; } = [];

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

        if (string.IsNullOrEmpty(ProviderId))
        {
            throw new InvalidDataException("Did you add a built-in command and forget to set the Id? Make sure you do that!");
        }
    }

    public bool IsFallbackEnabled(TopLevelViewModel command)
    {
        return FallbackCommands.TryGetValue(command.Id, out var enabled) ? enabled : true;
    }

    public void SetFallbackEnabled(TopLevelViewModel command, bool enabled)
    {
        FallbackCommands[command.Id] = enabled;
    }
}
