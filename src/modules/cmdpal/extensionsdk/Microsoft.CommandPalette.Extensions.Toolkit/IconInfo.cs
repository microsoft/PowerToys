// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class IconInfo : IIconInfo
{
    public IconData Dark { get; set; } = new();

    public IconData Light { get; set; } = new();

    IIconData IIconInfo.Dark => Dark;

    IIconData IIconInfo.Light => Light;

    public IconInfo(string? icon)
    {
        Dark = Light = new(icon);
    }

    public IconInfo(IconData light, IconData dark)
    {
        Light = light;
        Dark = dark;
    }

    internal IconInfo()
        : this(string.Empty)
    {
    }
}
