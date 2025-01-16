// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using AdvancedPaste.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace AdvancedPaste.Models.KernelQueryCache;

public sealed class PersistedCache : ISettingsConfig
{
    public record class CacheItem(CacheKey CacheKey, CacheValue CacheValue);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };

    public static PersistedCache FromJsonString(string json) => JsonSerializer.Deserialize<PersistedCache>(json, SerializerOptions);

    public string Version { get; init; }

    public List<CacheItem> Items { get; init; } = [];

    public string GetModuleName() => Constants.AdvancedPasteModuleName;

    public string ToJsonString() => JsonSerializer.Serialize(this, SerializerOptions);

    public override string ToString() => ToJsonString();

    public bool UpgradeSettingsConfiguration() => false;
}
