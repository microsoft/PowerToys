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

/// <summary>
/// The parsed form of an Adaptive Cards <c>Badge</c> element: <c>text</c>, <c>style</c>
/// (<c>default</c>, <c>subtle</c>, <c>informative</c>, <c>accent</c>, <c>good</c>,
/// <c>attention</c>, <c>warning</c>), <c>appearance</c>, <c>shape</c>, <c>size</c>, and
/// <c>tooltip</c>.
/// </summary>
internal sealed class BadgeModel : IAdaptiveVisualModel
{
    public string Text { get; init; } = string.Empty;

    public string? Style { get; init; }

    public BadgeAppearance Appearance { get; init; }

    public BadgeShape Shape { get; init; }

    public BadgeSize Size { get; init; }

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
                Shape = ChartJson.GetEnum(element, "shape", BadgeShape.Rounded, warnings),
                Size = ChartJson.GetEnum(element, "size", BadgeSize.Medium, warnings),
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
}
