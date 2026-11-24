// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

// One immutable snapshot keeps all widths together when extension notifications arrive off the UI thread.
public sealed record DockLabelWidthConstraints(
    DockLabelLength? Minimum,
    DockLabelLength? Maximum,
    DockLabelLength? TitleWidth = null,
    DockLabelLength? SubtitleWidth = null)
{
    public static DockLabelWidthConstraints Default { get; } = new(null, null);

    public bool UsesFontMeasurements => UsesCharacters ||
        Minimum?.Sample is not null || Maximum?.Sample is not null ||
        TitleWidth?.Sample is not null || SubtitleWidth?.Sample is not null;

    public bool UsesCharacters =>
        Minimum?.InCharacters == true || Maximum?.InCharacters == true ||
        TitleWidth?.InCharacters == true || SubtitleWidth?.InCharacters == true;

    internal static DockLabelWidthConstraints FromProperties(IDictionary<string, object?>? properties)
    {
        object? minimum = null;
        object? maximum = null;
        object? titleWidth = null;
        object? subtitleWidth = null;
        properties?.TryGetValue(WellKnownExtensionAttributes.DockMinLabelWidth, out minimum);
        properties?.TryGetValue(WellKnownExtensionAttributes.DockMaxLabelWidth, out maximum);
        properties?.TryGetValue(WellKnownExtensionAttributes.DockTitleWidth, out titleWidth);
        properties?.TryGetValue(WellKnownExtensionAttributes.DockSubtitleWidth, out subtitleWidth);

        var minLength = DockLabelLength.Parse(minimum);
        var maxLength = DockLabelLength.Parse(maximum);
        var titleLength = DockLabelLength.Parse(titleWidth);
        var subtitleLength = DockLabelLength.Parse(subtitleWidth);
        return minLength is null && maxLength is null && titleLength is null && subtitleLength is null
            ? Default
            : new(minLength, maxLength, titleLength, subtitleLength);
    }

    public (double Minimum, double Maximum) Resolve(
        double titleCharacterWidth,
        double subtitleCharacterWidth,
        double defaultMinimum,
        double defaultMaximum,
        bool showTitle = true,
        bool showSubtitle = true,
        double? titleSampleWidth = null,
        double? subtitleSampleWidth = null,
        double? minimumSampleWidth = null,
        double? maximumSampleWidth = null)
    {
        if (!showTitle && !showSubtitle)
        {
            return (0, defaultMaximum);
        }

        var minimum = Minimum?.Resolve(titleCharacterWidth, minimumSampleWidth);
        var maximum = Maximum?.Resolve(titleCharacterWidth, maximumSampleWidth);

        // Compare after measurement: mixed units can change ordering with the font or text scale.
        if (minimum.HasValue && maximum.HasValue && minimum.Value > maximum.Value)
        {
            minimum = null;
            maximum = null;
        }

        var titleWidth = showTitle ? TitleWidth?.Resolve(titleCharacterWidth, titleSampleWidth) : null;
        var subtitleWidth = showSubtitle ? SubtitleWidth?.Resolve(subtitleCharacterWidth, subtitleSampleWidth) : null;
        if (titleWidth.HasValue || subtitleWidth.HasValue)
        {
            var width = Math.Clamp(Math.Max(titleWidth ?? 0, subtitleWidth ?? 0), minimum ?? 0, maximum ?? float.MaxValue);
            return (width, width);
        }

        // Explicit limits override conflicting defaults; defaults do not cap a row reservation.
        var min = minimum ?? Math.Min(defaultMinimum, maximum ?? defaultMaximum);
        var max = maximum ?? Math.Max(defaultMaximum, min);
        return (min, max);
    }
}
