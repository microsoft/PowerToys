// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library;

public sealed class AdvancedPasteCustomActions
{
    private static readonly JsonSerializerOptions _serializerOptions = new(SettingsSerializationContext.Default.Options)
    {
        WriteIndented = true,
    };

    private readonly ObservableCollection<AdvancedPasteCustomAction> _value = [];

    [JsonPropertyName("value")]
    public ObservableCollection<AdvancedPasteCustomAction> Value
    {
        get => _value;
        init => _value = value ?? [];
    }

    public AdvancedPasteCustomActions()
    {
    }

    public string ToJsonString() => JsonSerializer.Serialize(this, _serializerOptions);
}
