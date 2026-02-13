// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Windows.Foundation.Collections;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Represents an icon that is a font glyph.
/// This is used for icons that are defined by a specific font face,
/// such as Wingdings.
///
/// Note that Command Palette will default to using the Segoe Fluent Icons,
/// Segoe MDL2 Assets font for glyphs in the Segoe UI Symbol range, or Segoe
/// UI for any other glyphs. This class is only needed if you want a non-Segoe
/// font icon.
/// </summary>
public partial class FontIconData : IconData, IExtendedAttributesProvider
{
    public string FontFamily { get; set; }

    public FontIconData(string glyph, string fontFamily)
        : base(glyph)
    {
        FontFamily = fontFamily;
    }

    public IDictionary<string, object>? GetProperties() => new ValueSet()
        {
            { WellKnownExtensionAttributes.FontFamily, FontFamily },
        };
}
