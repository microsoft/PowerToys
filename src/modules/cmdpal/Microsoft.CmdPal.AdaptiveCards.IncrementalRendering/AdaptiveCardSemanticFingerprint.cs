// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;

namespace Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;

/// <summary>
/// Produces a deterministic Adaptive Card fingerprint while excluding properties that the
/// incremental adapter has verified it can patch. All other authored semantics remain
/// replacement-sensitive.
/// </summary>
internal static class AdaptiveCardSemanticFingerprint
{
    public static string Create(string cardJson) => Create(cardJson, null, null);

    public static string Create(
        string cardJson,
        int mappedTextBlockCount,
        int mappedInlineSvgImageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(mappedTextBlockCount);
        ArgumentOutOfRangeException.ThrowIfNegative(mappedInlineSvgImageCount);
        return Create(
            cardJson,
            (int?)mappedTextBlockCount,
            (int?)mappedInlineSvgImageCount);
    }

    private static string Create(
        string cardJson,
        int? mappedTextBlockCount,
        int? mappedInlineSvgImageCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardJson);

        using var document = JsonDocument.Parse(cardJson);
        var authoredTextBlockCount = 0;
        var authoredInlineSvgImageCount = 0;
        CountPatchableElements(
            document.RootElement,
            ref authoredTextBlockCount,
            ref authoredInlineSvgImageCount);

        var allowTextPatch = mappedTextBlockCount is null
            || mappedTextBlockCount == authoredTextBlockCount;
        var allowInlineSvgPatch = mappedInlineSvgImageCount is null
            || mappedInlineSvgImageCount == authoredInlineSvgImageCount;
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteCanonicalValue(
                writer,
                document.RootElement,
                allowPatch: true,
                allowTextPatch,
                allowInlineSvgPatch);
        }

        return Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan));
    }

    private static void WriteCanonicalValue(
        Utf8JsonWriter writer,
        JsonElement value,
        bool allowPatch,
        bool allowTextPatch,
        bool allowInlineSvgPatch)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                WriteCanonicalObject(writer, value, allowPatch, allowTextPatch, allowInlineSvgPatch);
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    WriteCanonicalValue(writer, item, allowPatch, allowTextPatch, allowInlineSvgPatch);
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
        bool allowPatch,
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
        var isTextBlock = allowPatch
            && allowTextPatch
            && string.Equals(typeName, "TextBlock", StringComparison.Ordinal);
        var isImage = allowPatch
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
                var childAllowsPatch = allowPatch
                    && !isAction
                    && !IsActionProperty(property.Name);
                WriteCanonicalValue(
                    writer,
                    property.Value,
                    childAllowsPatch,
                    allowTextPatch,
                    allowInlineSvgPatch);
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
            && IsInlineSvg(value.GetString());
    }

    private static bool IsInlineSvg(string? value)
    {
        return value?.StartsWith("data:image/svg+xml,", StringComparison.OrdinalIgnoreCase) == true
            || value?.StartsWith("data:image/svg+xml;", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static void CountPatchableElements(
        JsonElement value,
        ref int textBlockCount,
        ref int inlineSvgImageCount)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var typeName = value.TryGetProperty("type", out var type)
                    && type.ValueKind == JsonValueKind.String
                        ? type.GetString()
                        : null;
                if (string.Equals(typeName, "TextBlock", StringComparison.Ordinal)
                    && value.TryGetProperty("text", out _))
                {
                    textBlockCount++;
                }
                else if (string.Equals(typeName, "Image", StringComparison.Ordinal)
                    && value.TryGetProperty("url", out var url)
                    && IsInlineSvg(url))
                {
                    inlineSvgImageCount++;
                }

                foreach (var property in value.EnumerateObject())
                {
                    CountPatchableElements(
                        property.Value,
                        ref textBlockCount,
                        ref inlineSvgImageCount);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    CountPatchableElements(
                        item,
                        ref textBlockCount,
                        ref inlineSvgImageCount);
                }

                break;
        }
    }
}