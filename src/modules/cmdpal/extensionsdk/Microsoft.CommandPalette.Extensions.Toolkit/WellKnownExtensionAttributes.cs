// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public static class WellKnownExtensionAttributes
{
    public const string DataPackage = "Microsoft.CommandPalette.DataPackage";

    public const string DockCommandId = "Microsoft.CommandPalette.DockCommandId";

    public const string FontFamily = "FontFamily";

    /// <summary>
    /// The logical property name used with <c>PropChanged</c> when any Dock label-width hint changes.
    /// </summary>
    public const string DockLabelWidthPropertyName = "DockLabelWidth";

    /// <summary>
    /// The logical property name used with <c>PropChanged</c> when the Dock tabular-digits hint changes.
    /// </summary>
    public const string DockLabelTabularDigitsPropertyName = "DockLabelTabularDigits";

    /// <summary>
    /// The logical property name used with <c>PropChanged</c> when the Dock trailing-alignment hint changes.
    /// </summary>
    public const string DockLabelTrailingAlignmentPropertyName = "DockLabelTrailingAlignment";

    /// <summary>
    /// Optional minimum width of the shared title/subtitle area, excluding the icon and padding.
    /// Accepts a finite, non-negative double in DIPs, an invariant character count such as "10ch",
    /// or a literal sample prefixed with "text:". Character counts and samples use the title font and text scale.
    /// Limits clamp row reservations; without reservations they bound the content's natural width.
    /// Hidden labels reserve no space, and vertical docks may shrink below the minimum.
    /// Use <see cref="DockLabelWidthExtensions"/> or notify <see cref="DockLabelWidthPropertyName"/> after direct edits.
    /// </summary>
    public const string DockMinLabelWidth = "Microsoft.CommandPalette.Dock.MinLabelWidth";

    /// <summary>
    /// Optional maximum shared label width. Accepts the same forms as <see cref="DockMinLabelWidth"/>.
    /// Text exceeding the available width is ellipsized. If the resolved minimum exceeds the maximum,
    /// both limits are ignored. Invalid values are ignored independently.
    /// </summary>
    public const string DockMaxLabelWidth = "Microsoft.CommandPalette.Dock.MaxLabelWidth";

    /// <summary>
    /// Optional fixed title reservation, including when the displayed text is empty.
    /// Accepts the same forms as <see cref="DockMinLabelWidth"/>; measurement uses the title font and text scale.
    /// The host reserves the larger enabled row's width, clamped by the shared limits.
    /// Samples are independent of displayed text; "text:" reserves zero width.
    /// </summary>
    public const string DockTitleWidth = "Microsoft.CommandPalette.Dock.TitleWidth";

    /// <summary>
    /// Optional fixed subtitle reservation. Accepts the same forms as <see cref="DockTitleWidth"/>.
    /// Measurement uses the subtitle font and text scale. Compact mode excludes this reservation.
    /// </summary>
    public const string DockSubtitleWidth = "Microsoft.CommandPalette.Dock.SubtitleWidth";

    /// <summary>
    /// Optional bool that displays both rows with tabular digits. The extension controls decimal precision.
    /// Use <see cref="DockLabelPresentationExtensions"/> or notify <see cref="DockLabelTabularDigitsPropertyName"/> after direct edits.
    /// </summary>
    public const string DockLabelTabularDigits = "Microsoft.CommandPalette.Dock.TabularDigits";

    /// <summary>
    /// Optional bool that aligns both rows to the trailing edge, independently of numeral styling.
    /// Use <see cref="DockLabelPresentationExtensions"/> or notify <see cref="DockLabelTrailingAlignmentPropertyName"/> after direct edits.
    /// </summary>
    public const string DockLabelTrailingAlignment = "Microsoft.CommandPalette.Dock.TrailingAlignment";
}
