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
    private const string UnusedFallbackPlaceholder = "$cmdpal.incremental.unused-fallback$";
    private const string FallbackProperty = "fallback";

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
                options,
                isDrawnFallback: false);
        }

        return Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan));
    }

    private static void WriteCanonicalValue(
        Utf8JsonWriter writer,
        JsonElement value,
        bool allowPatch,
        PatchOptions options,
        bool isDrawnFallback)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                WriteCanonicalObject(writer, value, allowPatch, options, isDrawnFallback);
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    WriteCanonicalValue(writer, item, allowPatch, options, isDrawnFallback: false);
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

    // A drawn fallback's control is tagged with the element it replaces, so its own text or image can't be patched.
    private static void WriteCanonicalObject(
        Utf8JsonWriter writer,
        JsonElement value,
        bool allowPatch,
        PatchOptions options,
        bool isDrawnFallback)
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
            && !isDrawnFallback
            && options.AllowText
            && string.Equals(typeName, "TextBlock", StringComparison.Ordinal);
        var isImage = allowPatch
            && !isDrawnFallback
            && options.AllowInlineSvg
            && string.Equals(typeName, "Image", StringComparison.Ordinal);
        var isPatchableCustomElement = allowPatch
            && options.AllowCustom
            && options.PatchableElements!.RendersItself(typeName, value);
        var hasUnusedFallback = allowPatch
            && RendersItself(value, typeName, options.PatchableElements);
        var drawsFallback = DrawsFallback(value, typeName, options.PatchableElements);

        writer.WriteStartObject();
        foreach (var property in properties)
        {
            writer.WritePropertyName(property.Name);
            if (hasUnusedFallback && string.Equals(property.Name, FallbackProperty, StringComparison.Ordinal))
            {
                writer.WriteStringValue(UnusedFallbackPlaceholder);
            }
            else if (isTextBlock && string.Equals(property.Name, "text", StringComparison.Ordinal))
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
                    options,
                    isDrawnFallback: drawsFallback && string.Equals(property.Name, FallbackProperty, StringComparison.Ordinal));
            }
        }

        writer.WriteEndObject();
    }

    private static string? GetTypeName(JsonElement value) =>
        value.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String
            ? type.GetString()
            : null;

    /// <summary>Returns whether <paramref name="value"/> draws itself, so its fallback is never drawn.</summary>
    private static bool RendersItself(
        JsonElement value,
        string? typeName,
        IncrementalPatchableElements? patchableElements) =>
        patchableElements?.RendersItself(typeName, value) == true
        && !value.TryGetProperty("requires", out _);

    /// <summary>
    /// Returns whether <paramref name="value"/> is a registered element that draws its fallback,
    /// such as an icon without a glyph.
    /// </summary>
    private static bool DrawsFallback(
        JsonElement value,
        string? typeName,
        IncrementalPatchableElements? patchableElements) =>
        patchableElements is not null
        && patchableElements.Contains(typeName)
        && !patchableElements.RendersItself(typeName, value)
        && !value.TryGetProperty("requires", out _);

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
        ref PatchableCounts counts,
        bool isDrawnFallback = false)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var typeName = GetTypeName(value);

                // A drawn fallback's own text or image is tagged with another element, so it's never mapped.
                if (!isDrawnFallback
                    && string.Equals(typeName, "TextBlock", StringComparison.Ordinal)
                    && value.TryGetProperty("text", out _))
                {
                    counts.TextBlocks++;
                }
                else if (!isDrawnFallback
                    && string.Equals(typeName, "Image", StringComparison.Ordinal)
                    && value.TryGetProperty("url", out var url)
                    && IsInlineSvg(url))
                {
                    counts.InlineSvgImages++;
                }
                else if (patchableElements?.RendersItself(typeName, value) == true)
                {
                    counts.CustomElements++;
                }

                // A fallback that's never drawn has no rendered elements to map.
                var hasUnusedFallback = RendersItself(value, typeName, patchableElements);
                var drawsFallback = DrawsFallback(value, typeName, patchableElements);
                foreach (var property in value.EnumerateObject())
                {
                    var isFallback = string.Equals(property.Name, FallbackProperty, StringComparison.Ordinal);
                    if (hasUnusedFallback && isFallback)
                    {
                        continue;
                    }

                    CountPatchableElements(
                        property.Value,
                        patchableElements,
                        ref counts,
                        drawsFallback && isFallback);
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
