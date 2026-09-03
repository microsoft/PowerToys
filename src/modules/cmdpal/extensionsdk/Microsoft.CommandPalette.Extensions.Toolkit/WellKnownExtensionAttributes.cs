// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public static class WellKnownExtensionAttributes
{
    public const string DataPackage = "Microsoft.CommandPalette.DataPackage";

    public const string FontFamily = "FontFamily";

    /// <summary>
    /// Optional minimum width of a Dock item's shared title/subtitle area, excluding its icon and padding.
    /// The value is a non-negative, finite <see cref="double"/> in DIPs, or an invariant string such as
    /// <c>"10ch"</c> measured in widths of the zero glyph in the Dock title's font and text scale.
    /// The <c>sqh</c> (squirrel hair width) unit is defined as <c>0.01ch</c>: <c>"1000sqh"</c> equals <c>"10ch"</c>.
    /// Use <see cref="DockLabelWidthExtensions"/> to set both bounds and notify automatically.
    /// For direct property-bag edits, raise <c>PropChanged</c> with the name <c>"Properties"</c>.
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
}
