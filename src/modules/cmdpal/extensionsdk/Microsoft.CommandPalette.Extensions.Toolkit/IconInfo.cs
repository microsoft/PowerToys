// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Storage.Streams;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class IconInfo : IIconInfo
{
    public virtual IconData Dark { get; set; } = new();

    public virtual IconData Light { get; set; } = new();

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

    public static IconInfo FromStream(IRandomAccessStream stream)
    {
        var data = new IconData(RandomAccessStreamReference.CreateFromStream(stream));
        return new IconInfo(data, data);
    }
}
