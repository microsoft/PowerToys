// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension.Helpers;

internal static class ColorSwatchIconFactory
{
    public static IconInfo Create(byte r, byte g, byte b, byte a)
        => new($"|Swatch|#{a:X2}{r:X2}{g:X2}{b:X2}|");
}
