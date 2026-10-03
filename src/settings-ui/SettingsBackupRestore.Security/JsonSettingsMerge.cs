// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Microsoft.PowerToys.SettingsBackupRestore.Security;

/// <summary>
/// Preserves the current recursive object/array merge behavior.
/// </summary>
public static class JsonSettingsMerge
{
    /// <summary>
    /// Merges backup JSON into current JSON, replacing scalars and de-duplicating array items.
    /// </summary>
    public static string Merge(string currentJson, string backupJson)
    {
        ArrayBufferWriter<byte> output = new();
        using JsonDocument current = JsonDocument.Parse(currentJson);
        using JsonDocument backup = JsonDocument.Parse(backupJson);
        using Utf8JsonWriter writer = new(output, new JsonWriterOptions { Indented = true });

        if (current.RootElement.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object))
        {
            throw new InvalidOperationException("Current JSON must be an object or array.");
        }

        if (current.RootElement.ValueKind != backup.RootElement.ValueKind)
        {
            return currentJson;
        }

        if (current.RootElement.ValueKind == JsonValueKind.Array)
        {
            MergeArrays(writer, current.RootElement, backup.RootElement);
        }
        else
        {
            MergeObjects(writer, current.RootElement, backup.RootElement);
        }

        writer.Flush();
        return Encoding.UTF8.GetString(output.WrittenSpan);
    }

    private static void MergeObjects(Utf8JsonWriter writer, JsonElement current, JsonElement backup)
    {
        writer.WriteStartObject();
        foreach (JsonProperty property in current.EnumerateObject())
        {
            if (backup.TryGetProperty(property.Name, out JsonElement backupValue) && backupValue.ValueKind != JsonValueKind.Null)
            {
                writer.WritePropertyName(property.Name);
                if (property.Value.ValueKind == JsonValueKind.Object && backupValue.ValueKind == JsonValueKind.Object)
                {
                    MergeObjects(writer, property.Value, backupValue);
                }
                else if (property.Value.ValueKind == JsonValueKind.Array && backupValue.ValueKind == JsonValueKind.Array)
                {
                    MergeArrays(writer, property.Value, backupValue);
                }
                else
                {
                    backupValue.WriteTo(writer);
                }
            }
            else
            {
                property.WriteTo(writer);
            }
        }

        foreach (JsonProperty property in backup.EnumerateObject())
        {
            if (!current.TryGetProperty(property.Name, out _))
            {
                property.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static void MergeArrays(Utf8JsonWriter writer, JsonElement current, JsonElement backup)
    {
        writer.WriteStartArray();
        HashSet<string> items = new(StringComparer.Ordinal);
        foreach (JsonElement item in current.EnumerateArray())
        {
            item.WriteTo(writer);
            items.Add(item.ToString());
        }

        foreach (JsonElement item in backup.EnumerateArray())
        {
            if (!items.Contains(item.ToString()))
            {
                item.WriteTo(writer);
            }
        }

        writer.WriteEndArray();
    }
}
