// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation.Collections;
using Windows.Storage.Streams;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class FontIconData : IconData, IHaveProperties
{
    public string FontFace { get; set; }

    public FontIconData(string glyph, string fontFace)
        : base(glyph)
    {
        FontFace = fontFace;
    }

    public IPropertySet Properties => new PropertySet()
        {
            { "FontFamily", FontFace },
        };
}
