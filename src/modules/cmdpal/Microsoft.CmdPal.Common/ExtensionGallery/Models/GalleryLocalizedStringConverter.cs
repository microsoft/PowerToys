// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.Common.ExtensionGallery.Models;

/// <summary>
/// Supports migration from legacy per-field localization object (for example {"en":"...", "cs-cz":"..."})
/// to direct string values in manifests.
/// </summary>
public sealed class GalleryLocalizedStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Localized field must be a string or localization object.");
        }

        Dictionary<string, string> localizedValues = new(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name while reading localization object.");
            }

            var language = reader.GetString();
            if (!reader.Read())
            {
                throw new JsonException("Unexpected end of localization object.");
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (!string.IsNullOrWhiteSpace(language) && !string.IsNullOrWhiteSpace(value))
                {
                    localizedValues[language] = value.Trim();
                }
            }
            else
            {
                using var ignored = JsonDocument.ParseValue(ref reader);
            }
        }

        return ResolveLocalizedValue(localizedValues);
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value ?? string.Empty);
    }

    private static string ResolveLocalizedValue(IReadOnlyDictionary<string, string> localizedValues)
    {
        if (localizedValues.Count == 0)
        {
            return string.Empty;
        }

        var culture = CultureInfo.CurrentUICulture;
        while (!culture.Equals(CultureInfo.InvariantCulture))
        {
            if (TryGetByCulture(localizedValues, culture, out var value))
            {
                return value;
            }

            culture = culture.Parent;
        }

        if (localizedValues.TryGetValue("en", out var english))
        {
            return english;
        }

        foreach (var pair in localizedValues)
        {
            return pair.Value;
        }

        return string.Empty;
    }

    private static bool TryGetByCulture(IReadOnlyDictionary<string, string> localizedValues, CultureInfo culture, out string value)
    {
        if (!string.IsNullOrWhiteSpace(culture.Name)
            && localizedValues.TryGetValue(culture.Name, out var cultureValue)
            && !string.IsNullOrWhiteSpace(cultureValue))
        {
            value = cultureValue;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(culture.TwoLetterISOLanguageName)
            && localizedValues.TryGetValue(culture.TwoLetterISOLanguageName, out var languageValue)
            && !string.IsNullOrWhiteSpace(languageValue))
        {
            value = languageValue;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
