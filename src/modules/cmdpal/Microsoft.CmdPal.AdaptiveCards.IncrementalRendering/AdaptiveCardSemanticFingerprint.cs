// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;

/// <summary>
/// Produces a deterministic Adaptive Card fingerprint while excluding properties that the first
/// incremental adapter can safely patch. All other authored semantics remain replacement-sensitive.
/// </summary>
public static class AdaptiveCardSemanticFingerprint
{
    public static string Create(string cardJson, bool allowInlineSvgPatch = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardJson);

        using var document = JsonDocument.Parse(cardJson);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteCanonicalValue(writer, document.RootElement, allowTextPatch: true, allowInlineSvgPatch);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    public static int CountInlineSvgImages(string cardJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardJson);

        using var document = JsonDocument.Parse(cardJson);
        return CountInlineSvgImages(document.RootElement);
    }

    private static void WriteCanonicalValue(
        Utf8JsonWriter writer,
        JsonElement value,
        bool allowTextPatch,
        bool allowInlineSvgPatch)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                WriteCanonicalObject(writer, value, allowTextPatch, allowInlineSvgPatch);
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    WriteCanonicalValue(writer, item, allowTextPatch, allowInlineSvgPatch);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetRawText(), skipInputValidation: true);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                break;
        }
    }

    private static void WriteCanonicalObject(
        Utf8JsonWriter writer,
        JsonElement value,
        bool allowTextPatch,
        bool allowInlineSvgPatch)
    {
        var properties = new List<JsonProperty>();
        foreach (var property in value.EnumerateObject())
        {
            properties.Add(property);
        }

        properties.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));

        var typeName = value.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String
            ? type.GetString()
            : null;
        var isAction = typeName?.StartsWith("Action.", StringComparison.Ordinal) == true;
        var isTextBlock = allowTextPatch
            && string.Equals(typeName, "TextBlock", StringComparison.Ordinal);
        var isImage = allowTextPatch
            && allowInlineSvgPatch
            && string.Equals(typeName, "Image", StringComparison.Ordinal);

        writer.WriteStartObject();
        foreach (var property in properties)
        {
            writer.WritePropertyName(property.Name);
            if (isTextBlock && string.Equals(property.Name, "text", StringComparison.Ordinal))
            {
                writer.WriteStringValue("$cmdpal.incremental.text$");
            }
            else if (isImage
                && string.Equals(property.Name, "url", StringComparison.Ordinal)
                && IsInlineSvg(property.Value))
            {
                writer.WriteStringValue("$cmdpal.incremental.inline-svg$");
            }
            else
            {
                var childAllowsTextPatch = allowTextPatch
                    && !isAction
                    && !IsActionProperty(property.Name);
                WriteCanonicalValue(writer, property.Value, childAllowsTextPatch, allowInlineSvgPatch);
            }
        }

        writer.WriteEndObject();
    }

    private static bool IsActionProperty(string propertyName) => propertyName is
        "actions" or
        "selectAction" or
        "inlineAction";

    private static bool IsInlineSvg(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.String
            && value.GetString()?.StartsWith("data:image/svg+xml", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static int CountInlineSvgImages(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var count = value.TryGetProperty("type", out var type)
                    && type.ValueKind == JsonValueKind.String
                    && string.Equals(type.GetString(), "Image", StringComparison.Ordinal)
                    && value.TryGetProperty("url", out var url)
                    && IsInlineSvg(url)
                        ? 1
                        : 0;
                foreach (var property in value.EnumerateObject())
                {
                    count += CountInlineSvgImages(property.Value);
                }

                return count;
            case JsonValueKind.Array:
                var arrayCount = 0;
                foreach (var item in value.EnumerateArray())
                {
                    arrayCount += CountInlineSvgImages(item);
                }

                return arrayCount;
            default:
                return 0;
        }
    }
}