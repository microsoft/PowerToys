// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace MouseJump.Common.Helpers;

public static class ConfigHelper
{
    public static Color? ToUnnamedColor(Color? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var color = value.Value;
        return Color.FromArgb(color.A, color.R, color.G, color.B);
    }

    public static string? SerializeToConfigColorString(Color? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var color = value.Value;
        return color switch
        {
            Color { IsNamedColor: true } =>
                $"{nameof(Color)}.{color.Name}",
            Color { IsSystemColor: true } =>
                $"{nameof(SystemColors)}.{color.Name}",
            _ =>
                $"#{color.R:X2}{color.G:X2}{color.B:X2}",
        };
    }

    public static Color? DeserializeFromConfigColorString(string? value)
    {
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
}
