// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump.Helpers;

/// <summary>
/// Converts a color string from the settings file into a color, and vice versa.
/// </summary>
/// <remarks>
/// "Color.Red"              => Color.Red
/// "SystemColors.Highlight" => SystemColors.Highlight
/// "#AABBCC"                => Color.FromArgb(0xFF, 0xAA, 0xBB, 0xCC)
/// </remarks>
internal sealed class MouseJumpColorConverter : JsonConverter<Color?>
{
    public override Color? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        // e.g. "#AABBCC"
        if (value.StartsWith('#'))
        {
            var culture = CultureInfo.InvariantCulture;
            if ((value.Length == 7)
                && int.TryParse(value[1..3], NumberStyles.HexNumber, culture, out var r)
                && int.TryParse(value[3..5], NumberStyles.HexNumber, culture, out var g)
                && int.TryParse(value[5..7], NumberStyles.HexNumber, culture, out var b))
            {
                return Color.FromArgb(0xFF, r, g, b);
            }
        }

        const StringComparison comparison = StringComparison.InvariantCulture;

        // e.g. "Color.Red"
        const string colorPrefix = $"{nameof(Color)}.";
        if (value.StartsWith(colorPrefix, comparison))
        {
            var colorName = value[colorPrefix.Length..];
            var property = typeof(Color).GetProperties()
                .SingleOrDefault(property => property.Name == colorName);
            if (property is not null)
            {
                return (Color?)property.GetValue(null, null);
            }
        }

        // e.g. "SystemColors.Highlight"
        const string systemColorPrefix = $"{nameof(SystemColors)}.";
        if (value.StartsWith(systemColorPrefix, comparison))
        {
            var colorName = value[systemColorPrefix.Length..];
            var property = typeof(SystemColors).GetProperties()
                .SingleOrDefault(property => property.Name == colorName);
            if (property is not null)
            {
                return (Color?)property.GetValue(null, null);
            }
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, Color? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        var color = value.Value;
        switch (color)
        {
            case Color { IsSystemColor: true } _:
                writer.WriteStringValue($"{nameof(SystemColors)}.{color.Name}");
                break;
            case Color { IsNamedColor: true } _:
                writer.WriteStringValue($"{nameof(Color)}.{color.Name}");
                break;
            default:
                writer.WriteStringValue($"#{color.R:X2}{color.G:X2}{color.B:X2}");
                break;
        }
    }
}
