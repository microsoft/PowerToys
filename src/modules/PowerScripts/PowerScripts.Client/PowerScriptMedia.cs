// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace PowerScripts.Client;

/// <summary>One image or audio item. Data contains raw bytes, not base64 or a file path.</summary>
public sealed record PowerScriptMediaItem(string MimeType, byte[] Data);

/// <summary>A single inline media item or an explicitly declared collection.</summary>
public sealed class PowerScriptMedia
{
    /// <summary>Maximum decoded bytes in each item.</summary>
    public const int MaxItemBytes = 16 * 1024 * 1024;

    /// <summary>Maximum decoded bytes across a collection.</summary>
    public const int MaxTotalBytes = 32 * 1024 * 1024;

    /// <summary>Creates a scalar media value.</summary>
    public PowerScriptMedia(PowerScriptMediaItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Items = new[] { item };
    }

    /// <summary>Creates an array value, including when it contains only one item.</summary>
    public PowerScriptMedia(IReadOnlyList<PowerScriptMediaItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = items;
        IsArray = true;
    }

    /// <summary>The media items.</summary>
    public IReadOnlyList<PowerScriptMediaItem> Items { get; }

    /// <summary>Whether the schema-shaped value is an array rather than a single object.</summary>
    public bool IsArray { get; }

    internal void Write(Utf8JsonWriter writer, string category)
    {
        Validate(category);
        if (IsArray)
        {
            writer.WriteStartArray();
        }

        foreach (var item in Items)
        {
            writer.WriteStartObject();
            writer.WriteString("mimeType", item.MimeType);
            writer.WriteBase64String("data", item.Data);
            writer.WriteEndObject();
        }

        if (IsArray)
        {
            writer.WriteEndArray();
        }
    }

    internal static PowerScriptMedia Read(JsonElement value, string category)
    {
        if (value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
        {
            throw new JsonException($"'{category}' must be a media object or array.");
        }

        var items = new List<PowerScriptMediaItem>();
        var total = 0;
        void ReadItem(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Object || item.EnumerateObject().Count() != 2 ||
                !item.TryGetProperty("mimeType", out var mime) || mime.ValueKind != JsonValueKind.String ||
                !item.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("Media requires only 'mimeType' and base64 'data'.");
            }

            var encoded = data.GetString()!;
            if (encoded.Length is 0 or > ((MaxItemBytes + 2) / 3) * 4 ||
                encoded.Length % 4 != 0 || encoded.Any(char.IsWhiteSpace))
            {
                throw new JsonException("Invalid or oversized base64 media.");
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(encoded);
            }
            catch (FormatException ex)
            {
                throw new JsonException("Invalid base64 media.", ex);
            }

            total += bytes.Length;
            if (bytes.Length is 0 or > MaxItemBytes || total > MaxTotalBytes || items.Count >= 64)
            {
                throw new JsonException("Media exceeds the item, collection, or decoded byte limits.");
            }

            items.Add(new PowerScriptMediaItem(mime.GetString()!, bytes));
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                ReadItem(item);
            }
        }
        else
        {
            ReadItem(value);
        }

        var result = value.ValueKind == JsonValueKind.Array ? new PowerScriptMedia(items) : new PowerScriptMedia(items[0]);
        result.Validate(category);
        return result;
    }

    private void Validate(string category)
    {
        long total = 0;
        if (Items.Count is < 1 or > 64)
        {
            throw new JsonException("Media must contain between 1 and 64 items.");
        }

        foreach (var item in Items)
        {
            if (item is null || item.Data is null || item.Data.Length is 0 or > MaxItemBytes ||
                item.MimeType is null || !item.MimeType.StartsWith(category + "/", StringComparison.Ordinal))
            {
                throw new JsonException($"Media must contain nonempty {category} data, at most 16 MiB per item.");
            }

            total += item.Data.Length;
        }

        if (total > MaxTotalBytes)
        {
            throw new JsonException("Media exceeds the 32 MiB decoded collection limit.");
        }
    }
}
