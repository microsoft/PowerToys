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
    DockLabelLength? SubtitleWidth = null)
{
    public static DockLabelWidthConstraints Default { get; } = new(null, null);

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

    public (double Minimum, double Maximum) Resolve(double titleCharacterWidth, double subtitleCharacterWidth, double defaultMinimum, double defaultMaximum, bool showTitle = true, bool showSubtitle = true)
    {
        if (!showTitle && !showSubtitle)
        {
            return (0, defaultMaximum);
        }

        var titleWidth = showTitle ? TitleWidth?.Resolve(titleCharacterWidth) : null;
        var subtitleWidth = showSubtitle ? SubtitleWidth?.Resolve(subtitleCharacterWidth) : null;
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
}
