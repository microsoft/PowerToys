// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

// One immutable snapshot keeps all bounds together when extension notifications arrive off the UI thread.
public sealed record DockLabelWidthConstraints(
    DockLabelLength? Minimum,
    DockLabelLength? Maximum,
    DockLabelLength? TitleWidth = null,
    DockLabelLength? SubtitleWidth = null,
    string? TitleWidthSample = null,
    string? SubtitleWidthSample = null)
{
    public static DockLabelWidthConstraints Default { get; } = new(null, null);

    public bool UsesFontMeasurements => UsesCharacters || TitleWidthSample is not null || SubtitleWidthSample is not null;

    public bool UsesCharacters
    {
        get
        {
            return Minimum?.InCharacters == true ||
                   Maximum?.InCharacters == true ||
                   TitleWidth?.InCharacters == true ||
                   SubtitleWidth?.InCharacters == true;
        }
    }

    internal static DockLabelWidthConstraints FromProperties(IDictionary<string, object?>? properties)
    {
        object? minimum = null;
        object? maximum = null;
        object? titleWidth = null;
        object? subtitleWidth = null;
        object? titleSample = null;
        object? subtitleSample = null;
        properties?.TryGetValue(WellKnownExtensionAttributes.DockMinLabelWidth, out minimum);
        properties?.TryGetValue(WellKnownExtensionAttributes.DockMaxLabelWidth, out maximum);
        properties?.TryGetValue(WellKnownExtensionAttributes.DockTitleWidth, out titleWidth);
        properties?.TryGetValue(WellKnownExtensionAttributes.DockSubtitleWidth, out subtitleWidth);
        properties?.TryGetValue(WellKnownExtensionAttributes.DockTitleWidthSample, out titleSample);
        properties?.TryGetValue(WellKnownExtensionAttributes.DockSubtitleWidthSample, out subtitleSample);

        var minLength = DockLabelLength.Parse(minimum);
        var maxLength = DockLabelLength.Parse(maximum);
        var titleLength = DockLabelLength.Parse(titleWidth);
        var subtitleLength = DockLabelLength.Parse(subtitleWidth);
        var titleText = titleSample as string;
        var subtitleText = subtitleSample as string;
        return minLength is null && maxLength is null && titleLength is null && subtitleLength is null && titleText is null && subtitleText is null
            ? Default
            : new(minLength, maxLength, titleLength, subtitleLength, titleText, subtitleText);
    }

    public (double Minimum, double Maximum) Resolve(
        double titleCharacterWidth,
        double subtitleCharacterWidth,
        double defaultMinimum,
        double defaultMaximum,
        bool showTitle = true,
        bool showSubtitle = true,
        double? titleSampleWidth = null,
        double? subtitleSampleWidth = null)
    {
        if (!showTitle && !showSubtitle)
        {
            return (0, defaultMaximum);
        }

        var titleWidth = showTitle ? ResolveRowWidth(TitleWidth, titleCharacterWidth, TitleWidthSample, titleSampleWidth) : null;
        var subtitleWidth = showSubtitle ? ResolveRowWidth(SubtitleWidth, subtitleCharacterWidth, SubtitleWidthSample, subtitleSampleWidth) : null;
        if (titleWidth.HasValue || subtitleWidth.HasValue)
        {
            var width = Math.Max(titleWidth ?? 0, subtitleWidth ?? 0);
            return (width, width);
        }

        var minimum = Minimum?.Resolve(titleCharacterWidth);
        var maximum = Maximum?.Resolve(titleCharacterWidth);

        // Compare after resolving: a pair can mix DIPs, ch, or sqh, and text scaling can change its ordering.
        if (minimum.HasValue && maximum.HasValue && minimum.Value > maximum.Value)
        {
            return (defaultMinimum, defaultMaximum);
        }

        // An explicit bound takes precedence over the opposite default. In particular, a requested
        // minimum above the default cap must not be rejected just because no maximum was provided.
        var min = minimum ?? Math.Min(defaultMinimum, maximum ?? defaultMaximum);
        var max = maximum ?? Math.Max(defaultMaximum, min);
        return (min, max);
    }

    private static double? ResolveRowWidth(DockLabelLength? length, double characterWidth, string? sample, double? sampleWidth)
    {
        if (sample is not null && sampleWidth is >= 0 and <= float.MaxValue)
        {
            return sampleWidth;
        }

        return length?.Resolve(characterWidth);
    }
}
