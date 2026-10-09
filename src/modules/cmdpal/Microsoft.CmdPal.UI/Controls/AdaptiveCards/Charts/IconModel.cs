// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

internal enum IconSize
{
    XxSmall,
    XSmall,
    Small,
    Standard,
    Medium,
    Large,
    XLarge,
    XxLarge,
}

internal enum IconStyle
{
    Regular,
    Filled,
}

internal enum IconColor
{
    Default,
    Dark,
    Light,
    Accent,
    Good,
    Warning,
    Attention,
}

internal enum IconAlignment
{
    Left,
    Center,
    Right,
}

/// <summary>
/// The parsed form of an Adaptive Cards <c>Icon</c> element: <c>name</c> (a Fluent icon name),
/// <c>size</c>, <c>style</c>, <c>color</c>, and <c>horizontalAlignment</c>.
/// </summary>
internal sealed class IconModel : IAdaptiveVisualModel
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the glyph to draw, or null when the name has no matching glyph.</summary>
    public string? Glyph { get; init; }

    public IconSize Size { get; init; } = IconSize.Standard;

    public IconStyle Style { get; init; }

    public IconColor Color { get; init; }

    public IconAlignment HorizontalAlignment { get; init; }

    public string IncrementalState { get; init; } = "{}";

    /// <summary>Gets whether the name has a glyph. Without one, the renderer draws the fallback.</summary>
    public bool RendersItself => Glyph is not null;

    /// <summary>Gets the size in pixels, from the Fluent icon sizes.</summary>
    public double PixelSize => Size switch
    {
        IconSize.XxSmall => 12,
        IconSize.XSmall => 16,
        IconSize.Small => 20,
        IconSize.Medium => 28,
        IconSize.Large => 32,
        IconSize.XLarge => 40,
        IconSize.XxLarge => 48,
        _ => 24,
    };

    /// <summary>Returns whether the name of an <c>Icon</c> element has a glyph, so the icon draws itself.</summary>
    public static bool HasGlyph(JsonElement element) =>
        FluentIconGlyphs.TryGetGlyph(ChartJson.GetString(element, "name"), filled: false, out _);

    public static IconModel Parse(string elementJson, ICollection<string> warnings)
    {
        try
        {
            using var document = JsonDocument.Parse(elementJson);
            var element = document.RootElement;
            var name = ChartJson.GetString(element, "name")?.Trim() ?? string.Empty;
            var style = ChartJson.GetEnum(element, "style", IconStyle.Regular, warnings);
            string? glyph = null;
            if (name.Length == 0)
            {
                warnings.Add("name is required.");
            }
            else if (FluentIconGlyphs.TryGetGlyph(name, style == IconStyle.Filled, out var found))
            {
                glyph = found;
            }
            else
            {
                warnings.Add($"name '{name}' has no matching glyph.");
            }

            return new IconModel
            {
                Name = name,
                Glyph = glyph,
                Size = ChartJson.GetEnum(element, "size", IconSize.Standard, warnings),
                Style = style,
                Color = ChartJson.GetEnum(element, "color", IconColor.Default, warnings),
                HorizontalAlignment = ChartJson.GetEnum(element, "horizontalAlignment", IconAlignment.Left, warnings),
                IncrementalState = ChartJson.Canonicalize(element),
            };
        }
        catch (JsonException ex)
        {
            warnings.Add($"The element JSON could not be read: {ex.Message}");
            return new IconModel();
        }
    }
}
