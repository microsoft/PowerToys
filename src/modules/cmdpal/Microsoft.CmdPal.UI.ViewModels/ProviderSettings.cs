// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels;

public class ProviderSettings
{
    public bool IsEnabled { get; set; } = true;

    [JsonIgnore]
    public string PackageFamilyName { get; set; } = string.Empty;

    [JsonIgnore]
    public string Id { get; set; } = string.Empty;

    [JsonIgnore]
    public string ProviderDisplayName { get; set; } = string.Empty;

    // Originally, I wanted to do:
    //    public string ProviderId => $"{PackageFamilyName}/{ProviderDisplayName}";
    // but I think that's actually a bad idea, because the Display Name can be localized.
    [JsonIgnore]
    public string ProviderId => $"{PackageFamilyName}/{Id}";

    [JsonIgnore]
    public bool IsBuiltin => string.IsNullOrEmpty(PackageFamilyName);

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
        PackageFamilyName = wrapper.Extension?.PackageFamilyName ?? string.Empty;
        Id = wrapper.DisplayName;
        ProviderDisplayName = wrapper.DisplayName;
        if (ProviderId == "/")
        {
            throw new InvalidDataException("Did you add a built-in command and forget to set the Id? Make sure you do that!");
        }
    }
}
