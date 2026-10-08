// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>Lenient readers for chart element JSON. Invalid values read as missing.</summary>
internal static class ChartJson
{
    public static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    public static double? GetNumber(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value)
            ? ToNumber(value)
            : null;

    public static double? ToNumber(JsonElement value)
    {
        double number;
        switch (value.ValueKind)
        {
            case JsonValueKind.Number when value.TryGetDouble(out number):
                return double.IsFinite(number) ? number : null;
            case JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number):
                return double.IsFinite(number) ? number : null;
            default:
                return null;
        }
    }

    public static bool? GetBoolean(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    /// <summary>Reads a pixel length written as a number or as a string such as <c>"120px"</c>.</summary>
    public static double? GetPixels(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()?.Trim() ?? string.Empty;
            if (text.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            {
                text = text[..^2];
            }

            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var pixels)
                && double.IsFinite(pixels)
                && pixels >= 0
                    ? pixels
                    : null;
        }

        return ToNumber(value) is double number && number >= 0 ? number : null;
    }

    /// <summary>Formats an <c>x</c> value as a label. Strings are kept; numbers use the current culture.</summary>
    public static string? ToLabel(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetDouble(out var number) => number.ToString("G", CultureInfo.CurrentCulture),
        _ => null,
    };

    /// <summary>
    /// Reads an array of objects such as <c>[{ "legend": "A", "value": 1, "color": "good" }]</c>.
    /// Missing values read as zero; a <paramref name="fallbackValueProperty"/> covers schema aliases.
    /// </summary>
    public static List<ChartDataPoint> GetDataPoints(
        JsonElement element,
        string arrayProperty,
        string labelProperty,
        string valueProperty,
        ICollection<string> warnings,
        string? fallbackValueProperty = null)
    {
        var points = new List<ChartDataPoint>();
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(arrayProperty, out var array))
        {
            return points;
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            warnings.Add($"{arrayProperty} must be an array.");
            return points;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                warnings.Add($"Each {arrayProperty} item must be an object.");
                continue;
            }

            var value = GetNumber(item, valueProperty)
                ?? (fallbackValueProperty is null ? null : GetNumber(item, fallbackValueProperty));
            var label = item.TryGetProperty(labelProperty, out var labelValue) ? ToLabel(labelValue) : null;
            points.Add(new ChartDataPoint(label, value ?? 0, GetString(item, "color")));
        }

        return points;
    }

    public static TEnum GetEnum<TEnum>(
        JsonElement element,
        string propertyName,
        TEnum defaultValue,
        ICollection<string> warnings)
        where TEnum : struct, Enum
    {
        var text = GetString(element, propertyName);
        if (string.IsNullOrEmpty(text))
        {
            return defaultValue;
        }

        if (Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        warnings.Add($"{propertyName} has unknown value '{text}'.");
        return defaultValue;
    }

    /// <summary>
    /// Serializes <paramref name="element"/> with object properties sorted, so two equivalent
    /// elements always produce the same string.
    /// </summary>
    public static string Canonicalize(JsonElement element)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteCanonical(writer, element);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var properties = new List<JsonProperty>();
                foreach (var property in element.EnumerateObject())
                {
                    properties.Add(property);
                }

                properties.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
                writer.WriteStartObject();
                foreach (var property in properties)
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
