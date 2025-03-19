// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public sealed class ColorHelpers
{
    public static OptionalColor FromArgb(byte a, byte r, byte g, byte b) => new(true, new(r, g, b, a));

    public static OptionalColor FromRgb(byte r, byte g, byte b) => new(true, new(r, g, b, 255));

    public static OptionalColor Transparent() => new(true, new(0, 0, 0, 0));

    public static OptionalColor NoColor() => new(false, new(0, 0, 0, 0));
}
