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
    private const string TextPlaceholder = "$cmdpal.incremental.text$";
    private const string InlineSvgPlaceholder = "$cmdpal.incremental.inline-svg$";
    private const string CustomElementPlaceholder = "$cmdpal.incremental.custom$";

    public static string Create(string cardJson) => Create(cardJson, null, null, null, null);

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
            (int?)mappedInlineSvgImageCount,
            null,
            null);
    }

    public static string Create(
        string cardJson,
        int mappedTextBlockCount,
        int mappedInlineSvgImageCount,
        int mappedCustomElementCount,
        IncrementalPatchableElements? patchableElements)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(mappedTextBlockCount);
        ArgumentOutOfRangeException.ThrowIfNegative(mappedInlineSvgImageCount);
        ArgumentOutOfRangeException.ThrowIfNegative(mappedCustomElementCount);
        return Create(
            cardJson,
            (int?)mappedTextBlockCount,
            (int?)mappedInlineSvgImageCount,
            mappedCustomElementCount,
            patchableElements);
    }

    private static string Create(
        string cardJson,
        int? mappedTextBlockCount,
        int? mappedInlineSvgImageCount,
        int? mappedCustomElementCount,
        IncrementalPatchableElements? patchableElements)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardJson);

        using var document = JsonDocument.Parse(cardJson);
        PatchableCounts authored = default;
        CountPatchableElements(document.RootElement, patchableElements, ref authored);

        var allowText = mappedTextBlockCount is null
            || mappedTextBlockCount == authored.TextBlocks;
        var allowInlineSvg = mappedInlineSvgImageCount is null
            || mappedInlineSvgImageCount == authored.InlineSvgImages;
        var allowCustom = patchableElements is { IsEmpty: false }
            && (mappedCustomElementCount is null
                || mappedCustomElementCount == authored.CustomElements);
        var options = new PatchOptions(allowText, allowInlineSvg, allowCustom, patchableElements);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteCanonicalValue(
                writer,
                document.RootElement,
                allowPatch: true,
                options);
        }

        return Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan));
    }

    private static void WriteCanonicalValue(
        Utf8JsonWriter writer,
        JsonElement value,
        bool allowPatch,
        PatchOptions options)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                WriteCanonicalObject(writer, value, allowPatch, options);
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    WriteCanonicalValue(writer, item, allowPatch, options);
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
        PatchOptions options)
    {
        var properties = new List<JsonProperty>();
        foreach (var property in value.EnumerateObject())
        {
            properties.Add(property);
        }

        properties.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));

        var typeName = GetTypeName(value);
        var isAction = typeName?.StartsWith("Action.", StringComparison.Ordinal) == true;
        var isTextBlock = allowPatch
            && options.AllowText
            && string.Equals(typeName, "TextBlock", StringComparison.Ordinal);
        var isImage = allowPatch
            && options.AllowInlineSvg
            && string.Equals(typeName, "Image", StringComparison.Ordinal);
        var isPatchableCustomElement = allowPatch
            && options.AllowCustom
            && options.PatchableElements!.Contains(typeName);

        writer.WriteStartObject();
        foreach (var property in properties)
        {
            writer.WritePropertyName(property.Name);
            if (isTextBlock && string.Equals(property.Name, "text", StringComparison.Ordinal))
            {
                writer.WriteStringValue(TextPlaceholder);
            }
            else if (isImage
                && string.Equals(property.Name, "url", StringComparison.Ordinal)
                && IsInlineSvg(property.Value))
            {
                writer.WriteStringValue(InlineSvgPlaceholder);
            }
            else if (isPatchableCustomElement
                && IncrementalPatchableElements.IsPatchableProperty(property.Name))
            {
                writer.WriteStringValue(CustomElementPlaceholder);
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
                    options);
            }
        }

        writer.WriteEndObject();
    }

    private static string? GetTypeName(JsonElement value) =>
        value.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String
            ? type.GetString()
            : null;

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
        IncrementalPatchableElements? patchableElements,
        ref PatchableCounts counts)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var typeName = GetTypeName(value);
                if (string.Equals(typeName, "TextBlock", StringComparison.Ordinal)
                    && value.TryGetProperty("text", out _))
                {
                    counts.TextBlocks++;
                }
                else if (string.Equals(typeName, "Image", StringComparison.Ordinal)
                    && value.TryGetProperty("url", out var url)
                    && IsInlineSvg(url))
                {
                    counts.InlineSvgImages++;
                }
                else if (patchableElements?.Contains(typeName) == true)
                {
                    counts.CustomElements++;
                }

                foreach (var property in value.EnumerateObject())
                {
                    CountPatchableElements(
                        property.Value,
                        patchableElements,
                        ref counts);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    CountPatchableElements(
                        item,
                        patchableElements,
                        ref counts);
                }

                break;
        }
    }

    private struct PatchableCounts
    {
        public int TextBlocks;
        public int InlineSvgImages;
        public int CustomElements;
    }

    private readonly record struct PatchOptions(
        bool AllowText,
        bool AllowInlineSvg,
        bool AllowCustom,
        IncrementalPatchableElements? PatchableElements);
}
