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
    /// Optional minimum width of a Dock item's shared title/subtitle area, excluding its icon and padding.
    /// The value is a non-negative, finite <see cref="double"/> in DIPs, or an invariant string such as
    /// <c>"10ch"</c> measured in widths of the zero glyph in the Dock title's font and text scale.
    /// The <c>sqh</c> (squirrel hair width) unit is defined as <c>0.01ch</c>: <c>"1000sqh"</c> equals <c>"10ch"</c>.
    /// Use <see cref="DockLabelWidthExtensions"/> to set both bounds and notify automatically.
    /// For direct property-bag edits, raise <c>PropChanged</c> with the name <see cref="DockLabelWidthPropertyName"/>
    /// or use <c>"Properties"</c> to invalidate the entire bag.
    /// The host can reduce the reservation to fit a vertical Dock. Hidden labels reserve no space.
    /// </summary>
    public const string DockMinLabelWidth = "Microsoft.CommandPalette.Dock.MinLabelWidth";

    /// <summary>
    /// Optional maximum width of a Dock item's shared title/subtitle area. Accepts the same value
    /// forms as <see cref="DockMinLabelWidth"/>. Text exceeding the available width is ellipsized.
    /// Omitted or invalid bounds use the host's defaults; a minimum greater than an explicitly
    /// supplied maximum causes both bounds to be ignored.
    /// </summary>
    public const string DockMaxLabelWidth = "Microsoft.CommandPalette.Dock.MaxLabelWidth";

    /// <summary>
    /// Optional fixed width reserved for the title, even when its text is empty.
    /// Accepts the same values as <see cref="DockMinLabelWidth"/>; character units use the title font and text scale.
    /// The host fixes the shared label width to the largest valid reservation among the enabled rows.
    /// Row reservations take precedence over shared label bounds. Hidden rows do not contribute.
    /// Use <c>SetDockLabelWidths</c> or notify <see cref="DockLabelWidthPropertyName"/> after direct edits.
    /// </summary>
    public const string DockTitleWidth = "Microsoft.CommandPalette.Dock.TitleWidth";

    /// <summary>
    /// Optional fixed width reserved for the subtitle, even when its text is empty.
    /// Character units use the subtitle font and text scale; compact mode excludes this reservation.
    /// If no valid reservation applies to an enabled row, the host uses the shared label bounds or its defaults.
    /// Use <c>SetDockLabelWidths</c> or notify <see cref="DockLabelWidthPropertyName"/> after direct edits.
    /// </summary>
    public const string DockSubtitleWidth = "Microsoft.CommandPalette.Dock.SubtitleWidth";

    /// <summary>
    /// Optional literal text measured in the title's font and text scale to reserve a fixed width.
    /// Takes precedence over <see cref="DockTitleWidth"/>; an empty string reserves zero width.
    /// The sample is independent of the displayed title and is not rendered.
    /// Use <c>SetDockLabelWidthSamples</c> or notify <see cref="DockLabelWidthPropertyName"/> after direct edits.
    /// </summary>
    public const string DockTitleWidthSample = "Microsoft.CommandPalette.Dock.TitleWidthSample";

    /// <summary>
    /// Optional literal text measured in the subtitle's font and text scale to reserve a fixed width.
    /// Takes precedence over <see cref="DockSubtitleWidth"/>; compact mode excludes this reservation.
    /// The sample is independent of the displayed subtitle and is not rendered.
    /// Use <c>SetDockLabelWidthSamples</c> or notify <see cref="DockLabelWidthPropertyName"/> after direct edits.
    /// </summary>
    public const string DockSubtitleWidthSample = "Microsoft.CommandPalette.Dock.SubtitleWidthSample";

    /// <summary>
    /// Optional <see cref="bool"/> hint that displays a Dock item's title and subtitle with tabular digits.
    /// The extension remains responsible for formatting consistent decimal precision.
    /// Use <see cref="DockLabelPresentationExtensions"/> to set or clear the hint and notify automatically.
    /// For direct property-bag edits, raise <c>PropChanged</c> with the name
    /// <see cref="DockLabelTabularDigitsPropertyName"/> or use <c>"Properties"</c> to invalidate the entire bag.
    /// </summary>
    public const string DockLabelTabularDigits = "Microsoft.CommandPalette.Dock.TabularDigits";

    /// <summary>
    /// Optional <see cref="bool"/> hint that aligns a Dock item's title and subtitle to the trailing edge
    /// of their label area. This hint is independent of <see cref="DockLabelTabularDigits"/>.
    /// Use <see cref="DockLabelPresentationExtensions"/> to set or clear the hint and notify automatically.
    /// For direct property-bag edits, raise <c>PropChanged</c> with the name
    /// <see cref="DockLabelTrailingAlignmentPropertyName"/> or use <c>"Properties"</c> to invalidate the entire bag.
    /// </summary>
    public const string DockLabelTrailingAlignment = "Microsoft.CommandPalette.Dock.TrailingAlignment";
}
