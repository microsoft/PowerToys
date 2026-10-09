// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

internal enum BadgeAppearance
{
    Filled,
    Tint,
}

internal enum BadgeShape
{
    Rounded,
    Square,
    Circular,
}

internal enum BadgeSize
{
    Medium,
    Large,
    ExtraLarge,
}

internal enum BadgeIconPosition
{
    Before,
    After,
}

/// <summary>The parsed form of an Adaptive Cards <c>Badge</c> element, with the schema's defaults.</summary>
internal sealed class BadgeModel : IAdaptiveVisualModel
{
    public string Text { get; init; } = string.Empty;

    public string? Style { get; init; }

    public BadgeAppearance Appearance { get; init; }

    public BadgeShape Shape { get; init; }

    public BadgeSize Size { get; init; }

    /// <summary>Gets the icon glyph to draw, or null when the badge has no icon or it has no glyph.</summary>
    public string? IconGlyph { get; init; }

    public BadgeIconPosition IconPosition { get; init; }

    public string? Tooltip { get; init; }

    public string IncrementalState { get; init; } = "{}";

    public static BadgeModel Parse(string elementJson, ICollection<string> warnings)
    {
        try
        {
            using var document = JsonDocument.Parse(elementJson);
            var element = document.RootElement;
            return new BadgeModel
            {
                Text = ChartJson.GetString(element, "text") ?? string.Empty,
                Style = ChartJson.GetString(element, "style"),
                Appearance = ChartJson.GetEnum(element, "appearance", BadgeAppearance.Filled, warnings),
                Shape = ChartJson.GetEnum(element, "shape", BadgeShape.Circular, warnings),
                Size = ChartJson.GetEnum(element, "size", BadgeSize.Medium, warnings),
                IconGlyph = GetIconGlyph(ChartJson.GetString(element, "icon"), warnings),
                IconPosition = ChartJson.GetEnum(element, "iconPosition", BadgeIconPosition.Before, warnings),
                Tooltip = ChartJson.GetString(element, "tooltip"),
                IncrementalState = ChartJson.Canonicalize(element),
            };
        }
        catch (JsonException ex)
        {
            warnings.Add($"The element JSON could not be read: {ex.Message}");
            return new BadgeModel();
        }
    }

    private static string? GetIconGlyph(string? icon, ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(icon))
        {
            return null;
        }

        var (name, filled) = FluentIconGlyphs.ParseReference(icon);
        if (FluentIconGlyphs.TryGetGlyph(name, filled, out var glyph))
        {
            return glyph;
        }

        warnings.Add($"icon '{name}' has no matching glyph.");
        return null;
    }
}
